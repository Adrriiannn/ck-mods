using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// The named lights, their measured numbers, and the shared builder.
    /// </summary>
    public sealed partial class DimensionEmittedLightTests
    {
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
    }
}
