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
    internal static class DimensionInteractionVisualUtility
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

        /// <summary>
        /// Gives anything with words floating over it somewhere to keep them, and takes an empty
        /// store away again from anything that no longer shows any.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE WORDS ARE NOT A FIELD ON THE OBJECT. They are a <c>DescriptionBuffer</c> on the
        /// entity, put there by <c>DescriptionConverter</c> the moment it sees
        /// <c>DescriptionAuthoring</c> — with or without any starting text. Without the buffer
        /// <c>WorldLabel.GetName</c> returns null, the floating text never renders, and the writing
        /// window is a box a player types into that saves nothing.
        /// </para>
        /// <para>
        /// A CHEST NEEDS THIS AS MUCH AS A SIGN DOES, which is the correction here.
        /// <c>Chest : WorldLabel</c>, and <c>Chest.Use</c> refuses to make the chest the active
        /// world label unless the entity has the buffer, so a generated container without one had a
        /// dead name field in its window. The previous version of this method stripped the
        /// component off everything that was not a sign, which took it back off every container the
        /// spine had just given one to.
        /// </para>
        /// <para>
        /// NOTHING IS REMOVED HERE ANY MORE, and the reason the removal was narrowed rather than
        /// dropped was wrong. It used to take the component off whenever words did not float above
        /// the object and <c>initialText</c> was empty, on the argument that
        /// <c>DimensionObjectSpine.ApplyFacingAndText</c> had already written the author's own text
        /// — but the container and workbench generators never call <c>ApplyFacingAndText</c> at
        /// all. Their store comes from <c>DimensionObjectSpine.ApplyPlacedObject</c>, which gives
        /// one to every placed object because that is what vanilla chests carry, and which runs
        /// before this. So a container whose interaction was left at the default had the store
        /// stripped one pass after it was given, and — per this file's own reading of
        /// <c>Chest.Use</c> — its rename box saved nothing.
        /// </para>
        /// <para>
        /// Every generator that reaches this method puts its object through
        /// <c>ApplyPlacedObject</c> first (containers, workbenches, world objects), so a removal
        /// here could only ever undo that pass. An empty store costs a player nothing: it is an
        /// empty <c>DescriptionBuffer</c>, which is exactly what a vanilla placed object has.
        /// </para>
        /// </remarks>
        private static void ApplyTheStoreForFloatingWords(GameObject root)
        {
            if (root.GetComponent<DescriptionAuthoring>() == null)
            {
                root.AddComponent<DescriptionAuthoring>();
            }
        }

        /// <summary>
        /// Lets an animal be given a name, because the window that tends it always offers to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOT COSMETIC, AND NOT OPTIONAL. <c>CattleUI.SetName</c> sends the name straight to the
        /// server with no check that the animal can hold one, and the server answers with
        /// <c>EntityUtility.GetComponentData&lt;NameCD&gt;</c>
        /// (<c>PlayerCommand/ServerSystem.cs:482</c>), which THROWS when the component is absent. So
        /// a generated animal without <c>NameAuthoring</c> is not an animal with a greyed-out name
        /// box; it is an animal that throws on the server the first time somebody names it.
        /// </para>
        /// <para>
        /// <c>NameAuthoring</c> is an empty marker class whose converter does nothing but
        /// <c>EnsureHasComponent&lt;NameCD&gt;</c>, so adding it costs one component and no
        /// decisions. The name tag above the animal reads the same <c>NameCD</c>, which is why this
        /// belongs beside the tag rather than in the object-roles panel: the roles panel's
        /// "can be named" tick is a choice, and this is a requirement of the use.
        /// </para>
        /// </remarks>
        private static void ApplyBeingNameable(
            GameObject root,
            DimensionInteractionTemplate interaction)
        {
            if (!interaction.ItCarriesANameTag)
            {
                return;
            }

            if (root.GetComponent<NameAuthoring>() == null)
            {
                root.AddComponent<NameAuthoring>();
            }
        }

        /// <summary>
        /// Says so when an object is set to open a crafting window it has no recipes for.
        /// </summary>
        /// <remarks>
        /// This is the one warning in this file that survives the framework views, and it is not
        /// about art. <c>CraftingHandler</c>'s constructor reads <c>CraftingCD</c> off the entity
        /// with <c>EntityUtility.GetComponentData</c>, which throws when the component is absent, and
        /// <c>CraftingBuilding.OnOccupied</c> builds that handler the moment the object is drawn. So
        /// an object that answers "opens a crafting bench" without carrying <c>CraftingAuthoring</c>
        /// does not open an empty window — it never appears at all. Only the Workbench asset writes
        /// that component, which is why the fix names it.
        /// </remarks>
        private static void WarnAboutABenchWithNothingToCraft(
            GameObject root,
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            if (report == null ||
                interaction.WhatUsingItDoes != DimensionUseBehaviour.OpensACraftingBench ||
                root.GetComponent<CraftingAuthoring>() != null)
            {
                return;
            }

            report(
                "opens a crafting bench but has no recipes of its own, and the game reads a " +
                "recipe list before it draws the object, so it would never appear where it was " +
                "placed. Make it a Workbench instead, which carries the recipes, or set what " +
                "using it does to something else.");
        }

        /// <summary>
        /// Says so when an animal's name tag would be drawn inside the animal.
        /// </summary>
        /// <remarks>
        /// The height that floats a chest's label nicely — the default, and the game's own chest
        /// value — is half a tile, and half a tile above an animal's feet is its middle. This is the
        /// one case where the shared default is wrong rather than merely unadventurous, so it is
        /// worth a sentence; anything above half a tile is left alone, because how high a name
        /// belongs over a creature is the author's judgement, not the framework's.
        /// </remarks>
        private static void WarnAboutANameTagInsideTheAnimal(
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            if (report == null ||
                !interaction.ItCarriesANameTag ||
                interaction.HowHighTheWordsFloat > 0.5f)
            {
                return;
            }

            report(
                "is tended like an animal, and its name would float " +
                interaction.HowHighTheWordsFloat.ToString("0.###") +
                " tiles up, which is inside most animals rather than above them. Raise how high " +
                "the words float — the game's own camel uses 2.");
        }

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

        /// <summary>
        /// Hangs the words that float above the object, for the uses that have any.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THREE OF THE SEVEN USES SHOW WORDS IN THE AIR, and each wants a different component
        /// pointed at a different field. A chest and a sign are both <c>WorldLabel</c>s and want
        /// <c>worldLabel</c>; an animal is a <c>Cattle</c> and wants <c>nameTag</c>. The other four
        /// get nothing, because the game's own bench, character and vending machine have nothing.
        /// </para>
        /// <para>
        /// THIS IS NOT DECORATION FOR A CHEST. <c>Chest.Use</c> only makes the chest the player's
        /// active world label when <c>worldLabel != null</c> (<c>Chest.cs:34-35</c>), and the naming
        /// box inside the chest window writes to the active world label and nowhere else
        /// (<c>ChestInventoryUI.cs:70-75</c>). Until this line existed, every generated container
        /// opened a window whose name field could be typed into and saved nothing.
        /// </para>
        /// <para>
        /// NOR FOR AN ANIMAL. Without the tag, <c>Cattle.UpdateName</c>, <c>OnShow</c> and
        /// <c>OnHide</c> all dereference null, which is why the framework's animal view used to
        /// override its whole per-frame pass away and lose the leash rope with it.
        /// </para>
        /// </remarks>
        private static void AddFloatingWords(
            GameObject root,
            MonoBehaviour behaviour,
            DimensionInteractionTemplate interaction)
        {
            if (behaviour == null)
            {
                return;
            }

            if (interaction.WordsFloatAboveIt)
            {
                WorldLabel label = behaviour as WorldLabel;
                if (label != null)
                {
                    label.worldLabel = DimensionFloatingTextUtility.AddWordsThatFloatAboveIt(
                        root, interaction.HowHighTheWordsFloat);
                }

                return;
            }

            if (interaction.ItCarriesANameTag)
            {
                Cattle cattle = behaviour as Cattle;
                if (cattle != null)
                {
                    cattle.nameTag = DimensionFloatingTextUtility.AddNameTagAboveIt(
                        root, interaction.HowHighTheWordsFloat);
                }
            }
        }

        /// <summary>
        /// Gives the body the child that flips it left and right, the way every vanilla graphical
        /// prefab has one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOT DECORATION — IT IS A NULL CHECK THE GAME NEVER DOES.
        /// <c>EntityMonoBehaviour.SetOrientation</c> ends in <c>this.XScaler.localScale = ...</c>
        /// with no guard, and it is reached from <c>UpdateAnimatorSpeedAndOrientation</c>, which
        /// <c>UpdateGraphicalObjectSystem</c> calls for every entity every frame. The animal and the
        /// character both answer <c>true</c> to <c>updateAnimOrientation</c>, so they reach it as
        /// soon as their facing direction is non-zero.
        /// </para>
        /// <para>
        /// AND THE POOL MAKES IT WORSE THAN "ONLY FOR THOSE TWO". <c>currentFacingVector</c> is a
        /// field on the shared instance, so a view that drew a facing object a moment ago carries
        /// that direction into the next object it is handed to, whatever its behaviour. Every view
        /// gets the child.
        /// </para>
        /// </remarks>
        private static Transform AddScaler(GameObject root, MonoBehaviour behaviour)
        {
            EntityMonoBehaviour view = behaviour as EntityMonoBehaviour;
            if (view == null)
            {
                return null;
            }

            GameObject scaler = new GameObject(ScalerChildName);
            scaler.transform.SetParent(root.transform, false);
            view.XScaler = scaler.transform;
            return scaler.transform;
        }

        /// <summary>Puts the component that actually does the thing on the visual prefab.</summary>
        /// <remarks>
        /// EVERY ONE OF THESE IS A FRAMEWORK SUBCLASS, NEVER THE GAME'S OWN COMPONENT, and that is
        /// not a preference. Core Keeper pools graphical objects by component TYPE
        /// (<c>MemoryManager.CreateModdedPrefabPool</c> registers the type with <c>TryAdd</c>) and
        /// <c>CreateGraphicalObjectSystem</c> asks the pool for a body rather than instantiating the
        /// prefab, so the first prefab to claim a type is the one every later object of that type is
        /// drawn as. For <c>Chest</c>, <c>SignText</c> and <c>VendingMachine</c> the winner is one of
        /// the game's own prefabs — the ripped corpus has exactly one asset carrying each, all three
        /// listed in <c>Resources/PooledGraphicalObjectBank.asset</c>. For <c>CraftingBuilding</c>,
        /// <c>Cattle</c> and <c>NPC</c> no vanilla prefab carries the base type at all, so the winner
        /// is whichever generated object loaded first — every later station wearing the first
        /// station's picture. Both failures have the same fix.
        /// </remarks>
        private static MonoBehaviour AddUseBehaviour(
            GameObject root,
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            switch (interaction.WhatUsingItDoes)
            {
                case DimensionUseBehaviour.OpensLikeAChest:
                {
                    Chest chest =
                        root.AddComponent<ExpandNullforge.Containers.DimensionContainerView>();
                    chest.showSortAndQuickStackButtons = interaction.ShowsSortAndQuickStackButtons;
                    return chest;
                }

                case DimensionUseBehaviour.OpensACraftingBench:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionCraftingBenchView>();

                case DimensionUseBehaviour.TendedLikeAnAnimal:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionCattleView>();

                case DimensionUseBehaviour.TalkedToLikeAnNpc:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionNpcView>();

                case DimensionUseBehaviour.ReadLikeASign:
                    // Deliberately NOT a SignText subclass: that class dereferences two atlas-backed
                    // SpriteObject fields every frame from a private method. See DimensionSignView.
                    return root.AddComponent<ExpandNullforge.Objects.DimensionSignView>();

                case DimensionUseBehaviour.SellsLikeAShop:
                    // The Forlorn Metropolis machines' own behaviour: Interact opens the buy window
                    // over the entity's baked item buffer. Borrowed by subclassing it.
                    return root.AddComponent<ExpandNullforge.Objects.DimensionShopView>();

                default:
                    if (report != null)
                    {
                        report(
                            "is marked as usable in a way the framework does not know how to build, " +
                            "so using it would do nothing.");
                    }

                    return null;
            }
        }

        /// <summary>
        /// Wires the two calls — using it, and walking away from it — into the prefab itself.
        /// </summary>
        /// <remarks>
        /// Both lists are replaced rather than appended to, so regenerating never leaves a listener
        /// from a previous answer sitting behind the new one.
        /// </remarks>
        private static void WireUse(
            InteractableObject interactable,
            MonoBehaviour behaviour,
            DimensionUseBehaviour what)
        {
            UnityEvent onUse = new UnityEvent();
            UnityEvent onLeave = new UnityEvent();

            switch (what)
            {
                case DimensionUseBehaviour.OpensLikeAChest:
                {
                    Chest chest = (Chest)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, chest.Use);
                    UnityEventTools.AddPersistentListener(onLeave, chest.OnPlayerLeftChest);
                    break;
                }

                case DimensionUseBehaviour.OpensACraftingBench:
                {
                    CraftingBuilding bench = (CraftingBuilding)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, bench.Use);
                    UnityEventTools.AddPersistentListener(onLeave, bench.OnPlayerLeftBuilding);
                    break;
                }

                case DimensionUseBehaviour.TendedLikeAnAnimal:
                {
                    Cattle cattle = (Cattle)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, cattle.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, cattle.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.TalkedToLikeAnNpc:
                {
                    NPC npc = (NPC)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, npc.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, npc.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.ReadLikeASign:
                {
                    // Typed as the framework view rather than SignText because the sign is the one
                    // use that does NOT derive from the game's component — see DimensionSignView for
                    // the every-frame null dereference that rules it out. The two method names are
                    // the same, so the wiring the converter reads is unchanged.
                    ExpandNullforge.Objects.DimensionSignView sign =
                        (ExpandNullforge.Objects.DimensionSignView)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, sign.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, sign.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.SellsLikeAShop:
                {
                    VendingMachine shop = (VendingMachine)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, shop.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, shop.OnPlayerLeft);
                    break;
                }
            }

            interactable.onUseActions = new List<UnityEvent> { onUse };
            interactable.onTriggerExitActions = new List<UnityEvent> { onLeave };
        }
    }
}
