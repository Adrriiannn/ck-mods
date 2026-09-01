using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// The light a placed object throws on the floor around it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT MAKES THIS DOMAIN ODD IS THAT THERE IS NO QUERY TO SATISFY. A placed light is not an ECS
    /// component and no system asks for one: it is a subtree of nodes on the graphical prefab, read
    /// by <c>PugLight</c>, <c>ManagedLight</c> and <c>LightFlickerEffect</c>, which are
    /// MonoBehaviours. So the thing that can be wrong is the SHAPE of that subtree, and the first
    /// test below is the shape written out as the three contracts the game's own code dereferences
    /// without a null check.
    /// </para>
    /// <para>
    /// AND THE SECOND THING THAT CAN BE WRONG IS THE FILE. The visual prefab is rebuilt from a new
    /// <c>GameObject</c> on every generate and saved over the old one, so a light added anywhere
    /// except inside that build is a light that survives exactly one pass. Generating twice and
    /// counting is what catches that, and it is the reason that test exists at all.
    /// </para>
    /// </remarks>
    public sealed class DimensionEmittedLightTests
    {
        private const string TestRoot = "Assets/NullforgeEmittedLightTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeEmittedLightTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SetString("objectIdentifier", "testlight");
            SetString("displayName", "Test Light");
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        /// <summary>Writes one answer onto the asset the way the inspector writes it.</summary>
        /// <remarks>
        /// <c>ApplyModifiedProperties</c> rather than the WithoutUndo variant, and the four setters
        /// below all use it: it is the call the inspector makes, and it is what runs the asset's
        /// <c>OnValidate</c> — which is where choosing one of the game's named lights fills the rest
        /// of the fold in. Writing the field by any other route would take the thing an author
        /// actually does out of the test.
        /// </remarks>
        private void SetString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            // intValue, not enumValueIndex — the same reason the container tests give: the index is
            // a position in the drawn list, and the value is what the field holds.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Says what using the object does, the way the inspector says it.</summary>
        /// <remarks>
        /// This is the answer the whole pooling problem hangs off: the moment it is anything but
        /// Nothing, the prefab a player walks up to carries one of the framework's views and is
        /// shared with every other object of that use.
        /// </remarks>
        private void SetUse(DimensionUseBehaviour use)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized
                .FindProperty("interaction")
                .FindPropertyRelative("whatUsingItDoes")
                .intValue = (int)use;
            serialized.ApplyModifiedProperties();
        }

        private DimensionWorldObjectGenerationReport Run()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        /// <summary>The prefab a player walks up to, which is where a placed light lives.</summary>
        private static GameObject LoadVisual()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testlightVisual.prefab");
        }

        private static ManagedLight[] EveryLightOn(GameObject prefab)
        {
            return prefab == null
                ? new ManagedLight[0]
                : prefab.GetComponentsInChildren<ManagedLight>(true);
        }

        /// <summary>Whether a node carries a component of this type name.</summary>
        private static bool CarriesAComponentCalled(GameObject node, string typeName)
        {
            Component[] components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- the shape the game dereferences ----

        /// <summary>
        /// Every node the game reads without checking it is there.
        /// </summary>
        /// <remarks>
        /// Four separate unguarded dereferences, each one a crash rather than a dark object:
        /// <c>ManagedLight.Awake</c> takes the <c>PugLight</c> off the light it was given and
        /// <c>SetOptimized</c> reads <c>.quality</c> off it every optimisation pass;
        /// <c>SetOptimized</c> also reads <c>lightContainer.activeInHierarchy</c>;
        /// <c>PugLight</c> is <c>[RequireComponent(typeof(Light))]</c> and calls
        /// <c>GetComponent&lt;Light&gt;</c> on itself; and <c>LightFlickerEffect.OnEnable</c> hands
        /// its light straight to the light manager.
        /// </remarks>
        [Test]
        public void AGeneratedLightHasEveryNodeTheGameNeeds()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();

            GameObject visual = LoadVisual();
            Assert.IsNotNull(visual, "The prefab a player walks up to was not written.");

            ManagedLight managed = visual.GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed, "No ManagedLight — nothing would ever be lit.");
            Assert.IsNotNull(
                managed.lightContainer,
                "ManagedLight.SetOptimized reads lightContainer.activeInHierarchy with no check.");
            Assert.IsNotNull(
                managed.lightToOptimize,
                "ManagedLight.Awake asks lightToOptimize for its PugLight with no check.");
            Assert.IsNotNull(
                managed.fallbackRenderer,
                "The sprite the light is swapped for when the game dims it.");
            // Asked for by name rather than by type: PugLight lives in the render pipeline's own
            // assembly, which this test assembly deliberately does not reference. The contract
            // being checked is the same one — ManagedLight.Awake takes the PugLight off the light
            // it was given, and SetOptimized reads .quality off it with no null check.
            Assert.IsTrue(
                CarriesAComponentCalled(managed.lightToOptimize.gameObject, "PugLight"),
                "A Light with no PugLight beside it makes SetOptimized a null reference.");

            LightFlickerEffect flicker = visual.GetComponentInChildren<LightFlickerEffect>(true);
            Assert.IsNotNull(flicker, "The flicker, which every light in the game carries.");
            Assert.AreSame(
                managed.lightToOptimize,
                flicker.flickeringLight,
                "LightFlickerEffect.AddLightFlicker gives up on a null light and says nothing.");
        }

        /// <summary>
        /// The light hangs off the root, beside the child that flips the art.
        /// </summary>
        /// <remarks>
        /// Measured, not chosen: <c>Torch.prefab</c>'s root has exactly two children, <c>XScaler</c>
        /// and <c>LightOptimizer</c>, side by side, and the other five lit prefabs match. Under the
        /// scaler instead, the object's light would slide across the tile every time it turned to
        /// face the other way.
        /// </remarks>
        [Test]
        public void TheLightHangsOffTheRootAndNotOffTheThingThatFlipsTheArt()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();

            GameObject visual = LoadVisual();
            ManagedLight managed = visual.GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed);
            Assert.AreSame(
                visual.transform,
                managed.transform.parent,
                "The light must be a sibling of XScaler, the way the game's own torch has it.");
        }

        [Test]
        public void TheLightSitsAtTheHeightTheAuthorAskedFor()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetFloat("emittedLight.howHighAboveTheFloor", 2.2f);
            Run();

            ManagedLight managed = LoadVisual().GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed);
            Assert.AreEqual(2.2f, managed.transform.localPosition.y, 0.001f);
        }

        [Test]
        public void TheColourReachAndShadowsAreTheOnesTheAuthorAskedFor()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", false);
            SetBool("emittedLight.itCastsShadows", false);
            SetFloat("emittedLight.howFarItReaches", 9f);
            SetFloat("emittedLight.howBright", 0.4f);
            Run();

            ManagedLight managed = LoadVisual().GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed);
            Assert.AreEqual(9f, managed.lightToOptimize.range, 0.001f);
            Assert.AreEqual(0.4f, managed.lightToOptimize.intensity, 0.001f);
            Assert.AreEqual(LightShadows.None, managed.lightToOptimize.shadows);
        }

        /// <summary>
        /// A steady light is a flicker with both ends at the same brightness.
        /// </summary>
        /// <remarks>
        /// The component is never torn out of the cloned subtree, because tearing a component out
        /// of a copy of a vanilla subtree is how the copy stops matching vanilla. Equal bounds is
        /// how the game itself makes a light hold still, and it is what the dimension portal has
        /// always done.
        /// </remarks>
        [Test]
        public void ALightThatDoesNotFlickerIsSteadyRatherThanMissingItsFlicker()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", false);
            SetFloat("emittedLight.howBright", 0.5f);
            Run();

            GameObject visual = LoadVisual();
            LightFlickerEffect flicker = visual.GetComponentInChildren<LightFlickerEffect>(true);
            Assert.IsNotNull(flicker, "The component stays; only its bounds close up.");
            Assert.AreEqual(0.5f, flicker.minIntensity, 0.001f);
            Assert.AreEqual(0.5f, flicker.maxIntensity, 0.001f);
            Assert.IsFalse(flicker.enableMovement, "A steady light does not wander either.");
        }

        /// <summary>
        /// A flickering light is baked at the brightness the game is going to settle on.
        /// </summary>
        /// <remarks>
        /// <c>LightFlickerEffect.Awake</c> assigns
        /// <c>flickeringLight.intensity = (minIntensity + maxIntensity) * 0.5f</c> before anything
        /// reads it, so any other value baked here would be a number in the prefab that the game
        /// throws away on the first frame. The portal's own visual profile already answers the
        /// brightness question this way and says so in its remark; this is the same rule for a
        /// placed object.
        /// </remarks>
        [Test]
        public void AFlickeringLightIsBakedAtTheBrightnessTheGameWillSettleOn()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", true);
            SetFloat("emittedLight.atItsDimmest", 0.4f);
            SetFloat("emittedLight.atItsBrightest", 0.8f);
            Run();

            ManagedLight managed = LoadVisual().GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed);
            Assert.AreEqual(0.6f, managed.lightToOptimize.intensity, 0.001f);

            LightFlickerEffect flicker = LoadVisual().GetComponentInChildren<LightFlickerEffect>(true);
            Assert.AreEqual(0.4f, flicker.minIntensity, 0.001f);
            Assert.AreEqual(0.8f, flicker.maxIntensity, 0.001f);
        }

        // ---- the two ways it could ship dead ----

        /// <summary>
        /// With the light off, there is no light anywhere on an object nothing uses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The subject here is deliberately an object with no use, because that prefab is
        /// instantiated once per entity rather than pooled — so what is baked is what shows, and
        /// the honest shape for "no light" is no nodes — and when the light was the only reason the
        /// prefab existed, no prefab at all, which is stronger and is what the generator does.
        /// </para>
        /// <para>
        /// A USABLE OBJECT ANSWERS THE SAME QUESTION DIFFERENTLY, and
        /// <see cref="AUsableObjectWithNoLightKeepsTheNodeWithItsLightSwitchedOff"/> is that case.
        /// Its prefab is shared by every object of its use, so taking the nodes away would take
        /// away the only place a lit sibling has to put its light.
        /// </para>
        /// </remarks>
        [Test]
        public void TurningTheLightOffLeavesNoLightAtAllOnAnObjectNothingUses()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();
            Assert.AreEqual(1, EveryLightOn(LoadVisual()).Length, "It was on for this pass.");

            SetBool("emittedLight.givesOffLight", false);
            Run();

            // TWO SHAPES BOTH SATISFY THIS, and the stronger one is no file. An object with no
            // picture, no use and no light has nothing left to draw, so the generator takes the
            // prefab away rather than leaving an empty one — which is also what stops it lighting
            // the floor from an earlier build. An empty prefab passes too, for a caller that keeps
            // one for another reason.
            GameObject visual = LoadVisual();
            if (visual == null)
            {
                Assert.Pass("Nothing is drawn for it at all, so there is nowhere for a light to be.");
            }

            Assert.AreEqual(0, EveryLightOn(visual).Length, "No ManagedLight is left behind.");
            Assert.AreEqual(
                0,
                visual.GetComponentsInChildren<Light>(true).Length,
                "And no bare Light either.");
        }

        /// <summary>
        /// Generating twice leaves one light, not two.
        /// </summary>
        /// <remarks>
        /// This is the clobber guard. The visual prefab is written from scratch every pass, so the
        /// count can only grow if the light were ever added to the SAVED file instead of to the
        /// object being built — which is the single most likely way this feature ships broken.
        /// </remarks>
        [Test]
        public void GeneratingTwiceLeavesExactlyOneLight()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();
            Run();

            Assert.AreEqual(1, EveryLightOn(LoadVisual()).Length);
        }

        /// <summary>
        /// An object with no picture and no use still gets its light.
        /// </summary>
        /// <remarks>
        /// The light lives in the prefab a player walks up to, and that prefab used to be written
        /// only for an object that had a picture or something that used it. A lit alcove with no
        /// art of its own would have had its light dropped along with the file.
        /// </remarks>
        [Test]
        public void AnObjectWithNoPictureStillGetsItsLight()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();

            Assert.IsNotNull(LoadVisual(), "The prefab has to be written for the light to live in.");
            Assert.AreEqual(1, EveryLightOn(LoadVisual()).Length);
        }

        // ---- what the author is told ----

        [Test]
        public void ALightThatReachesNothingIsReported()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetFloat("emittedLight.howFarItReaches", 0f);

            Assert.IsTrue(worldObject.EmittedLight.ItLightsNothingBecauseItReachesNothing);
            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void FlickerBoundsTheWrongWayRoundAreReported()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", true);
            SetFloat("emittedLight.atItsDimmest", 0.9f);
            SetFloat("emittedLight.atItsBrightest", 0.2f);

            Assert.IsTrue(worldObject.EmittedLight.ItsDimmestIsBrighterThanItsBrightest);
            Assert.IsNotEmpty(Run().Warnings);
        }

        /// <summary>
        /// A real light silences the "glows but lights nothing" warning.
        /// </summary>
        /// <remarks>
        /// That warning was written when nothing a modder made could light anything at all. Left
        /// alone, it would now tell an author who answered the light question honestly that their
        /// lamp lights nothing.
        /// </remarks>
        [Test]
        public void AGlowOnTopOfARealLightIsNotCalledALightThatLightsNothing()
        {
            SetBool("objectItselfGlows", true);
            Assert.IsTrue(worldObject.GlowsButLightsNothing, "With no light, it still is one.");

            SetBool("emittedLight.givesOffLight", true);
            Assert.IsFalse(worldObject.GlowsButLightsNothing);
        }

        // ---- the named lights ----

        /// <summary>
        /// Choosing one of the game's lights fills the numbers in, once.
        /// </summary>
        /// <remarks>
        /// Both halves matter. Filling nothing in makes the dropdown decoration; filling it in on
        /// every repaint takes the author's own numbers away again the moment they type one.
        /// </remarks>
        [Test]
        public void ChoosingANamedLightFillsTheNumbersInAndThenLeavesThemAlone()
        {
            SetEnum("emittedLight.lightLike", (int)DimensionLightLike.Campfire);

            DimensionEmittedLightTemplate light = worldObject.EmittedLight;
            Assert.AreEqual(7f, light.HowFarItReaches, 0.001f, "Campfire.prefab:434");
            Assert.AreEqual(0.6f, light.AtItsDimmest, 0.001f, "Campfire.prefab:500");
            Assert.AreEqual(0.7f, light.AtItsBrightest, 0.001f, "Campfire.prefab:501");

            SetFloat("emittedLight.howFarItReaches", 3f);
            Assert.AreEqual(
                3f,
                worldObject.EmittedLight.HowFarItReaches,
                0.001f,
                "The preset must not be copied back over an answer the author typed.");
        }

        [Test]
        public void PickingCustomChangesNothing()
        {
            SetFloat("emittedLight.howFarItReaches", 3f);
            SetEnum("emittedLight.lightLike", (int)DimensionLightLike.Custom);

            Assert.AreEqual(3f, worldObject.EmittedLight.HowFarItReaches, 0.001f);
        }

        /// <summary>
        /// Every named light carries the numbers measured off its own prefab.
        /// </summary>
        /// <remarks>
        /// The expectations here are the second copy of those measurements, written down separately
        /// from the table they check, so that a typo in either one is a failure rather than a
        /// silent agreement. Each assertion names the prefab it came from: the day Pugstorm retunes
        /// a torch, this is what says so.
        /// </remarks>
        [Test]
        public void EveryNamedLightCarriesTheNumbersMeasuredOffItsPrefab()
        {
            DimensionLightLike[] named = DimensionEmittedLightTemplate.EveryNamedLight();
            Assert.IsNotEmpty(named, "There is nothing to check, which is a failure and not a pass.");
            Assert.AreEqual(6, named.Length, "Six lights were measured.");

            foreach (DimensionLightLike light in named)
            {
                DimensionEmittedLightTemplate.Preset preset =
                    DimensionEmittedLightTemplate.PresetFor(light);
                Assert.Greater(preset.Range, 0f, light + " reaches nowhere.");
                Assert.Greater(
                    preset.HeightAboveTheFloor,
                    0f,
                    light + " would sit in the floor.");
                Assert.LessOrEqual(
                    preset.Dimmest,
                    preset.Brightest,
                    light + " flickers between bounds the wrong way round.");
            }

            AssertPreset(
                DimensionLightLike.Torch, 1f, 0.93333334f, 0.8f, 0.65f, 5f, 0.5f, 0.65f, true,
                0.75f, 0f,
                "Torch.prefab:873-875, :941-943, :808");
            AssertPreset(
                DimensionLightLike.Campfire, 1f, 0.8352941f, 0.5254902f, 0.65f, 7f, 0.6f, 0.7f, true,
                0.75f, 0f,
                "Campfire.prefab:432-434, :500-502, :367");
            AssertPreset(
                DimensionLightLike.Lamp, 1f, 1f, 1f, 0.75f, 7f, 0.7f, 0.8f, false,
                0.75f, 0f,
                "Lamp.prefab:664-666, :732-734, :599");
            AssertPreset(
                DimensionLightLike.PaperLantern, 1f, 1f, 1f, 0.75f, 6f, 0.7f, 0.8f, false,
                0.75f, -0.3125f,
                "ChineseLantern.prefab:374-376, :442-444, :309");
            AssertPreset(
                DimensionLightLike.CrystalLamp, 0.5254902f, 0.81942016f, 1f, 0.65f, 6f, 0.6f, 0.7f, true,
                2.7f, -0.15f,
                "CrystalLamp.prefab:7831-7833, :7899-7901, :7766");
            AssertPreset(
                DimensionLightLike.LampPost, 0.8537736f, 0.99561495f, 1f, 0.65f, 6f, 0.6f, 0.7f, true,
                2.2f, -0.4f,
                "LampPost.prefab:436-438, :504-506, :371");
        }

        /// <summary>
        /// A named light copies where the game hangs it, not only how bright it is.
        /// </summary>
        /// <remarks>
        /// Three of the six sit off the middle of their tile because their lamp head hangs out over
        /// the edge. A preset that copied the height and dropped this would put "Light like: paper
        /// lantern" a third of a tile away from the paper lantern's own light, which is not what
        /// the control says it does.
        /// </remarks>
        [Test]
        public void ThePresetsThatSitOffCentreCopyThatToo()
        {
            DimensionLightLike[] named = DimensionEmittedLightTemplate.EveryNamedLight();
            Assert.IsNotEmpty(named, "There is nothing to check, which is a failure and not a pass.");

            int offCentre = 0;
            foreach (DimensionLightLike light in named)
            {
                if (DimensionEmittedLightTemplate.PresetFor(light).FrontToBack != 0f)
                {
                    offCentre++;
                }
            }

            Assert.AreEqual(
                3,
                offCentre,
                "Measured: ChineseLantern.prefab:309 is -0.3125, CrystalLamp.prefab:7766 is -0.15 " +
                "and LampPost.prefab:371 is -0.4; Torch, Campfire and Lamp are all at 0.");

            SetEnum("emittedLight.lightLike", (int)DimensionLightLike.PaperLantern);
            Assert.AreEqual(
                -0.3125f,
                worldObject.EmittedLight.HowFarFrontToBack,
                0.0001f,
                "Picking a named light has to fill this in like every other number in the fold.");
        }

        private static void AssertPreset(
            DimensionLightLike light,
            float red,
            float green,
            float blue,
            float brightness,
            float range,
            float dimmest,
            float brightest,
            bool flameMoves,
            float height,
            float frontToBack,
            string measuredAt)
        {
            DimensionEmittedLightTemplate.Preset preset =
                DimensionEmittedLightTemplate.PresetFor(light);
            Assert.AreEqual(red, preset.Colour.r, 0.0001f, light + " red, " + measuredAt);
            Assert.AreEqual(green, preset.Colour.g, 0.0001f, light + " green, " + measuredAt);
            Assert.AreEqual(blue, preset.Colour.b, 0.0001f, light + " blue, " + measuredAt);
            Assert.AreEqual(brightness, preset.Brightness, 0.0001f, light + " brightness, " + measuredAt);
            Assert.AreEqual(range, preset.Range, 0.0001f, light + " range, " + measuredAt);
            Assert.AreEqual(dimmest, preset.Dimmest, 0.0001f, light + " dimmest, " + measuredAt);
            Assert.AreEqual(brightest, preset.Brightest, 0.0001f, light + " brightest, " + measuredAt);
            Assert.AreEqual(flameMoves, preset.FlameMoves, light + " flame movement, " + measuredAt);
            Assert.AreEqual(height, preset.HeightAboveTheFloor, 0.0001f, light + " height, " + measuredAt);
            Assert.AreEqual(
                frontToBack, preset.FrontToBack, 0.0001f, light + " front to back, " + measuredAt);
        }

        // ---- the builder the portal shares ----

        /// <summary>
        /// The builder the portal now calls still writes the portal's own numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The portal's ground light was measured at intensity 0.65, range 5, flicker 0.3 to 0.3
        /// with movement on and hard shadows — the values <c>EnsurePortalManagedLight</c> falls back
        /// to when no visual profile is set. Extracting its body into a shared builder was supposed
        /// to change nothing, and this drives the extracted builder with those numbers to say so.
        /// </para>
        /// <para>
        /// IT EXERCISES THE BUILDER, NOT THE PORTAL GENERATOR. The portal's own pass needs a whole
        /// generated portal to run; what moved was this method, so this is what is checked.
        /// </para>
        /// </remarks>
        [Test]
        public void TheBuilderThePortalSharesStillWritesThePortalsOwnNumbers()
        {
            GameObject host = new GameObject("PortalLightHost");
            try
            {
                ManagedLight managed = DimensionEmittedLightBuilder.Ensure(
                    host.transform,
                    DimensionEmittedLightBuilder.TemplateLightLocalPosition(),
                    Color.white,
                    0.65f,
                    5f,
                    true,
                    0.3f,
                    0.3f,
                    true,
                    true);

                Assert.IsNotNull(managed, "A light that is on comes back so the view can be told.");
                Assert.AreEqual(0.65f, managed.lightToOptimize.intensity, 0.001f);
                Assert.AreEqual(5f, managed.lightToOptimize.range, 0.001f);
                Assert.AreEqual(LightShadows.Hard, managed.lightToOptimize.shadows);

                LightFlickerEffect flicker = host.GetComponentInChildren<LightFlickerEffect>(true);
                Assert.IsNotNull(flicker);
                Assert.AreEqual(0.3f, flicker.minIntensity, 0.001f);
                Assert.AreEqual(0.3f, flicker.maxIntensity, 0.001f);
                Assert.IsTrue(flicker.enableMovement);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// A light that is switched off is left unassigned, on purpose.
        /// </summary>
        /// <remarks>
        /// <c>EntityMonoBehaviour</c> switches an assigned <c>optionalLightOptimizer</c> back on the
        /// moment the object is drawn, so returning the light anyway would turn every "no light"
        /// answer back into a light.
        /// </remarks>
        [Test]
        public void TheBuilderRefusesToHandBackALightThatIsSwitchedOff()
        {
            GameObject host = new GameObject("PortalLightHost");
            try
            {
                ManagedLight managed = DimensionEmittedLightBuilder.Ensure(
                    host.transform,
                    Vector3.zero,
                    Color.white,
                    0.65f,
                    5f,
                    true,
                    0.3f,
                    0.3f,
                    true,
                    false);

                Assert.IsNull(managed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Calling the builder twice on the same parent leaves one light.
        /// </summary>
        /// <remarks>
        /// The portal's prefab is reloaded and built on top of, unlike the world object's, which is
        /// made fresh. Without the destroy the builder starts with, every portal regenerate would
        /// stack another light on the same node.
        /// </remarks>
        [Test]
        public void CallingTheBuilderTwiceOnOneParentLeavesOneLight()
        {
            GameObject host = new GameObject("PortalLightHost");
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    DimensionEmittedLightBuilder.Ensure(
                        host.transform, Vector3.zero, Color.white, 0.65f, 5f, true, 0.3f, 0.3f, true, true);
                }

                Assert.AreEqual(1, host.GetComponentsInChildren<ManagedLight>(true).Length);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---- reachable from the studio ----

        /// <summary>
        /// The light block is drawn on both pages an author can reach an object from.
        /// </summary>
        /// <remarks>
        /// A control nothing draws is the same as a control that does not exist, and this framework
        /// has two editors over the same assets: the panel list and the stage pages. A block named
        /// by neither falls into the catch-all fold with no label and no explanation.
        /// </remarks>
        [Test]
        public void TheLightBlockIsDrawnOnBothPagesThatEditAnObject()
        {
            string window =
                DimensionFrameworkSourceScanner.ReadByName("DimensionFrameworkAuthoringWindow.cs");
            string catalog =
                DimensionFrameworkSourceScanner.ReadByName("DimensionStageCatalog.cs");

            Assert.IsNotEmpty(window, "The panel source was not found, so nothing was checked.");
            Assert.IsNotEmpty(catalog, "The stage catalog source was not found.");

            StringAssert.Contains("Field(\"emittedLight\"", window);
            StringAssert.Contains("F(\"emittedLight\"", catalog);
        }

        // ---- a light on a usable object is that object's own light ----

        /// <summary>
        /// A lit object that can be used hands its light to the view that draws it.
        /// </summary>
        /// <remarks>
        /// <c>EntityMonoBehaviour</c> hides the light while the object is dying and switches it
        /// back on when the object is drawn, and both go through <c>optionalLightOptimizer</c>
        /// (<c>:591</c>, <c>:625</c>, <c>:1117</c>). It is also the only handle the view has on the
        /// light when it re-derives it per entity. Until this test existed the line that assigns it
        /// only ran for a usable object and no test made an object usable.
        /// </remarks>
        [Test]
        public void ALitObjectThatCanBeUsedHandsItsLightToTheViewThatDrawsIt()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetUse(DimensionUseBehaviour.OpensLikeAChest);
            Run();

            GameObject visual = LoadVisual();
            Assert.IsNotNull(visual, "The prefab a player walks up to was not written.");

            ManagedLight managed = visual.GetComponentInChildren<ManagedLight>(true);
            Assert.IsNotNull(managed, "A lit object that can be used still needs its light.");

            EntityMonoBehaviour view = visual.GetComponent<EntityMonoBehaviour>();
            Assert.IsNotNull(view, "A usable object is drawn by one of the framework's views.");
            Assert.AreSame(
                managed,
                view.optionalLightOptimizer,
                "Unassigned, the object comes back from a death animation dark and the view has " +
                "nothing to re-derive the light onto.");
        }

        /// <summary>
        /// A usable object with no light keeps the light node, switched off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS THE TEST THAT FAILS IF THE LIGHT IS EVER BAKED ONTO A SHARED VIEW AGAIN. The
        /// prefab a usable object walks around in is POOLED BY COMPONENT TYPE — only the first
        /// prefab of a type is ever instantiated (<c>MemoryManager.CreateModdedPrefabPool</c> ends
        /// in <c>_poolFromComponentType.TryAdd</c>) — so if the node were built only on the lit
        /// object's prefab, a modder would get either a dark forge or every bench in their mod
        /// glowing with the forge's colour, depending on which prefab registered first. Every
        /// pooled prefab carries the node so that whichever one wins has somewhere to put whatever
        /// light the object being drawn asked for.
        /// </para>
        /// <para>
        /// AND IT IS SWITCHED OFF AT THE GAMEOBJECT, not at the component flag.
        /// <c>ManagedLight.OnEnable</c> assigns <c>lightToOptimize.enabled = true</c> whenever its
        /// node is activated, which <c>EntityMonoBehaviour.OnOccupied</c> does on every occupy, so
        /// the flag alone would not hold.
        /// </para>
        /// </remarks>
        [Test]
        public void AUsableObjectWithNoLightKeepsTheNodeWithItsLightSwitchedOff()
        {
            SetBool("emittedLight.givesOffLight", false);
            SetUse(DimensionUseBehaviour.OpensLikeAChest);
            Run();

            GameObject visual = LoadVisual();
            Assert.IsNotNull(visual);

            ManagedLight[] lights = EveryLightOn(visual);
            Assert.AreEqual(
                1,
                lights.Length,
                "Without the node, a lit object sharing this pool has nowhere to be lit.");

            Assert.IsNotNull(lights[0].lightToOptimize);
            Assert.IsFalse(
                lights[0].lightToOptimize.gameObject.activeSelf,
                "An object that asked for no light must not light anything.");
            Assert.IsFalse(lights[0].lightToOptimize.enabled);

            Assert.AreSame(
                lights[0],
                visual.GetComponent<EntityMonoBehaviour>().optionalLightOptimizer,
                "The field is what a LIT object sharing this prefab re-derives its light onto.");
        }

        /// <summary>
        /// Every use the framework builds is drawn by a view that re-derives the light per object.
        /// </summary>
        /// <remarks>
        /// The interface is the promise, and a view added later without it would silently give
        /// every object of its behaviour whichever light was written first. The generator reports
        /// exactly that case, so this is the check that the report stays unreachable.
        /// </remarks>
        [Test]
        public void EveryUseTheFrameworkBuildsRederivesItsLightPerObject()
        {
            DimensionUseBehaviour[] uses =
            {
                DimensionUseBehaviour.OpensLikeAChest,
                DimensionUseBehaviour.OpensACraftingBench,
                DimensionUseBehaviour.TendedLikeAnAnimal,
                DimensionUseBehaviour.TalkedToLikeAnNpc,
                DimensionUseBehaviour.ReadLikeASign,
                DimensionUseBehaviour.SellsLikeAShop
            };

            Assert.IsNotEmpty(uses, "Nothing was checked, which is a failure and not a pass.");
            Assert.AreEqual(
                uses.Length + 1,
                System.Enum.GetValues(typeof(DimensionUseBehaviour)).Length,
                "A use was added to the list an author picks from without being added here. " +
                "The extra one is Nothing, which builds no view at all.");

            for (int i = 0; i < uses.Length; i++)
            {
                SetBool("emittedLight.givesOffLight", true);
                SetUse(uses[i]);
                Run();

                GameObject visual = LoadVisual();
                Assert.IsNotNull(visual, uses[i] + " built no prefab to walk up to.");

                EntityMonoBehaviour view = visual.GetComponent<EntityMonoBehaviour>();
                Assert.IsNotNull(view, uses[i] + " is drawn by nothing.");
                Assert.IsInstanceOf<ExpandNullforge.Objects.IDimensionAuthoredLight>(
                    view,
                    uses[i] + " shares one pooled instance with every other object of its use, so " +
                    "a light baked on its prefab would be every one of their lights. It has to " +
                    "re-derive the light per object.");
            }
        }

        /// <summary>
        /// Each view re-derives the light from inside its own occupy.
        /// </summary>
        /// <remarks>
        /// Carrying the interface is not the same as calling it. The occupy is where a pooled
        /// instance learns which object it is drawing this time, so it is the only place the light
        /// can be re-derived; a view that implemented the method and never called it would look
        /// correct to every other test here. Anchored to the method body rather than the file, so
        /// splitting or moving a view changes nothing.
        /// </remarks>
        [Test]
        public void EveryViewRederivesTheLightFromInsideItsOwnOccupy()
        {
            string[] views =
            {
                "DimensionContainerView.cs",
                "DimensionCraftingBenchView.cs",
                "DimensionCattleView.cs",
                "DimensionNpcView.cs",
                "DimensionSignView.cs",
                "DimensionShopView.cs"
            };

            Assert.IsNotEmpty(views, "Nothing was checked, which is a failure and not a pass.");

            for (int i = 0; i < views.Length; i++)
            {
                string source = DimensionFrameworkSourceScanner.ReadByName(views[i]);
                Assert.IsNotEmpty(source, views[i] + " was found but read as nothing.");

                string occupied = DimensionFrameworkSourceScanner.BodyOfMethodIn(
                    source, "OnOccupied");
                Assert.IsNotNull(occupied, views[i] + " no longer has an OnOccupied to check.");
                StringAssert.Contains(
                    "PointTheLight()",
                    occupied,
                    views[i] + " does not re-derive its light where it learns which object it is " +
                    "drawing, so every object of its use would share one light.");

                StringAssert.Contains(
                    "DimensionAuthoredLight.Point(optionalLightOptimizer",
                    source,
                    views[i] + " re-derives nothing: the light the game switches on and off is " +
                    "the one in optionalLightOptimizer.");
            }
        }

        /// <summary>
        /// A light handed to an object that never asked for one goes out.
        /// </summary>
        /// <remarks>
        /// This is the container half of the same problem. The container and workbench generators
        /// never ask the light question, so their objects register nothing — and a chest handed the
        /// pooled instance a lit brazier was using a moment ago must not keep the brazier's light.
        /// Driven through <c>Point</c>, which is the call every view makes.
        /// </remarks>
        [Test]
        public void ALightHandedToAnObjectThatNeverAskedForOneGoesOut()
        {
            GameObject host = new GameObject("SharedViewStandIn");
            try
            {
                ManagedLight managed = DimensionEmittedLightBuilder.Ensure(
                    host.transform, Vector3.zero, Color.white, 0.65f, 5f, true, 0.5f, 0.65f, true,
                    true);
                Assert.IsNotNull(managed);

                ExpandNullforge.Objects.DimensionAuthoredLight.Show(
                    managed,
                    new ExpandNullforge.Objects.DimensionEmittedLightNumbers(
                        Color.red, 0.6f, 4f, true, 0.5f, 0.7f, true, 1.5f, -0.25f));
                Assert.IsTrue(
                    managed.lightToOptimize.gameObject.activeSelf,
                    "The brazier's own light, before the instance is handed on.");

                ExpandNullforge.Objects.DimensionAuthoredLight.Point(managed, ObjectID.None);

                Assert.IsFalse(
                    managed.lightToOptimize.gameObject.activeSelf,
                    "An object with no light of its own must not keep the last one's.");
                Assert.IsFalse(managed.lightToOptimize.enabled);
                Assert.IsFalse(
                    managed.fallbackRenderer.gameObject.activeSelf,
                    "Nor the glowing sprite the game swaps in for a light it has dimmed.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Showing a light puts every one of the author's numbers on it.
        /// </summary>
        /// <remarks>
        /// The same applier runs at generation and on every occupy, so a number it forgets is a
        /// number the running game never sees whatever the prefab says. The height and the
        /// front-to-back nudge go on the <c>ManagedLight</c>'s own node, which is where all six of
        /// the game's lit prefabs put them.
        /// </remarks>
        [Test]
        public void ShowingALightPutsEveryNumberOnIt()
        {
            GameObject host = new GameObject("SharedViewStandIn");
            try
            {
                ManagedLight managed = DimensionEmittedLightBuilder.Ensure(
                    host.transform, Vector3.zero, Color.white, 0.1f, 1f, false, 0.1f, 0.1f, false,
                    true);

                ExpandNullforge.Objects.DimensionAuthoredLight.Show(
                    managed,
                    new ExpandNullforge.Objects.DimensionEmittedLightNumbers(
                        new Color(0.2f, 0.4f, 0.6f, 1f), 0.55f, 8f, true, 0.4f, 0.7f, true,
                        2.2f, -0.4f));

                Assert.AreEqual(0.2f, managed.lightToOptimize.color.r, 0.001f);
                Assert.AreEqual(0.4f, managed.lightToOptimize.color.g, 0.001f);
                Assert.AreEqual(0.6f, managed.lightToOptimize.color.b, 0.001f);
                Assert.AreEqual(0.55f, managed.lightToOptimize.intensity, 0.001f);
                Assert.AreEqual(8f, managed.lightToOptimize.range, 0.001f);
                Assert.AreEqual(LightShadows.Hard, managed.lightToOptimize.shadows);
                Assert.AreEqual(2.2f, managed.transform.localPosition.y, 0.001f);
                Assert.AreEqual(-0.4f, managed.transform.localPosition.z, 0.001f);
                Assert.IsTrue(managed.lightToOptimize.enabled);
                Assert.IsTrue(managed.lightToOptimize.gameObject.activeSelf);

                LightFlickerEffect flicker = host.GetComponentInChildren<LightFlickerEffect>(true);
                Assert.IsNotNull(flicker);
                Assert.AreEqual(0.4f, flicker.minIntensity, 0.001f);
                Assert.AreEqual(0.7f, flicker.maxIntensity, 0.001f);
                Assert.IsTrue(flicker.enableMovement);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// A light registered by name can be found again, and clearing the table empties it.
        /// </summary>
        /// <remarks>
        /// Only the storage half is driven here. Turning a name into an <c>ObjectID</c> is
        /// <c>API.Authoring.GetObjectID</c>, which has nothing to answer with until a mod is
        /// loaded, so a test that asked for a row by id would be asserting on the game's loader
        /// rather than on this table.
        /// </remarks>
        [Test]
        public void ALightRegisteredByNameLandsInTheTableAndClearingItEmptiesIt()
        {
            ExpandNullforge.Objects.DimensionEmittedLightRegistry.Clear();
            Assert.AreEqual(0, ExpandNullforge.Objects.DimensionEmittedLightRegistry.Count);

            ExpandNullforge.Objects.DimensionEmittedLightRegistry.Register(
                "TestMod:brazier", 1f, 0.9f, 0.8f, 0.575f, 5f, true, 0.5f, 0.65f, true, 0.75f, 0f);
            Assert.AreEqual(1, ExpandNullforge.Objects.DimensionEmittedLightRegistry.Count);

            ExpandNullforge.Objects.DimensionEmittedLightNumbers numbers;
            Assert.IsFalse(
                ExpandNullforge.Objects.DimensionEmittedLightRegistry.TryGet(
                    ObjectID.None, out numbers),
                "No object is ObjectID.None, so nothing may be handed a light for it.");

            ExpandNullforge.Objects.DimensionEmittedLightRegistry.Clear();
            Assert.AreEqual(
                0,
                ExpandNullforge.Objects.DimensionEmittedLightRegistry.Count,
                "The table outlives one test, so it has to be emptiable.");
        }

        /// <summary>
        /// A lit object writes its light into the generated bootstrap; an unlit one writes nothing.
        /// </summary>
        /// <remarks>
        /// The emitted text is the only thing that survives the editor: a number that is not
        /// written into it cannot be read by the running game afterwards, whatever the asset says.
        /// And "no row" is the answer that keeps a generated container dark, so the second half
        /// matters as much as the first.
        /// </remarks>
        [Test]
        public void ALitObjectWritesItsLightIntoTheBootstrapAndAnUnlitOneWritesNothing()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetFloat("emittedLight.howFarItReaches", 5f);
            SetBool("emittedLight.itFlickers", false);
            SetFloat("emittedLight.howBright", 0.5f);

            string lit = EmitLights();
            StringAssert.Contains(
                "DimensionEmittedLightRegistry.Register(",
                lit,
                "Nothing reaches the running game unless the bootstrap says it.");
            StringAssert.Contains("\"TestMod:testlight\"", lit);
            StringAssert.Contains("0.5f", lit);

            SetBool("emittedLight.givesOffLight", false);
            Assert.That(
                EmitLights(),
                Does.Not.Contain("DimensionEmittedLightRegistry"),
                "An object with no light must register nothing: a missing row is what tells the " +
                "shared view to put the light out for it.");
        }

        // ---- what the author is told about a light that lights nothing ----

        /// <summary>
        /// A light with no brightness in it is reported, the same as one that reaches nowhere.
        /// </summary>
        /// <remarks>
        /// Both are the same mistake and only one was caught. A light with a reach and no
        /// brightness builds the whole subtree and lights nothing, and in the prefab it is not
        /// visibly different from one that works.
        /// </remarks>
        [Test]
        public void ALightWithNoBrightnessInItIsReported()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", false);
            SetFloat("emittedLight.howBright", 0f);

            Assert.IsTrue(worldObject.EmittedLight.ItLightsNothingBecauseItIsNotBrightEnough);
            Assert.IsFalse(
                worldObject.EmittedLight.ItLightsNothingBecauseItReachesNothing,
                "The reach is fine; it is the brightness that is missing.");
            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void AFlickeringLightWithBothBoundsAtZeroIsReportedToo()
        {
            // The flicker bounds are what a flickering light takes its brightness from, so both at
            // zero is the same dark light by another route.
            SetBool("emittedLight.givesOffLight", true);
            SetBool("emittedLight.itFlickers", true);
            SetFloat("emittedLight.atItsDimmest", 0f);
            SetFloat("emittedLight.atItsBrightest", 0f);

            Assert.IsTrue(worldObject.EmittedLight.ItLightsNothingBecauseItIsNotBrightEnough);
        }

        [Test]
        public void AnOrdinaryLightIsNotReportedAsAProblem()
        {
            // The control on the two checks above: a light that works must say nothing at all.
            SetBool("emittedLight.givesOffLight", true);

            Assert.IsFalse(worldObject.EmittedLight.ItLightsNothingBecauseItIsNotBrightEnough);
            Assert.IsFalse(worldObject.EmittedLight.ItLightsNothingBecauseItReachesNothing);
            Assert.IsFalse(worldObject.EmittedLight.ItsDimmestIsBrighterThanItsBrightest);
        }

        /// <summary>
        /// A height below the floor is brought back to it.
        /// </summary>
        /// <remarks>
        /// The four numbers beside it have carried a floor since they were written; this one did
        /// not, so a light could be placed under the floor, light nothing a player could see, and
        /// say nothing about it.
        /// </remarks>
        [Test]
        public void AHeightBelowTheFloorIsBroughtBackToIt()
        {
            SetBool("emittedLight.givesOffLight", true);
            SetFloat("emittedLight.howHighAboveTheFloor", -5f);

            Assert.AreEqual(0f, worldObject.EmittedLight.HowHighAboveTheFloor, 0.0001f);
        }

        [Test]
        public void ANegativeFrontToBackIsLeftAlone()
        {
            // The one number here that must NOT be clamped: all three of the game's own lights that
            // use it use a negative value, so a floor at zero would take the control away.
            SetBool("emittedLight.givesOffLight", true);
            SetFloat("emittedLight.howFarFrontToBack", -0.4f);

            Assert.AreEqual(-0.4f, worldObject.EmittedLight.HowFarFrontToBack, 0.0001f);
        }

        /// <summary>The bootstrap text this object's light would be written into.</summary>
        private string EmitLights()
        {
            DimensionTemplateAsset template =
                ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(template);
                SerializedProperty list = serialized.FindProperty("globalWorldObjects");
                Assert.IsNotNull(list, "globalWorldObjects is gone, so nothing was emitted.");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = worldObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                DimensionRuntimeConsumerBootstrapUtility.AppendEmittedLightRegistrations(
                    builder, template, "TestMod");
                return builder.ToString();
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }
    }
}
