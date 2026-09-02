using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The light an interactable carries, and what is wrong with one that cannot work.
    /// </summary>
    internal static partial class DimensionInteractionVisualUtility
    {
        /// <summary>
        /// Gives the object the light it throws on the floor where it stands.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IT HAS TO BE BUILT IN HERE, NOT BOLTED ON AFTERWARDS. This method makes the visual prefab
        /// out of a brand-new <c>GameObject</c> on every generate and saves over the old file, so
        /// nothing added to that file from outside survives the next pass. A light added anywhere
        /// else is a light that works once.
        /// </para>
        /// <para>
        /// PARENTED TO THE ROOT, NOT TO THE SCALER, and that is measured rather than chosen.
        /// <c>Torch.prefab</c>'s root has exactly two children — <c>XScaler</c>, which holds the
        /// art, and <c>LightOptimizer</c>, which holds the light — side by side.
        /// <c>Lamp</c>, <c>Campfire</c>, <c>CrystalLamp</c>, <c>ChineseLantern</c> and
        /// <c>LampPost</c> all do the same. Hanging the light under the scaler instead would drag it
        /// sideways every time the object flipped to face the other way.
        /// </para>
        /// <para>
        /// AND THE VIEW HAS TO BE TOLD. <c>EntityMonoBehaviour</c> hides the light while the object
        /// is dying and switches it back on when the object is drawn, and it does both through
        /// <c>optionalLightOptimizer</c> (<c>:591</c>, <c>:625</c>, <c>:1117</c>). Vanilla's
        /// <c>Lamp.prefab</c> assigns that field; a generated object with a view and an unassigned
        /// one would come back from a death animation dark.
        /// </para>
        /// <para>
        /// A PREFAB WITHOUT A VIEW STILL LIGHTS. Nothing is assigned in that case because there is
        /// nothing to assign it to, and nothing needs to be: the subtree is left active, and
        /// <c>ManagedLight.OnEnable</c> registers the light the moment the object is instantiated.
        /// </para>
        /// <para>
        /// A PREFAB WITH A VIEW GETS THE SUBTREE WHETHER OR NOT THIS OBJECT IS LIT, and that is the
        /// one thing here that is not vanilla's shape. Such a prefab is POOLED BY COMPONENT TYPE —
        /// <c>MemoryManager.CreateModdedPrefabPool</c> ends in
        /// <c>_poolFromComponentType.TryAdd(type, prefab)</c> and only the first prefab of a type
        /// is ever instantiated — so one prefab's nodes are the nodes every object of that
        /// behaviour is drawn with. Build the light only on the lit object's prefab and a modder
        /// gets one of two results depending on which prefab registered first: their forge is dark,
        /// or every bench in the mod glows with the forge's colour. Giving every pooled prefab the
        /// node and letting <c>DimensionAuthoredLight</c> re-derive the light per entity is the
        /// same answer <c>DimensionAuthoredBody</c> already gives for the picture, which is built
        /// on every one of these prefabs for exactly the same reason.
        /// </para>
        /// <para>
        /// AN UNLIT POOLED OBJECT COSTS NOTHING ON SCREEN. The node is there but its light is
        /// switched off at the <c>GameObject</c>, so <c>PugLight.OnDisable</c> drops it from
        /// <c>PugLight.instances</c>, Unity's culling never reports it, and
        /// <c>ManagedLight.SetOptimized</c> forces its fallback glow off (<c>:109-112</c>). What it
        /// does cost is one <c>ManagedLight</c> in the list <c>UpdateOptimization</c> walks, per
        /// live instance.
        /// </para>
        /// </remarks>
        private static void AddLight(
            GameObject root,
            MonoBehaviour behaviour,
            DimensionUseBehaviour what,
            DimensionEmittedLightTemplate emittedLight,
            System.Action<string> report)
        {
            EntityMonoBehaviour view = behaviour as EntityMonoBehaviour;
            bool givesOffLight = emittedLight != null && emittedLight.GivesOffLight;

            if (view == null && !givesOffLight)
            {
                // Nothing pools this prefab and nothing asked for a light, so it gets no nodes at
                // all — the shape every vanilla prefab without a light has.
                return;
            }

            if (givesOffLight)
            {
                ReportWhatIsWrongWithTheLight(emittedLight, report);
            }

            // A steady light is a flicker with both ends the same, and a flickering one is baked at
            // the midpoint the game overwrites it with; both rules live in TheNumbersTheGameRunsWith
            // so that the value written into this prefab and the value re-applied per entity cannot
            // say different things. An object with no light asks nothing and gets the empty answer.
            ExpandNullforge.Objects.DimensionEmittedLightNumbers numbers = givesOffLight
                ? emittedLight.TheNumbersTheGameRunsWith()
                : default(ExpandNullforge.Objects.DimensionEmittedLightNumbers);

            ManagedLight managedLight;
            try
            {
                managedLight = DimensionEmittedLightBuilder.Ensure(
                    root.transform,
                    new Vector3(0f, numbers.HowHighAboveTheFloor, numbers.HowFarFrontToBack),
                    numbers.Colour,
                    numbers.HowBright,
                    numbers.HowFarItReaches,
                    numbers.ItCastsShadows,
                    numbers.AtItsDimmest,
                    numbers.AtItsBrightest,
                    numbers.TheFlameMoves,
                    true);
            }
            catch (System.InvalidOperationException missingTemplate)
            {
                // The builder throws when the framework's own donor prefab is not there to copy,
                // which is right for an object that ASKED for a light: it is a broken install and
                // saying so loudly is the only useful answer. It is not right for an object that
                // asked for nothing and is only being given the node so that a lit object sharing
                // its pool has somewhere to put a light — failing a plain chest's build over a
                // light template nobody mentioned would be baffling.
                if (givesOffLight)
                {
                    throw;
                }

                if (report != null)
                {
                    report(
                        "could not be given the light nodes every object of its use shares (" +
                        missingTemplate.Message + "). It stays dark, which is what it asked for, " +
                        "but a lit object used the same way may not light while this is true.");
                }

                return;
            }

            if (view == null)
            {
                // Not pooled: this prefab is instantiated per entity, so what is baked above IS the
                // light and there is no field to assign it to.
                return;
            }

            // Run the same applier the view runs on every occupy, rather than trusting that the
            // builder above happened to leave the node in the state the applier would. It is the
            // only way the prefab on disk is guaranteed to match what a player sees.
            if (givesOffLight)
            {
                ExpandNullforge.Objects.DimensionAuthoredLight.Show(managedLight, numbers);
            }
            else
            {
                ExpandNullforge.Objects.DimensionAuthoredLight.Hide(managedLight);
            }

            view.optionalLightOptimizer = managedLight;

            if (behaviour is ExpandNullforge.Objects.IDimensionAuthoredLight || report == null)
            {
                return;
            }

            // Unreachable for the six uses the framework knows: every one of their views carries
            // IDimensionAuthoredLight and calls the applier from its own occupy. It stays as the
            // tripwire for a seventh added later — whoever adds it will read this instead of
            // shipping a light control that works for one object per behaviour and silently not
            // for the rest.
            report(
                "is used in a way that has no framework light yet (" + what + ", drawn by " +
                behaviour.GetType().Name + "). Core Keeper hands out one shared body per component " +
                "type and the light hangs on that body, so every object used this way would show " +
                "whichever light was written first: one lit object would light all of them, and " +
                "one dark object would darken all of them. Set what using it does to one of the " +
                "listed uses if the light matters.");
        }

        /// <summary>Says what is wrong with an authored light, if anything is.</summary>
        /// <remarks>
        /// Both ways of asking for a light that lights nothing are here together. Reaching nowhere
        /// was reported before; being asked for at no brightness was not, and it produces the same
        /// complete-looking subtree that lights nothing — a light with a reach and no brightness is
        /// not visibly different in the prefab from one that works.
        /// </remarks>
        private static void ReportWhatIsWrongWithTheLight(
            DimensionEmittedLightTemplate emittedLight,
            System.Action<string> report)
        {
            if (report == null)
            {
                return;
            }

            if (emittedLight.ItLightsNothingBecauseItReachesNothing)
            {
                report(
                    "gives off light that reaches no distance at all, so nothing around it is lit. " +
                    "Say how far it reaches — the game's torch reaches five tiles.");
            }

            if (emittedLight.ItLightsNothingBecauseItIsNotBrightEnough)
            {
                report(
                    "gives off light with no brightness in it, so nothing around it is lit. The " +
                    "game's torch sits at 0.65, and a flickering light takes its brightness from " +
                    "the dimmest and brightest numbers instead of from how bright.");
            }

            if (emittedLight.ItsDimmestIsBrighterThanItsBrightest)
            {
                report(
                    "flickers between a dimmest that is brighter than its brightest, which is the " +
                    "two numbers the wrong way round.");
            }
        }
    }
}
