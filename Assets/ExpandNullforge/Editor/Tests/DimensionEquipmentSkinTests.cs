using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the paperdoll: what a worn item looks like on the character.
    /// </summary>
    /// <remarks>
    /// The failure worth guarding hardest is that all of this is invisible from the inspector. An
    /// armour piece with no skin equips, grants its stats, sits in the inventory and leaves the
    /// character on screen bare — nothing errors, nothing is missing, it simply is not drawn. The
    /// second is the address: left unstamped every generated skin in a mod shares the all-zero
    /// address and several custom helmets all draw as whichever one loaded first.
    /// </remarks>
    public sealed class DimensionEquipmentSkinTests
    {
        private const string TestRoot = "Assets/NullforgeSkinTests";

        private DimensionItemAsset item;
        private Texture2D sheet;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeSkinTests");
            }

            // Held in memory rather than written as an asset. A SerializedObject write made right
            // after AssetDatabase.CreateAsset does not stick - the first tests in this class ran
            // against an item still carrying its defaults, so nothing generated and the failure
            // read as a skin bug rather than a setup one.
            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            Set("itemId", "testarmor");
            Set("displayName", "Test Armour");

            // Every item needs something to look like in the inventory or generation refuses it,
            // which would make these tests pass for the wrong reason: nothing generated at all.
            Set("iconId", "1");

            sheet = new Texture2D(
                DimensionEquipmentSkinTemplate.SheetWidth,
                DimensionEquipmentSkinTemplate.SheetHeight);
            System.IO.File.WriteAllBytes(
                System.IO.Path.GetFullPath(TestRoot + "/sheet.png"),
                sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(TestRoot + "/sheet.png", ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void Cleanup()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (sheet != null)
            {
                Object.DestroyImmediate(sheet);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetSkin(DimensionEquipmentSkinSlot slot, bool withArt)
        {
            SerializedObject serialized = new SerializedObject(item);
            SerializedProperty skin = serialized.FindProperty("equipmentSkin");
            skin.FindPropertyRelative("slot").intValue = (int)slot;
            skin.FindPropertyRelative("sheet").objectReferenceValue = withArt
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(TestRoot + "/sheet.png")
                : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport Run()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private static ScriptableDataBlock LoadSkin()
        {
            return AssetDatabase.LoadAssetAtPath<ScriptableDataBlock>(SkinPath);
        }

        private static string SkinPath
        {
            get
            {
                return TestRoot + "/" + DimensionEquipmentSkinGenerator.FolderName +
                    "/testarmorSkin.asset";
            }
        }

        private static string Diagnose(DimensionItemGenerationReport report)
        {
            return "folder=" + AssetDatabase.IsValidFolder(
                       TestRoot + "/" + DimensionEquipmentSkinGenerator.FolderName) +
                " onDisk=" + System.IO.File.Exists(System.IO.Path.GetFullPath(SkinPath)) +
                " created=" + string.Join(",", report.Created.ToArray()) +
                " skipped=" + string.Join(",", report.Skipped.ToArray()) +
                " errors=" + string.Join(",", report.Errors.ToArray()) +
                " warnings=" + string.Join(" | ", report.Warnings.ToArray());
        }

        [Test]
        public void EachBodyPartGetsTheDataBlockTheGameReadsForIt()
        {
            SetSkin(DimensionEquipmentSkinSlot.Head, true);
            DimensionItemGenerationReport first = Run();
            Assert.IsInstanceOf<HelmSkinDataBlock>(LoadSkin(), Diagnose(first));

            SetSkin(DimensionEquipmentSkinSlot.Chest, true);
            Run();
            Assert.IsInstanceOf<BreastArmorSkinDataBlock>(
                LoadSkin(),
                "changing which body part it goes on has to replace the block, not keep the old type");

            SetSkin(DimensionEquipmentSkinSlot.Legs, true);
            Run();
            Assert.IsInstanceOf<PantsArmorSkinDataBlock>(LoadSkin());
        }

        [Test]
        public void TheItemPointsAtItsOwnSkinAndNotAtNothing()
        {
            // The address is the whole mechanism: EquipmentSkinAuthoring holds nothing else.
            SetSkin(DimensionEquipmentSkinSlot.Head, true);
            Run();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testarmor.prefab");
            EquipmentSkinAuthoring authoring = prefab.GetComponent<EquipmentSkinAuthoring>();
            Assert.IsNotNull(authoring);
            Assert.IsTrue(authoring.skinRef.hasAddress, "an all-zero address is no address at all");
            Assert.AreEqual(LoadSkin().address, authoring.skinRef.address);
        }

        [Test]
        public void AnItemThatIsNotWornCarriesNoSkinAtAll()
        {
            // Generation is authoritative: an item switched back to not-worn must not keep either
            // the component or the data block.
            SetSkin(DimensionEquipmentSkinSlot.Head, true);
            Run();
            Assert.IsNotNull(LoadSkin());

            SetSkin(DimensionEquipmentSkinSlot.NotWorn, true);
            Run();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testarmor.prefab");
            Assert.IsNull(prefab.GetComponent<EquipmentSkinAuthoring>());
            Assert.IsNull(LoadSkin(), "the mod should not ship art nothing points at");
        }

        [Test]
        public void WornWithNoArtIsReportedRatherThanShippedInvisible()
        {
            SetSkin(DimensionEquipmentSkinSlot.Chest, false);

            DimensionItemGenerationReport report = Run();

            Assert.IsTrue(
                report.Warnings.Exists(warning => warning.Contains("no character sheet")),
                "the one failure a player sees and the inspector does not");
            Assert.IsNull(LoadSkin());
        }

        [Test]
        public void TheQuietFailuresAreDetectableWithoutGenerating()
        {
            DimensionEquipmentSkinTemplate template = new DimensionEquipmentSkinTemplate();
            Assert.IsFalse(template.IsWorn);
            Assert.IsFalse(template.WornButInvisible, "not worn is not a failure");
            Assert.IsFalse(template.ShowsOnTheCharacter);
        }

        [Test]
        public void HeadOnlyAnswersDoNotLeakOntoOtherBodyParts()
        {
            // A chest piece cannot hide hair, so asking is meaningless and reading it back as
            // anything but hidden would write a value the game reads for a slot that has none.
            DimensionEquipmentSkinTemplate template = new DimensionEquipmentSkinTemplate();
            SerializedObject holder = new SerializedObject(item);
            SerializedProperty skin = holder.FindProperty("equipmentSkin");
            skin.FindPropertyRelative("slot").intValue = (int)DimensionEquipmentSkinSlot.Chest;
            skin.FindPropertyRelative("hairUnderIt").intValue = (int)DimensionHairUnderHelm.FullyShown;
            skin.FindPropertyRelative("nudge").vector2IntValue = new Vector2Int(3, 4);
            holder.ApplyModifiedPropertiesWithoutUndo();

            template = item.EquipmentSkin;
            Assert.AreEqual(DimensionHairUnderHelm.Hidden, template.HairUnderIt);
            Assert.AreEqual(Vector2Int.zero, template.Nudge);
        }
    }
}
