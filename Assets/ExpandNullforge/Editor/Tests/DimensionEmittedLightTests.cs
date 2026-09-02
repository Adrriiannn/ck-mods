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
    public sealed partial class DimensionEmittedLightTests
    {

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

            // THE HALF THAT IS ASSERTED FIRST IS THE REFERENCE, because the file going and the
            // object still pointing at it is its own bug — a missing-asset reference on the thing
            // a player walks up to — and it is the half a delete can get wrong on its own. The
            // object above had a visual for one reason, its light; with the light off it has none.
            Assert.IsNotNull(LoadEntity(), "The object's own prefab is still written either way.");
            Assert.IsNull(
                LoadEntity().GetComponent<ObjectAuthoring>().graphicalPrefab,
                "The object still points at something to draw where it stands. Whether the file " +
                "is there or not, a light it no longer asks for would come back with it.");

            // TWO SHAPES BOTH SATISFY THE REST, and the stronger one is no file. An object with no
            // picture, no use and no light has nothing left to draw, so the generator takes the
            // prefab away rather than leaving an empty one — which is also what stops it lighting
            // the floor from an earlier build. An empty prefab passes too, for a caller that keeps
            // one for another reason.
            // BOTH SHAPES ARE CHECKED, rather than the first one passing the test. Assert.Pass
            // throws, so an early exit here would leave the two assertions below unreachable and
            // the empty-prefab case untested — which is what an earlier version of this test did.
            GameObject visual = LoadVisual();
            if (visual == null)
            {
                GameObject entity = LoadEntity();
                Assert.That(entity, Is.Not.Null, "The object's own prefab should still be there.");
                Assert.That(
                    entity.GetComponent<ObjectAuthoring>().graphicalPrefab,
                    Is.Null,
                    "Nothing is drawn for it, so the object must not still point at a prefab.");
                return;
            }

            Assert.AreEqual(0, EveryLightOn(visual).Length, "No ManagedLight is left behind.");
            Assert.AreEqual(
                0,
                visual.GetComponentsInChildren<Light>(true).Length,
                "And no bare Light either.");
        }

        /// <summary>
        /// Turning the light off twice says nothing the second time and leaves nothing behind.
        /// </summary>
        /// <remarks>
        /// The delete has to be a thing that happened once, not a line the creator reads on every
        /// generate for the rest of the object's life. This is also the guard on the delete firing
        /// for an object that never had a visual at all: the third pass below is exactly that
        /// object, and it must be silent.
        /// </remarks>
        [Test]
        public void TakingTheLeftOverVisualAwayIsSaidOnceAndNotEveryTimeAfter()
        {
            SetBool("emittedLight.givesOffLight", true);
            Run();

            SetBool("emittedLight.givesOffLight", false);
            DimensionWorldObjectGenerationReport first = Run();
            Assert.IsTrue(
                SomethingWasTakenAway(first),
                "Taking away a prefab the object still pointed at has to be said. Silently " +
                "un-lighting something a creator lit is the thing this delete exists to stop " +
                "being silent.");

            DimensionWorldObjectGenerationReport second = Run();
            Assert.IsFalse(
                SomethingWasTakenAway(second),
                "The second generate said it again. There was nothing left to take away, so an " +
                "object that has never had a visual would be told about one on every build.");
            Assert.IsNull(LoadVisual());
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
    }
}
