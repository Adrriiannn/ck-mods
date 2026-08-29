#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves the four small surfaces this wave opened actually reach the game.
    /// </summary>
    /// <remarks>
    /// Each of these was a value an author could set — or was about to be able to set — with
    /// nothing at the far end. The assertions are on the emitted registration text because that
    /// string is the only thing that survives the editor: whatever is not written into it cannot
    /// be read by the running game afterwards, whatever the asset says.
    /// </remarks>
    internal sealed class DimensionLeftoverSurfaceTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        // ------------------------------------------------ the crafting window's look ---

        [Test]
        public void AStationWearingTheStoneWindow_SaysSoInTheBootstrap()
        {
            DimensionTemplateAsset template = TemplateWithWorkbench(
                "Alchemy", DimensionCraftingWindowLook.Stone);

            string emitted = EmitRecipes(template);

            Assert.That(
                emitted,
                Does.Contain("DimensionCraftingBenchLookRegistry.Register(\"TestMod:Alchemy\", 1)"),
                "The window's look is looked up per entity because every generated station shares " +
                "one pooled view, so it has to travel to the running game as a row keyed on the " +
                "station's name. Stone is 1 in the game's own CraftingUIThemeType.");
        }

        [Test]
        public void AStationWearingTheOrdinaryWoodenWindow_NeedsNoRowAtAll()
        {
            DimensionTemplateAsset template = TemplateWithWorkbench(
                "Bench", DimensionCraftingWindowLook.Wooden);

            Assert.That(
                EmitRecipes(template),
                Does.Not.Contain("DimensionCraftingBenchLookRegistry"),
                "Wood is what the registry and the game itself both fall back to, so a row saying " +
                "wood changes nothing and only makes the generated file harder to read.");
        }

        [Test]
        public void AWorkbenchThatGeneratesNoObject_GetsNoWindowRow()
        {
            DimensionTemplateAsset template = TemplateWithWorkbench(
                "Grouping", DimensionCraftingWindowLook.Merchant);
            SetBool(template.GlobalWorkbenches[0], "generatesItsOwnObject", false);

            Assert.That(
                EmitRecipes(template),
                Does.Not.Contain("DimensionCraftingBenchLookRegistry"),
                "A Workbench that makes no object of its own is a way of grouping recipes onto " +
                "somebody else's bench. It never builds a view, so a row for it would sit in the " +
                "table for ever matching nothing.");
        }

        [Test]
        public void EveryWindowLook_IsOneTheGameActuallyAuthored()
        {
            // Counted from Resources/Global Objects (Main Manager).prefab, which ships exactly
            // five craftingUIThemes rows. UIManager.GetCraftingUITheme logs an error and returns
            // null for anything the list does not hold, and its caller dereferences that null.
            Assert.That(
                System.Enum.GetValues(typeof(DimensionCraftingWindowLook)).Length,
                Is.EqualTo(5),
                "Offering a sixth look would be offering a window the game has no artwork for.");

            Assert.That((int)DimensionCraftingWindowLook.Wooden, Is.EqualTo(0));
            Assert.That((int)DimensionCraftingWindowLook.Stone, Is.EqualTo(1));
            Assert.That((int)DimensionCraftingWindowLook.Merchant, Is.EqualTo(2));
            Assert.That((int)DimensionCraftingWindowLook.UpgradeForge, Is.EqualTo(3));
            Assert.That(
                (int)DimensionCraftingWindowLook.Dangerous,
                Is.EqualTo(4),
                "The numbers are cast straight to the game's CraftingUIThemeType, so renumbering " +
                "them would re-skin every station already authored.");
        }

        // ----------------------------------------------- the dimension's own music ---

        [Test]
        public void ADimensionNamingOneOfTheGamesRosters_SetsItAndAsksForNoTracks()
        {
            DimensionTemplateAsset template = TemplateWithMusic("MYSTERY", new string[0]);

            string emitted = EmitMusic(template);

            Assert.That(
                emitted,
                Does.Contain("DimensionMusicOverrideRegistry.SetRoster(\"test.dimension\", \"MYSTERY\")"),
                "SetRoster had no caller at all, so every dimension played the default dungeon " +
                "music whatever its author wanted.");
            Assert.That(
                emitted,
                Does.Not.Contain("RegisterCue"),
                "One of the game's rosters brings its own tracks.");
        }

        [Test]
        public void ADimensionWithMusicOfItsOwn_RegistersTheCueBeforeChoosingIt()
        {
            DimensionTemplateAsset template =
                TemplateWithMusic("DeepHum", new[] { "mod/deep_hum_a", "mod/deep_hum_b" });

            string emitted = EmitMusic(template);

            int cue = emitted.IndexOf("RegisterCue", System.StringComparison.Ordinal);
            int set = emitted.IndexOf("SetRoster", System.StringComparison.Ordinal);
            Assert.That(cue, Is.GreaterThanOrEqualTo(0), "The mod's own tracks have to become a roster.");
            Assert.That(
                set,
                Is.GreaterThan(cue),
                "SetRoster asks whether the name is a known cue, so the cue has to exist first or " +
                "the dimension falls back to the default with a warning.");
            Assert.That(emitted, Does.Contain("\"mod/deep_hum_a\""));
            Assert.That(emitted, Does.Contain("\"mod/deep_hum_b\""));
        }

        [Test]
        public void AMusicNameThatIsNeitherKind_IsRefusedRatherThanRegistered()
        {
            DimensionTemplateAsset template = TemplateWithMusic("SomethingIMadeUp", new string[0]);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SomethingIMadeUp"));

            Assert.That(
                EmitMusic(template),
                Is.Empty,
                "A name that is neither one of the game's rosters nor backed by tracks resolves " +
                "to nothing, and registering it would put a warning in the player's log every " +
                "session instead of in the author's console once.");
        }

        [Test]
        public void ADimensionWithNoMusicNamed_LeavesTheGroundToDecide()
        {
            Assert.That(
                EmitMusic(TemplateWithMusic(string.Empty, new string[0])),
                Is.Empty,
                "An empty field means the author has said nothing, and the game's own biome music " +
                "is a better answer than any default this framework could invent.");
        }

        // ------------------------------------------------------------------ helpers ---

        private static string EmitRecipes(DimensionTemplateAsset template)
        {
            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendRecipeCraftingRegistrations(
                builder, template, "TestMod");
            return builder.ToString();
        }

        private static string EmitMusic(DimensionTemplateAsset template)
        {
            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendDimensionMusicRegistrations(
                builder, template);
            return builder.ToString();
        }

        private DimensionTemplateAsset TemplateWithWorkbench(
            string workbenchId,
            DimensionCraftingWindowLook look)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionWorkbenchAsset workbench = Make<DimensionWorkbenchAsset>();
            SetString(workbench, "workbenchId", workbenchId);
            SetString(workbench, "displayName", workbenchId);
            SetBool(workbench, "enabled", true);
            SetBool(workbench, "generatesItsOwnObject", true);
            SetEnum(workbench, "interaction.craftingWindowLook", (int)look);
            template.SetGlobalWorkbenches(new[] { workbench });
            return template;
        }

        private DimensionTemplateAsset TemplateWithMusic(string musicName, string[] tracks)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SetString(template, "dimensionId", "test.dimension");
            SetString(template, "music", musicName);

            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty list = serialized.FindProperty("musicTracks");
            Assert.That(list, Is.Not.Null, "musicTracks missing on the dimension asset.");
            list.arraySize = tracks.Length;
            for (int i = 0; i < tracks.Length; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = tracks[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetString(Object asset, string field, string value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field + " missing on " + asset.GetType().Name);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object asset, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field + " missing on " + asset.GetType().Name);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object asset, string path, int value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
