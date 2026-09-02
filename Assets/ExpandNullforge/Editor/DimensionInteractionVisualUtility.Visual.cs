using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The visual prefab an interactable object needs, and clearing away the last one.
    /// </summary>
    internal static partial class DimensionInteractionVisualUtility
    {
        /// <summary>
        /// Writes the prefab a player walks up to, replacing whatever was there before.
        /// </summary>
        /// <remarks>
        /// Rebuilt from scratch each generate rather than edited in place. The wiring is a graph of
        /// references between a behaviour component and two event lists on a child, and patching
        /// that graph in place is how a stale listener survives a changed answer.
        /// </remarks>
        private static GameObject BuildVisualPrefab(
            DimensionInteractionTemplate interaction,
            string folder,
            string assetStem,
            System.Action<string> report,
            bool drawsItself,
            Sprite ownBody,
            DimensionEmittedLightTemplate emittedLight)
        {
            string path = folder.TrimEnd('/') + "/" + assetStem + VisualSuffix + ".prefab";

            GameObject root = new GameObject(assetStem + VisualSuffix);
            try
            {
                MonoBehaviour behaviour = interaction.IsUsable
                    ? AddUseBehaviour(root, interaction, report)
                    : null;
                if (interaction.IsUsable && behaviour == null)
                {
                    return null;
                }

                Transform scaler = AddScaler(root, behaviour);

                if (drawsItself)
                {
                    AddBody(root, scaler, behaviour, ownBody, report);
                }

                AddLight(root, behaviour, interaction.WhatUsingItDoes, emittedLight, report);

                AddFloatingWords(root, behaviour, interaction);

                if (!interaction.IsUsable)
                {
                    // Nothing uses it, so it needs no interactable and no listeners — and, just as
                    // importantly, no component of the game's own. A graphical prefab with no
                    // EntityMonoBehaviour on it takes CreateGraphicalObjectSystem's other branch and
                    // is Instantiated per entity rather than fetched from a shared pool, so the
                    // picture baked below is the one that shows.
                    return SaveDuringABatch(root, path);
                }

                AddInteractable(
                    root,
                    behaviour,
                    interaction.Reach,
                    interaction.WorksFromAnySide,
                    interaction.OnlyWhoeverClaimedIt,
                    interaction.HowStronglyItAsksToBeUsed,
                    interaction.AFlatOutlineColour,
                    interaction.OnlyThisFactionMayUseIt,
                    interaction.WhatUsingItDoes,
                    report);

                GameObject written = SaveDuringABatch(root, path);
                if (written == null)
                {
                    if (report != null)
                    {
                        report(
                            "can be used, but the prefab a player walks up to could not be written " +
                            "to '" + path + "', so using it would do nothing.");
                    }

                    return null;
                }

                return written;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Builds the child a player actually walks up to, and wires the use to it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Lifted out of <see cref="BuildVisualPrefab"/> unchanged so the creature entry below can
        /// use the identical wiring rather than a second copy of it. The two entries differ in one
        /// thing only — where the body comes from — and that difference is the whole reason there
        /// are two.
        /// </para>
        /// <para>
        /// <c>CreateGraphicalObjectSystem</c> only fills <c>InteractableObjectReferenceCD</c> from
        /// the view's own field, and <c>LocalInteractionSystem</c> reads the use event off THAT
        /// component and gives up when it is null. Without the assignment every generated object
        /// baked its wiring correctly and then did nothing at all when a player used it, and it
        /// never drew an outline either — <c>EntityMonoBehaviour.UpdateOutline</c> reads the same
        /// field.
        /// </para>
        /// </remarks>
        private static InteractableObject AddInteractable(
            GameObject root,
            MonoBehaviour behaviour,
            float reach,
            bool worksFromAnySide,
            bool onlyWhoeverClaimedIt,
            float howStronglyItAsksToBeUsed,
            bool aFlatOutlineColour,
            string onlyThisFactionMayUseIt,
            DimensionUseBehaviour what,
            System.Action<string> report)
        {
            GameObject child = new GameObject(InteractableChildName);
            child.transform.SetParent(root.transform, false);

            InteractableObject interactable = child.AddComponent<InteractableObject>();
            interactable.radius = reach;
            interactable.ignorePlayerDirection = worksFromAnySide;
            interactable.allowToUseOnlyWhenClaimed = onlyWhoeverClaimedIt;
            interactable.weightMultiplier = howStronglyItAsksToBeUsed;
            interactable.useDiscreteOutlineColor = aFlatOutlineColour;
            interactable.additionalOutlineControllers = new List<OutlineController>();
            interactable.spriteObjects = new List<Pug.Sprite.SpriteObject>();

            FactionID faction;
            if (!string.IsNullOrEmpty(onlyThisFactionMayUseIt))
            {
                if (System.Enum.TryParse(onlyThisFactionMayUseIt, false, out faction))
                {
                    interactable.requiredFactionToInteract = faction;
                }
                else if (report != null)
                {
                    report(
                        "may only be used by '" + onlyThisFactionMayUseIt +
                        "', which is not a faction the game has, so anyone can use it.");
                }
            }

            Transform point = new GameObject("InteractionPoint").transform;
            point.SetParent(child.transform, false);
            interactable.interactingPoints = new List<Transform> { point };

            EntityMonoBehaviour view = behaviour as EntityMonoBehaviour;
            if (view != null)
            {
                view.interactable = interactable;
            }

            WireUse(interactable, behaviour, what);
            return interactable;
        }

        /// <summary>
        /// Wires a use onto a creature's own animated body, rather than building a body for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A SEPARATE ENTRY, AND NOT A SHORTCUT. <see cref="Apply"/> cannot be pointed at a creature
        /// however tempting it looks: it builds its OWN graphical prefab out of one still picture
        /// and assigns it over <c>ObjectAuthoring.graphicalPrefab</c>, which is where the creature
        /// generator has already put the animated body it built at
        /// <c>DimensionCreatureGenerator.cs:532</c>. Calling it on a creature throws that body away
        /// and replaces an animal with a photograph of one. Whoever is tempted to simplify these
        /// two back into one should read that line first.
        /// </para>
        /// <para>
        /// WHAT IT DOES ADD is exactly the half a creature is missing: the interactable child, its
        /// reach and faction, the <c>view.interactable</c> assignment, the two persistent listeners,
        /// and — for an animal — the name tag that <c>Cattle.UpdateName</c> dereferences every frame
        /// with no null check. The caller saves the prefab afterwards, as it already does.
        /// </para>
        /// <para>
        /// THE OTHER HALF IS ON THE ENTITY, not here: <c>LocalInteractableAuthoring</c> has to be on
        /// the creature's own prefab or <c>LocalInteractableConverter</c> never looks at any of
        /// this. The creature generator adds it beside the call to this method.
        /// </para>
        /// </remarks>
        public static void ApplyToACreatureView(
            GameObject viewRoot,
            MonoBehaviour behaviour,
            DimensionUseBehaviour what,
            DimensionCreatureTendingTemplate tending,
            System.Action<string> report)
        {
            if (viewRoot == null || behaviour == null || what == DimensionUseBehaviour.Nothing)
            {
                return;
            }

            DimensionCreatureTendingTemplate numbers =
                tending ?? new DimensionCreatureTendingTemplate();

            AddInteractable(
                viewRoot,
                behaviour,
                numbers.HowCloseAPlayerMustBe,
                numbers.WorksFromAnySide,
                false,
                numbers.HowStronglyItAsksToBeUsed,
                false,
                numbers.OnlyThisFactionMayUseIt,
                what,
                report);

            // THE NAME TAG IS NOT DECORATION. Cattle.UpdateName, OnShow and OnHide all dereference
            // nameTag with no check, from a per-frame pass, and Cattle.GetName answers null until
            // the entity has a NameCD to read — which is why the creature's habits also put
            // NameAuthoring on. Without both halves the tending window offers to name an animal
            // that has nowhere to keep the name.
            Cattle cattle = behaviour as Cattle;
            if (cattle != null)
            {
                cattle.nameTag = DimensionFloatingTextUtility.AddNameTagAboveIt(
                    viewRoot, numbers.HowHighItsNameFloats);

                if (numbers.ItsNameWouldFloatInsideIt && report != null)
                {
                    report(
                        "can be tended, and its name would float " +
                        numbers.HowHighItsNameFloats.ToString("0.###") +
                        " tiles up, which is inside most animals rather than above them. Raise how " +
                        "high its name floats — the game's own cow uses 2.");
                }
            }
        }

        /// <summary>
        /// Writes a brand-new prefab asset from inside a generator's asset-editing batch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY GENERATOR WRAPS ITS RUN IN <c>AssetDatabase.StartAssetEditing</c>, which holds the
        /// importer back so a hundred prefabs cost one import instead of a hundred. Inside that
        /// window <c>SaveAsPrefabAsset</c> still writes the file, but it reports failure and hands
        /// back null, because the asset it would return has not been imported yet. That is fine for
        /// the entity prefabs, whose return value nobody reads — and fatal here, where the returned
        /// reference is the whole point: it is what goes into <c>graphicalPrefab</c>.
        /// </para>
        /// <para>
        /// So the batch is paused for exactly this one save and started again straight after. The
        /// pause is balanced, so the generator's own <c>StopAssetEditing</c> still closes the window
        /// it opened. This costs one import per usable object, which is the price of the reference
        /// being real.
        /// </para>
        /// </remarks>
        private static GameObject SaveDuringABatch(GameObject root, string path)
        {
            AssetDatabase.StopAssetEditing();
            try
            {
                bool saved;
                GameObject written = PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
                if (saved && written != null)
                {
                    return written;
                }

                // The file may still be on disk even when the save reported failure, so the path is
                // asked directly before giving up on it.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
            }
        }

        /// <summary>
        /// Gives the visual prefab something to draw.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A plain <c>SpriteRenderer</c> rather than one of the game's <c>SpriteObject</c>s. A
        /// SpriteObject is addressed into a compiled sprite ATLAS and cannot be pointed at a loose
        /// Sprite an author dropped into a field, so a renderer is the only thing that can draw
        /// what was actually authored. It is the same shape the boss body already uses.
        /// </para>
        /// <para>
        /// THE BAKED SPRITE ONLY COUNTS ON A PREFAB NOTHING POOLS. When the object carries an
        /// EntityMonoBehaviour, Core Keeper hands out a shared instance per component type, so the
        /// value written here is replaced on every occupy by the view reading the entity's own
        /// object record. It is still written, because a prefab opened in the inspector with an
        /// empty renderer reads as a mistake.
        /// </para>
        /// </remarks>
        private static void AddBody(
            GameObject root,
            Transform scaler,
            MonoBehaviour behaviour,
            Sprite sprite,
            System.Action<string> report)
        {
            GameObject bodyObject = new GameObject(BodyChildName);
            bodyObject.transform.SetParent(scaler != null ? scaler : root.transform, false);

            SpriteRenderer renderer = bodyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.spriteSortPoint = SpriteSortPoint.Center;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = sprite != null;

            ExpandNullforge.Objects.IDimensionAuthoredBody dressable =
                behaviour as ExpandNullforge.Objects.IDimensionAuthoredBody;
            if (dressable != null)
            {
                dressable.Body = renderer;
                return;
            }

            if (behaviour != null && report != null)
            {
                // Unreachable for the six uses the framework knows: each has a view of its own that
                // implements IDimensionAuthoredBody. It stays as the tripwire for a seventh added
                // later — whoever adds it will see this rather than a silently blank object.
                report(
                    "is used in a way that has no framework body yet (" +
                    behaviour.GetType().Name + "). Core Keeper hands out one shared body per " +
                    "component type, so its picture would be whichever object claimed that type " +
                    "first. Set what using it does to one of the listed uses if the picture " +
                    "matters.");
            }
        }

        /// <summary>
        /// Takes away a visual prefab written by an earlier generate that nothing asks for now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The object stops pointing at it first and the file goes second, so a half-done delete
        /// leaves a dangling file rather than a dangling reference. Says so only when something was
        /// actually removed: an object that never had one is the ordinary case and needs no line.
        /// </para>
        /// <para>
        /// NOTHING READS A VISUAL PREFAB FOR AN OBJECT THAT DRAWS NOTHING, which is what makes the
        /// delete right rather than merely tidy. On the game's side every reader is written for the
        /// null: <c>GraphicalObjectConversion</c> only creates the graphical entity when
        /// <c>graphicalPrefab</c> is set (<c>ck-db\Pug.Other\Pug\ECS\Hybrid\GraphicalObjectConversion.cs:35-47</c>),
        /// <c>ObjectAuthoring.ObjectAuthoringToObjectInfo</c> writes a null <c>prefab</c> into the
        /// object's own record (<c>:56</c>), and <c>SittableConverter</c>,
        /// <c>InteractablePostConverter</c> and <c>LocalInteractableConverter</c> all test it first.
        /// That is exactly the shape one of Core Keeper's own objects with nothing to draw has. On
        /// this side the two readers — <c>DimensionObjectSpine.ThereIsSomewhereToSit</c> and
        /// <c>DimensionQueryCompanions.ThereIsSomethingToUseOnIt</c> — both answer false on a null,
        /// which is the true answer for an object with no picture and no use.
        /// </para>
        /// <para>
        /// AND THE FILE IS THE ONLY THING THAT HAS TO GO. <c>AssetDatabase.DeleteAsset</c> takes the
        /// <c>.meta</c> with it, so no orphan sidecar is left. The mod's file list is not a record
        /// to keep in step: <c>ModBuilder.UpdateAssetHashes</c> clears
        /// <c>ModBuilderSettings.assets</c> and refills it from a folder scan on every build, and
        /// <c>CheckAssetsForChanges</c> walks the assets that are there now, so a deleted file
        /// simply stops appearing. Nothing else in the tree points at a <c>…Visual.prefab</c> by
        /// path; the entity prefab's own reference is the only one, and it is cleared above before
        /// the file goes.
        /// </para>
        /// </remarks>
        private static void ClearAnyVisualLeftFromBefore(
            GameObject root,
            string folder,
            string assetStem,
            System.Action<string> report)
        {
            if (root == null || string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(assetStem))
            {
                return;
            }

            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            bool pointedAtOne = objectAuthoring != null && objectAuthoring.graphicalPrefab != null;
            if (objectAuthoring != null)
            {
                objectAuthoring.graphicalPrefab = null;
            }

            // The same expression BuildVisualPrefab writes with, through the same constant. Spelled
            // out here as a literal it would have been a second copy of the file name, and the two
            // only have to disagree once for the delete to miss the file it was written for.
            string path = folder.TrimEnd('/') + "/" + assetStem + VisualSuffix + ".prefab";
            bool hadAFile = UnityEditor.AssetDatabase
                .LoadAssetAtPath<GameObject>(path) != null;
            if (hadAFile)
            {
                UnityEditor.AssetDatabase.DeleteAsset(path);
            }

            if ((pointedAtOne || hadAFile) && report != null)
            {
                report(
                    "had something drawn where it stands from an earlier build, and nothing asks "
                        + "for it now, so it was taken away. If this was the light, that is why it "
                        + "has stopped glowing.");
            }
        }
    }
}
