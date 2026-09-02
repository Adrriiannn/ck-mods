using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// The light on a usable object, and every view that redraws it.
    /// </summary>
    public sealed partial class DimensionEmittedLightTests
    {
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
                DimensionFrameworkSourceScanner.ReadPartials("DimensionFrameworkAuthoringWindow");
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
    }
}
