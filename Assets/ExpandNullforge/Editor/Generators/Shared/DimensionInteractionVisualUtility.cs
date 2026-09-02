using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Builds the second prefab — the one a player actually walks up to — and wires the use to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A THING IN CORE KEEPER IS TWO PREFABS. The entity prefab is the data the simulation runs on;
    /// the graphical prefab is what stands in the world, and the interaction lives there. An author
    /// answers one question — "what does using it do?" — and this is the only place that knows the
    /// answer means a second prefab with an <c>InteractableObject</c> on a child, a behaviour
    /// component beside it, two <c>UnityEvent</c> listeners wired between them, and
    /// <c>LocalInteractableAuthoring</c> back on the entity so the converter notices any of it.
    /// </para>
    /// <para>
    /// THE CONVERTER FAILS LOUDLY AND UNHELPFULLY IF THE WIRING IS MISSING.
    /// <c>LocalInteractableConverter</c> reads the graphical prefab's first
    /// <c>InteractableObject</c>, counts persistent listeners on its two event lists, and logs
    /// "No local interaction events registered on entity" when it finds none. That error is the
    /// symptom of a half-built object, so this never adds the authoring component without also
    /// wiring at least one listener.
    /// </para>
    /// <para>
    /// THE LISTENERS ARE PERSISTENT, NOT RUNTIME. A listener added with <c>+=</c> lives only as long
    /// as the object does and is not saved; <c>UnityEventTools.AddPersistentListener</c> writes it
    /// into the prefab, which is what the converter reads at bake time. This is the same wiring the
    /// portal pipeline uses.
    /// </para>
    /// </remarks>
    internal static partial class DimensionInteractionVisualUtility
    {
        /// <summary>The name given to the child that carries the interaction.</summary>
        private const string InteractableChildName = "Interactable";

        /// <summary>The suffix on the generated graphical prefab's file name.</summary>
        private const string VisualSuffix = "Visual";

        /// <summary>The name given to the child that carries the object's picture.</summary>
        private const string BodyChildName = "Body";

        /// <summary>
        /// The name Core Keeper's own graphical prefabs give the child that flips left and right.
        /// </summary>
        /// <remarks>
        /// Kept identical to vanilla — <c>Chest.prefab</c>, <c>VendingMachine.prefab</c> and
        /// <c>SignText.prefab</c> all hang their art under a child called this — so anyone opening a
        /// generated prefab beside a ripped one sees the same shape.
        /// </remarks>
        private const string ScalerChildName = "XScaler";

        /// <summary>
        /// Makes the entity match what the author asked for: builds or refreshes its graphical
        /// prefab when it is usable, and takes the wiring off when it is not.
        /// </summary>
        /// <param name="root">The entity prefab being generated.</param>
        /// <param name="interaction">What the author answered.</param>
        /// <param name="folder">Where the entity prefab is being written.</param>
        /// <param name="assetStem">The entity's file name without its extension.</param>
        /// <param name="report">Somewhere to say what was quietly dropped.</param>
        /// <param name="drawsItself">
        /// The caller wants this object to SHOW something as well as do something. It gets a
        /// renderer on its graphical prefab, and the prefab is written even when nothing uses the
        /// object — a chest nobody can open still has to be visible where it stands.
        /// </param>
        /// <param name="ownBody">
        /// The picture to bake, for the case where the graphical prefab is one of a kind. As soon as
        /// anything uses the object the prefab is POOLED and shared with every other object of that
        /// use, so the baked value is overwritten on every occupy and only the renderer itself
        /// matters — see <see cref="ExpandNullforge.Objects.DimensionAuthoredBody"/>.
        /// </param>
        /// <param name="emittedLight">
        /// The light the object throws on the floor where it stands, if it was given one. It has to
        /// arrive here rather than beside the entity's other lighting components because Core
        /// Keeper keeps a placed light in the GRAPHICAL prefab, as a subtree of nodes, and this
        /// method is the only writer of that file. Left null by the callers whose assets do not ask
        /// the question yet, which is why it defaults — and null is a real answer rather than a
        /// gap: a usable object still gets the light nodes, switched off, because its prefab is
        /// shared with every other object of its use and one of those may be lit.
        /// </param>
        public static void Apply(
            GameObject root,
            DimensionInteractionTemplate interaction,
            string folder,
            string assetStem,
            System.Action<string> report,
            bool drawsItself = false,
            Sprite ownBody = null,
            DimensionEmittedLightTemplate emittedLight = null)
        {
            if (root == null || interaction == null)
            {
                return;
            }

            // Ahead of every early return below: an object that shows words needs the store however
            // the rest of this method turns out.
            ApplyTheStoreForFloatingWords(root);
            ApplyBeingNameable(root, interaction);

            // A LIGHT ON ITS OWN IS ENOUGH REASON TO WRITE THE PREFAB. The light lives in the
            // graphical prefab and nowhere else, so an object that gives one off and has no picture
            // and no use — a glow under a floor grate, a lit alcove — would otherwise have its
            // light quietly dropped along with the file it lives in.
            bool givesOffLight = emittedLight != null && emittedLight.GivesOffLight;
            bool wantsAVisual =
                interaction.IsUsable || (drawsItself && (ownBody != null || givesOffLight));
            if (!wantsAVisual)
            {
                DimensionObjectSpine.TryRemoveComponent<Interaction.LocalInteractableAuthoring>(root);

                if (drawsItself && report != null)
                {
                    report(
                        "has no picture and nothing that uses it, so nothing is drawn where it " +
                        "stands. Give it a sprite, or set what using it does.");
                }

                // Unreachable for every caller today: the only asset that answers the light
                // question also asks for a body. It is here for the caller that asks the light
                // question without one, so that a dropped light is a sentence rather than silence.
                if (!drawsItself && givesOffLight && report != null)
                {
                    report(
                        "gives off light, but nothing is drawn where it stands, and the light " +
                        "lives on the thing that is drawn. It would light nothing.");
                }

                // NOTHING WANTS A VISUAL NOW, BUT SOMETHING MAY HAVE LAST TIME. An object whose
                // only reason for a prefab was its light keeps that prefab when the light is
                // switched off — the file is not rewritten and the object still points at it — so
                // it carries on lighting the floor after the creator turned the light off, with
                // nothing said. Take the old one away instead of leaving it.
                ClearAnyVisualLeftFromBefore(root, folder, assetStem, report);
                return;
            }

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(assetStem))
            {
                DimensionObjectSpine.TryRemoveComponent<Interaction.LocalInteractableAuthoring>(root);

                if (report != null)
                {
                    report(
                        "needs a prefab a player walks up to, and there is nowhere to write it, " +
                        "so what it does where it stands — being used, and any light it gives " +
                        "off — would do nothing.");
                }

                return;
            }

            GameObject visual = BuildVisualPrefab(
                interaction, folder, assetStem, report, drawsItself, ownBody, emittedLight);
            if (visual == null)
            {
                DimensionObjectSpine.TryRemoveComponent<Interaction.LocalInteractableAuthoring>(root);
                return;
            }

            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null)
            {
                objectAuthoring.graphicalPrefab = visual;
            }

            // Only when something actually uses it. LocalInteractableConverter reads the graphical
            // prefab's first InteractableObject and logs "No local interaction events registered on
            // entity" when it finds none, so a purely decorative body must not carry this.
            if (interaction.IsUsable)
            {
                if (root.GetComponent<Interaction.LocalInteractableAuthoring>() == null)
                {
                    root.AddComponent<Interaction.LocalInteractableAuthoring>();
                }
            }
            else
            {
                DimensionObjectSpine.TryRemoveComponent<Interaction.LocalInteractableAuthoring>(root);
            }

            WarnAboutABenchWithNothingToCraft(root, interaction, report);

            if (report != null && interaction.IsUsable && interaction.NobodyCanEverReachIt)
            {
                report(
                    "can be used from no distance at all, so no player can ever be close enough " +
                    "to use it.");
            }

            WarnAboutANameTagInsideTheAnimal(interaction, report);
        }
    }
}
