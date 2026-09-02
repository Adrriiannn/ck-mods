using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What a rename should do with a block's pinned identity.</summary>
    /// <remarks>
    /// Only a block has a pin, so every other kind of rename passes
    /// <see cref="LeaveThePinAlone"/> and nothing happens.
    /// </remarks>
    internal enum DimensionRenameIdentityAnswer
    {
        /// <summary>There is no pin here, or it is not this rename's business.</summary>
        LeaveThePinAlone = 0,

        /// <summary>
        /// Freeze what this block is now, so tiles already in somebody's world stay this block and
        /// the rename is a change of label only.
        /// </summary>
        KeepTheIdentity = 1,

        /// <summary>
        /// Let the old identity go, so the renamed block is a genuinely new block. Tiles in a world
        /// that was played under the old one stop matching.
        /// </summary>
        StartFresh = 2
    }

    /// <summary>
    /// What renaming one name would do, worked out before anything is written.
    /// </summary>
    /// <remarks>
    /// Read-only by construction. Nothing on this class touches an asset; it is the thing shown to
    /// the creator so the question asked is the question answered.
    /// </remarks>
    internal sealed class DimensionIdRenamePlan
    {
        internal DimensionIdRenamePlan(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            string oldId,
            string newId)
        {
            Template = template;
            Asset = asset;
            Field = field;
            OldId = oldId ?? string.Empty;
            NewId = newId ?? string.Empty;
            Edges = new List<DimensionIdReference>();
            Blockers = new List<string>();
            Warnings = new List<string>();
            GhostFiles = new List<string>();
            Claimants = new List<DimensionIdClaimant>();
        }

        internal DimensionTemplateAsset Template { get; }

        internal Object Asset { get; }

        internal DimensionIdentityField Field { get; }

        internal string OldId { get; }

        internal string NewId { get; }

        /// <summary>
        /// Every place the old name is written, the asset's own name included — that one is marked
        /// <see cref="DimensionIdReference.IsAName"/> and is written last, not with the rest.
        /// </summary>
        internal List<DimensionIdReference> Edges { get; }

        /// <summary>Reasons this rename is refused, in the words a creator reads.</summary>
        internal List<string> Blockers { get; }

        /// <summary>Things that will change and are not stopped, said before they happen.</summary>
        internal List<string> Warnings { get; }

        /// <summary>Generated files that would be left behind under the old name.</summary>
        internal List<string> GhostFiles { get; }

        /// <summary>
        /// Other things in this pack that answer to the same name, in different namespaces.
        /// </summary>
        /// <remarks>
        /// Empty nearly always, and when it is not it is what decides whether a row starts ticked.
        /// See <see cref="DimensionIdReferences.OtherThingsCalled"/>.
        /// </remarks>
        internal List<DimensionIdClaimant> Claimants { get; }

        /// <summary>
        /// True when a pointer saying this name could mean something else in this pack, so no
        /// pointer row can be ticked on by default.
        /// </summary>
        internal bool TheNameIsSharedInThisPack
        {
            get { return Claimants.Count > 0; }
        }

        /// <summary>
        /// Whether this row should start ticked, and why not when it should not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE DEFAULT HAS TO BE SAFE, and unticking everything is not safe either — a list nobody
        /// can act on is a feature nobody has. So the question asked is the narrow one: could this
        /// row mean something OTHER than the thing being renamed?
        /// </para>
        /// <para>
        /// It cannot, unless two things in this pack share the name. When they do, a row whose
        /// field is named after this kind ("Output Item Id" while an item is being renamed) is
        /// still ticked, and everything else starts off with the reason written into the row. That
        /// reading of a field's name is only ever allowed to take a tick OUT.
        /// </para>
        /// </remarks>
        internal bool StartsTicked(DimensionIdReference edge, out string because)
        {
            because = string.Empty;
            if (edge.Hold == DimensionIdHold.TheThingItself)
            {
                because = "it holds this " + Field.Thing + " itself, so there is no name in it to change";
                return false;
            }

            if (edge.IsAName)
            {
                because = "it is a name of its own, and rewriting it is a second rename";
                return false;
            }

            if (edge.OutsideThisPack)
            {
                because = "it is in another dimension in this project";
                return false;
            }

            // A derived row was found by matching "EmberSeed", not "Ember", so nothing else in the
            // pack can be answering to it — a second thing genuinely called EmberSeed would have
            // collided with the plant's own generated seed long before this.
            if (edge.BecomesLocal.Length > 0)
            {
                return true;
            }

            if (!TheNameIsSharedInThisPack)
            {
                return true;
            }

            if (string.Equals(edge.ReadsAs, Field.Thing, StringComparison.Ordinal))
            {
                return true;
            }

            string alsoCalled = Claimants[0].Thing;
            because = edge.ReadsAs.Length > 0
                ? "this pack also has a " + alsoCalled + " called \"" + OldId +
                  "\", and this field reads as a " + edge.ReadsAs + "'s name, not a " +
                  Field.Thing + "'s"
                : "this pack also has a " + alsoCalled + " called \"" + OldId +
                  "\", and nothing about this field says which of the two it means";
            return false;
        }

        /// <summary>True when nothing refuses it.</summary>
        internal bool CanGo
        {
            get { return Blockers.Count == 0; }
        }

        /// <summary>How many places would actually be rewritten, the identity not counted.</summary>
        internal int InboundCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Edges.Count; i++)
                {
                    if (!IsTheIdentityRow(Edges[i]))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>True for the one row that IS the name being renamed.</summary>
        internal bool IsTheIdentityRow(DimensionIdReference edge)
        {
            return edge.Asset == Asset &&
                   string.Equals(edge.PropertyPath, Field.Property, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Renaming a name, and rewriting everything that points at it, in one step a creator can undo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EDGES FIRST, IDENTITY LAST, AND THAT ORDER IS NOT ARBITRARY. A rename interrupted halfway —
    /// a crash, a locked asset, a domain reload — then always leaves the identity at its OLD value,
    /// so the rename is incomplete rather than lost, and running the same old → new again finishes
    /// it, because every edge write is idempotent. Identity-first leaves the opposite: an asset
    /// called the new name that nothing points at, and no way left to discover who used to.
    /// </para>
    /// <para>
    /// WHAT IT REFUSES, AND WHY EACH REFUSAL IS A REFUSAL RATHER THAN A WARNING. Renaming TO one of
    /// the game's own object names is the worst case in the whole feature and it is completely
    /// silent today: <c>DimensionNamingContext.Owns</c> asks vanilla FIRST and answers false, so
    /// <c>QualifyReference</c> leaves every recipe, ingredient, drop, shot and trader entry
    /// unqualified and they all now mean the GAME's object — while <c>QualifyGenerated</c> qualifies
    /// unconditionally, so the mod still ships an object nothing can reach. Nothing would be said.
    /// The binder's own rule is used to spot it (<c>DimensionObjectBinder.Vanilla</c>), not the
    /// validator's, because the binder is what the emitter actually asks.
    /// </para>
    /// <para>
    /// FILES ARE NEVER DELETED BY A RENAME. Deleting an asset is outside undo, so a rename that
    /// removed the old prefab could be undone into a project that no longer has it. The leftovers
    /// are named instead, and removing them is a separate thing the creator says yes to.
    /// </para>
    /// </remarks>
    internal static class DimensionIdRename
    {
        /// <summary>Works out what would happen. Writes nothing.</summary>
        internal static DimensionIdRenamePlan Plan(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            string newId,
            bool alsoOutsideThisPack)
        {
            string oldId = ReadId(asset, field);
            DimensionIdRenamePlan plan =
                new DimensionIdRenamePlan(template, asset, field, oldId, newId);

            if (template == null || asset == null || field == null)
            {
                plan.Blockers.Add("There is nothing open to rename.");
                return plan;
            }

            if (string.IsNullOrEmpty(oldId))
            {
                plan.Blockers.Add(
                    "This " + field.Thing + " has no name yet, so there is nothing to rename. " +
                    "Type one in and it is simply written.");
                return plan;
            }

            string trimmed = (newId ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                plan.Blockers.Add(
                    "A " + field.Thing + " with no name cannot be pointed at by anything, so an " +
                    "empty name is not offered.");
                return plan;
            }

            if (string.Equals(trimmed, oldId, StringComparison.Ordinal))
            {
                plan.Blockers.Add("That is the name it already has.");
                return plan;
            }

            AddNameBlockers(plan, trimmed);
            plan.Edges.AddRange(
                DimensionIdReferences.Find(template, oldId, alsoOutsideThisPack));

            // The places that hold the asset itself. Added to the same list so nothing downstream
            // has to know there are two kinds of edge; each row says which it is, and the rename
            // skips them because there is no name in them to rewrite.
            plan.Edges.AddRange(
                DimensionIdReferences.FindHolders(
                    template, asset, alsoOutsideThisPack, field.ContainerProperty));

            AddDerivedEdges(plan, oldId, trimmed, alsoOutsideThisPack);
            plan.Claimants.AddRange(
                DimensionIdReferences.OtherThingsCalled(template, oldId, asset));
            AddEdgeBlockersAndWarnings(plan);
            plan.GhostFiles.AddRange(DimensionGeneratedFootprint.Of(template, field, oldId));
            return plan;
        }

        /// <summary>
        /// Finds the places naming a second id this one is built into, and says what each becomes.
        /// </summary>
        /// <remarks>
        /// Six families were measured invisible before this: a plant's seed and grown form, a
        /// monster's elite, a boss's summoning circle and map marker, a block's two block items, a
        /// dish's two better qualities and an item's golden form. The rename said "nothing else
        /// pointed at it" and the reference was left naming an object nobody builds — a silence,
        /// which is the failure this whole class is against.
        /// </remarks>
        private static void AddDerivedEdges(
            DimensionIdRenamePlan plan,
            string oldId,
            string newId,
            bool alsoOutsideThisPack)
        {
            Func<string, string>[] derived = plan.Field.DerivedIds;
            if (derived.Length == 0)
            {
                return;
            }

            // A BLOCK'S SECOND IDS FOLLOW ITS IDENTITY, NOT ITS LABEL, and which of those changes
            // is the creator's answer rather than something a plan can know: "keep the identity"
            // pins the token it has now and the two block items keep their names, "start fresh"
            // lets it go and they take the new one. Rows would be right for one answer and wrong
            // for the other, so what this says instead is the fact, and lets the buttons mean what
            // they say.
            if (plan.Field.PinProperty.Length > 0)
            {
                string was = string.Empty;
                string becomes = string.Empty;
                for (int i = 0; i < derived.Length; i++)
                {
                    was = Join(was, derived[i](DimensionObjectNamespace.LocalIdOf(oldId)));
                    becomes = Join(becomes, derived[i](DimensionObjectNamespace.LocalIdOf(newId)));
                }

                plan.Warnings.Add(
                    "The two items this block makes are named after its identity, not after its " +
                    "label: " + was + ". Keep the identity and they stay exactly as they are. " +
                    "Start fresh and they become " + becomes + ", and every recipe, drop and " +
                    "scene that types one of the old pair is left naming an item nobody builds. " +
                    "Nothing here rewrites those, because which of the two you meant is the " +
                    "button you are about to press.");
                return;
            }

            for (int i = 0; i < derived.Length; i++)
            {
                string was = derived[i](DimensionObjectNamespace.LocalIdOf(oldId));
                string becomes = derived[i](DimensionObjectNamespace.LocalIdOf(newId));
                plan.Edges.AddRange(DimensionIdReferences.FindDerived(
                    plan.Template,
                    was,
                    becomes,
                    "built out of this " + plan.Field.Thing + "'s name",
                    alsoOutsideThisPack));
            }
        }

        /// <summary>The value stored in the name field today.</summary>
        internal static string ReadId(Object asset, DimensionIdentityField field)
        {
            if (asset == null || field == null)
            {
                return string.Empty;
            }

            SerializedProperty property =
                new SerializedObject(asset).FindProperty(field.Property);
            return property == null || property.propertyType != SerializedPropertyType.String
                ? string.Empty
                : property.stringValue ?? string.Empty;
        }

        private static void AddNameBlockers(DimensionIdRenamePlan plan, string newId)
        {
            // The silent vanilla redirect. Asked of the binder rather than of the validator's id
            // universe: the universe asks the mod's own list first, so it would answer "fine" for
            // exactly the name that breaks, and the emitter would then register the recipe against
            // a different object with nothing said.
            //
            // ASKED OF THE LOCAL HALF, which is the whole point of the check. Asked of the string
            // as typed — and ids are created qualified by default — the one form a creator actually
            // types, "MyMod:CopperShovel", walks straight past a refusal that catches only
            // "CopperShovel". Owns() keys on the local half and rejects anything vanilla owns, so
            // the qualified form breaks in exactly the same way.
            //
            // AND ONLY FOR THE NAMES THAT BECOME OBJECTS. A loot table, a recipe, a biome, a
            // scene, a dungeon, a generation step, a portal rule, a room filling, a game setup and
            // a layout are never resolved against ObjectID, so a refusal there would be a block
            // delivered with a reason that is not true of it — the enum happens to contain Wood,
            // Slime, Bomb and Lantern, and a loot table may be called any of them.
            string local = DimensionObjectNamespace.LocalIdOf(newId);
            if (plan.Field.BecomesAGameObject &&
                DimensionObjectBinder.Vanilla(local) != ObjectID.None)
            {
                plan.Blockers.Add(
                    "The game already has an object called \"" + local + "\". Take that name and " +
                    "every recipe, drop, shot and trader row that says it would quietly start " +
                    "meaning the game's one, while this " + plan.Field.Thing + " still ships under " +
                    "a name nothing can reach. Nothing in the editor would say a word about it, " +
                    "so it is refused here.");
            }

            if (plan.Field.BecomesAGameObject &&
                DimensionObjectBinder.Vanilla(
                    DimensionObjectNamespace.LocalIdOf(plan.OldId)) != ObjectID.None)
            {
                plan.Warnings.Add(
                    "\"" + plan.OldId + "\" is also the name of one of the game's own objects, so " +
                    "everything pointing at it has been meaning the GAME's one, not yours. After " +
                    "this rename they mean yours. That is a change to how the mod plays, not a " +
                    "tidy-up.");
            }

            AddCollisionBlockers(plan, newId);

            // Two names on one asset must not become one name on one asset: the runtime files
            // scenes under templateId with a dictionary, and the second one in never registers.
            List<DimensionIdentityField> siblings = DimensionIdentityCatalog.Of(plan.Asset);
            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i] == plan.Field)
                {
                    continue;
                }

                if (string.Equals(ReadId(plan.Asset, siblings[i]), newId, StringComparison.Ordinal))
                {
                    plan.Blockers.Add(
                        "This " + plan.Field.Thing + "'s other name — " + siblings[i].Label +
                        " — is already \"" + newId + "\". Two of its names being the same string " +
                        "makes it impossible to tell afterwards which of them anything meant.");
                }
            }
        }

        private static string Join(string list, string next)
        {
            return list.Length == 0 ? "\"" + next + "\"" : list + " and \"" + next + "\"";
        }

        private static void AddCollisionBlockers(DimensionIdRenamePlan plan, string newId)
        {
            // Blocks compare TOKENS, not names: "Eerie Stone", "Eerie-Stone" and "Eerie  Stone!"
            // are all one identity, so two of them in one pack is a collision that a plain string
            // comparison would wave through.
            if (plan.Asset is DimensionTilesetAsset)
            {
                string token = DimensionTilesetAsset.IdentityTokenFor(newId);
                DimensionTilesetAsset[] blocks = plan.Template.Tilesets;
                for (int i = 0; blocks != null && i < blocks.Length; i++)
                {
                    if (blocks[i] == null || blocks[i] == plan.Asset)
                    {
                        continue;
                    }

                    if (string.Equals(blocks[i].IdentityToken, token, StringComparison.Ordinal))
                    {
                        plan.Blockers.Add(
                            "\"" + blocks[i].BlockName + "\" is already that block. Spacing, " +
                            "punctuation and capitals are not part of a block's identity, so " +
                            "\"" + newId + "\" and \"" + blocks[i].BlockName + "\" are the same " +
                            "block as far as a saved world is concerned.");
                        return;
                    }
                }

                return;
            }

            string local = DimensionObjectNamespace.LocalIdOf(newId);

            // The binder's own walk, so this asks the question the emitter asks. It covers
            // everything that becomes an object in the game — items, creatures, workbenches,
            // plants, containers and the rest.
            if (Contains(DimensionGeneratedObjectIds.Collect(plan.Template), local))
            {
                plan.Blockers.Add(
                    "Something else in this dimension is already called \"" + local + "\". Two " +
                    "things sharing a name cannot be told apart afterwards, so this is refused " +
                    "rather than warned about.");
                return;
            }

            // A switched-off asset still owns its name. Colliding with one does not fail today; it
            // fails the day the creator ticks it back on, by which time nothing connects the two
            // events.
            if (Contains(DimensionGeneratedObjectIds.SwitchedOff(plan.Template), local))
            {
                plan.Blockers.Add(
                    "Something switched off in this dimension is called \"" + local + "\". It is " +
                    "not being built right now, so nothing would break today — it would break the " +
                    "day you switch it back on, and nothing would connect the two.");
                return;
            }

            // And the same question again over the things that never become an object: a recipe, a
            // loot table, a biome, a scene, a generation step. None of those is in the binder's
            // walk, because the binder is about objects — so without this half, renaming one loot
            // table onto another's id would go through in silence.
            List<Object> pack = DimensionIdReferences.AssetsOf(plan.Template);
            for (int i = 0; i < pack.Count; i++)
            {
                if (pack[i] == plan.Asset || pack[i].GetType() != plan.Asset.GetType())
                {
                    continue;
                }

                if (string.Equals(
                        DimensionObjectNamespace.LocalIdOf(ReadId(pack[i], plan.Field)),
                        local,
                        StringComparison.Ordinal))
                {
                    plan.Blockers.Add(
                        "Another " + plan.Field.Thing + " in this dimension is already called \"" +
                        local + "\". Two of them sharing a name cannot be told apart afterwards.");
                    return;
                }
            }
        }

        private static bool Contains(List<string> ids, string local)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(
                        DimensionObjectNamespace.LocalIdOf(ids[i]),
                        local,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddEdgeBlockersAndWarnings(DimensionIdRenamePlan plan)
        {
            int foreign = 0;
            bool archived = false;
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].OutsideThisPack)
                {
                    foreign++;
                }

                // The layout archive is a record of what a published layout WAS, kept so a world
                // made under it still generates. Rewriting it makes the project consistent and the
                // archive a lie; refusing keeps the archive honest and leaves a name in it that
                // points at nothing. That is an owner's decision about which of two truths matters
                // more, not something this tool gets to settle, so it declines and says so.
                if (plan.Edges[i].PropertyPath.StartsWith(
                        "publishedVersions.", StringComparison.Ordinal))
                {
                    archived = true;
                }
            }

            if (archived)
            {
                plan.Blockers.Add(
                    "A published layout has this name written into it. That record exists so a " +
                    "world made under that layout can still be generated — rewrite it and the " +
                    "record no longer says what was published, leave it and it names something " +
                    "that no longer exists. Which of those is right has not been decided, so " +
                    "renaming is refused here rather than guessed at.");
            }

            if (foreign > 0)
            {
                plan.Warnings.Add(
                    foreign + (foreign == 1 ? " place" : " places") + " in another dimension in " +
                    "this project also names it. Those are listed separately and are left alone " +
                    "unless you say otherwise.");
            }

            // NOT DONE, SAID HERE RATHER THAN NOWHERE. The words a player reads are written into
            // the mod's text file keyed on the generated object name, and the only rows a generate
            // ever retires are ones left by an older way of spelling keys
            // (DimensionLocalizationCsv.cs:70-75). Nothing in the game reads the dead pair, so this
            // is a warning and not a refusal — but a rename that says nothing about it is a rename
            // that quietly grows the file every time it is used.
            if (plan.Field.BecomesAGameObject)
            {
                plan.Warnings.Add(
                    "The words a player reads for this are filed in your mod's text file under the " +
                    "old name, and a rename does not take those two lines away. Nothing reads them " +
                    "afterwards; the file simply keeps them.");
            }

            if (plan.Field.SavedWorldsRememberIt)
            {
                plan.Warnings.Add(
                    "A world somebody has already played remembers this " + plan.Field.Thing +
                    " by this name. Rewriting the project cannot reach those worlds: what is " +
                    "already in the ground there stops matching.");
            }
        }

        /// <summary>
        /// Writes the rename: every ticked place first, the name itself last, all inside one undo.
        /// </summary>
        /// <param name="plan">A plan whose <see cref="DimensionIdRenamePlan.CanGo"/> is true.</param>
        /// <param name="rewrite">
        /// The places to rewrite. The identity row is written whether or not it is in here.
        /// </param>
        /// <param name="report">What was done, in one sentence.</param>
        /// <returns>False when the plan refused, and nothing was touched.</returns>
        internal static bool Apply(
            DimensionIdRenamePlan plan,
            IReadOnlyList<DimensionIdReference> rewrite,
            out string report)
        {
            return Apply(plan, rewrite, DimensionRenameIdentityAnswer.LeaveThePinAlone, out report);
        }

        /// <summary>
        /// The same, saying what should happen to a block's pinned identity.
        /// </summary>
        /// <param name="identity">
        /// What to do with the pin BEFORE the name lands: keep the block the tiles in a saved world
        /// already are, start it fresh as a new block, or leave the pin exactly as it is for
        /// everything that has no pin.
        /// </param>
        internal static bool Apply(
            DimensionIdRenamePlan plan,
            IReadOnlyList<DimensionIdReference> rewrite,
            DimensionRenameIdentityAnswer identity,
            out string report)
        {
            if (plan == null || !plan.CanGo)
            {
                report = plan == null || plan.Blockers.Count == 0
                    ? "There was nothing to rename."
                    : plan.Blockers[0];
                return false;
            }

            List<Object> touched = new List<Object>();
            for (int i = 0; rewrite != null && i < rewrite.Count; i++)
            {
                if (rewrite[i].Asset != null && !touched.Contains(rewrite[i].Asset))
                {
                    touched.Add(rewrite[i].Asset);
                }
            }

            if (!touched.Contains(plan.Asset))
            {
                touched.Add(plan.Asset);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Rename " + plan.OldId);
            int group = Undo.GetCurrentGroup();
            Undo.RecordObjects(touched.ToArray(), "Rename " + plan.OldId);

            int written = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                // INSIDE THE GROUP, and it was not. The pin was written before Undo's group was
                // even opened, so the one thing the card promises about a rename — that Ctrl+Z puts
                // every touched asset back in one step — was untrue of the identity token.
                WriteTheIdentityAnswer(plan, identity);

                for (int i = 0; rewrite != null && i < rewrite.Count; i++)
                {
                    if (plan.IsTheIdentityRow(rewrite[i]))
                    {
                        continue;
                    }

                    if (Write(rewrite[i], plan.NewId))
                    {
                        written++;
                    }
                }

                // Last, always. See the class remarks.
                WriteIdentity(plan);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            Undo.CollapseUndoOperations(group);

            report = "Renamed the " + plan.Field.Thing + " to \"" + plan.NewId + "\"" +
                     (written == 0
                         ? " — nothing else pointed at it."
                         : " and rewrote " + written +
                           (written == 1 ? " place that pointed at it." : " places that pointed at it."));
            return true;
        }

        /// <summary>
        /// Writes one place, in the form it was already written in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The property is re-read through a fresh <c>SerializedObject</c> rather than kept from
        /// the plan, and its current value is checked against the plan's, so a plan gone stale
        /// under an edit skips the row instead of overwriting whatever is there now.
        /// </para>
        /// <para>
        /// THE FORM IS PRESERVED IN BOTH DIRECTIONS, not just one. Preserved one way — the
        /// qualified branch putting the row's own qualifier back while the plain branch writes the
        /// new name exactly as typed — typing "MyMod:Sword" over "Blade" puts a qualifier into a
        /// field that never carried one. A field written unqualified stays unqualified, which is
        /// what the class remarks say this does.
        /// </para>
        /// <para>
        /// A DERIVED ROW BECOMES ITS OWN NEW VALUE, not the new name: "EmberSeed" has to become
        /// "AshSeed". The row carries that value because only the catalog knows how the second id
        /// is built.
        /// </para>
        /// </remarks>
        private static bool Write(DimensionIdReference edge, string newId)
        {
            if (edge.Asset == null || edge.Hold != DimensionIdHold.TheName)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(edge.Asset);
            SerializedProperty property = serialized.FindProperty(edge.PropertyPath);
            if (property == null ||
                property.propertyType != SerializedPropertyType.String ||
                !string.Equals(property.stringValue, edge.RawValue, StringComparison.Ordinal))
            {
                return false;
            }

            string local = edge.BecomesLocal.Length > 0
                ? edge.BecomesLocal
                : DimensionObjectNamespace.LocalIdOf(newId);
            property.stringValue = edge.Form == DimensionIdForm.Qualified
                ? DimensionObjectNamespace.Qualify(
                    DimensionObjectNamespace.ModOf(edge.RawValue), local)
                : local;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(edge.Asset);
            return true;
        }

        private static void WriteIdentity(DimensionIdRenamePlan plan)
        {
            SerializedObject serialized = new SerializedObject(plan.Asset);
            SerializedProperty property = serialized.FindProperty(plan.Field.Property);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            property.stringValue = plan.NewId;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(plan.Asset);
        }

        /// <summary>
        /// Pins a block's identity to what it is now, so the rename is a change of label only and
        /// tiles already in somebody's world stay this block.
        /// </summary>
        /// <remarks>
        /// Written BEFORE the name lands, because the token is derived from the name it is
        /// replacing. This is the tileset studio's own guard, reached from the shared rename so the
        /// two answers a block rename has always offered stay the two answers. Kept public because
        /// it is the readable name for what "keep the identity" means; <see cref="Apply"/> is where
        /// a rename should reach it from, so the write lands inside the same undo step as the rest.
        /// </remarks>
        internal static void PinIdentityFirst(DimensionIdRenamePlan plan)
        {
            DimensionTilesetAsset block = PinnableBlock(plan);
            if (block == null)
            {
                return;
            }

            WritePin(plan, block.IdentityToken);
        }

        /// <summary>
        /// Lets a block's identity go, so the renamed block is a NEW block rather than the old one
        /// under a new label.
        /// </summary>
        /// <remarks>
        /// THIS IS THE WHOLE DIFFERENCE BETWEEN THE TWO ANSWERS and it did not exist. "Rename and
        /// start fresh" only skipped the pin write, which does nothing at all to a block that was
        /// pinned by an earlier rename: the frozen token wins over the name
        /// (<c>DimensionTilesetAsset.IdentityToken</c>), so the creator pressed the button that
        /// promises a new block and got the same block under a third name — same tileset id, same
        /// ground and wall block items. Clearing the pin is what the tileset studio's own
        /// start-fresh path does.
        /// </remarks>
        internal static void StartTheIdentityFresh(DimensionIdRenamePlan plan)
        {
            if (PinnableBlock(plan) == null)
            {
                return;
            }

            WritePin(plan, string.Empty);
        }

        /// <summary>The block whose identity this plan can pin, or null when there is none.</summary>
        private static DimensionTilesetAsset PinnableBlock(DimensionIdRenamePlan plan)
        {
            if (plan == null || plan.Asset == null || plan.Field == null ||
                plan.Field.PinProperty.Length == 0)
            {
                return null;
            }

            return plan.Asset as DimensionTilesetAsset;
        }

        private static void WritePin(DimensionIdRenamePlan plan, string token)
        {
            SerializedObject serialized = new SerializedObject(plan.Asset);
            SerializedProperty pin = serialized.FindProperty(plan.Field.PinProperty);
            if (pin == null || pin.propertyType != SerializedPropertyType.String ||
                string.Equals(pin.stringValue, token, StringComparison.Ordinal))
            {
                return;
            }

            pin.stringValue = token;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(plan.Asset);
        }

        private static void WriteTheIdentityAnswer(
            DimensionIdRenamePlan plan,
            DimensionRenameIdentityAnswer identity)
        {
            if (identity == DimensionRenameIdentityAnswer.KeepTheIdentity)
            {
                PinIdentityFirst(plan);
            }
            else if (identity == DimensionRenameIdentityAnswer.StartFresh)
            {
                StartTheIdentityFresh(plan);
            }
        }

        /// <summary>
        /// Runs the sweep again for the OLD name and names anything that still says it.
        /// </summary>
        /// <remarks>
        /// The point of this is that it can fail. A rename that reports success and leaves three
        /// references behind is worse than one that refuses, because the creator stops looking.
        /// The identity row is excluded — after a successful rename the asset no longer holds the
        /// old name, so anything found here is a place that did not get written.
        /// </remarks>
        internal static List<DimensionIdReference> WhatStillSaysTheOldName(
            DimensionTemplateAsset template,
            string oldId,
            bool alsoOutsideThisPack)
        {
            return DimensionIdReferences.Find(template, oldId, alsoOutsideThisPack);
        }

        /// <summary>
        /// The places the creator asked to have rewritten that still say the old name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A NARROWER QUESTION THAN <see cref="WhatStillSaysTheOldName"/>, and the one a report
        /// after a rename should be asking. The wide sweep counts the rows that were deliberately
        /// left alone — a creature whose <c>objectId</c> matches its <c>mobId</c> ships unticked on
        /// purpose, and so does every row in another dimension — so a rename that did exactly what
        /// was asked ended with "1 place still says Slime. Run the rename again and it will
        /// finish", which re-running cannot do: the identity now holds the new name, so there is no
        /// old-to-new left to run.
        /// </para>
        /// <para>
        /// Asked over the same reach the writes were, so ticking a foreign row and then checking
        /// only this pack cannot report it as missed either.
        /// </para>
        /// </remarks>
        internal static List<DimensionIdReference> WhatWasAskedForAndDidNotHappen(
            DimensionTemplateAsset template,
            string oldId,
            IReadOnlyList<DimensionIdReference> asked,
            bool alsoOutsideThisPack)
        {
            List<DimensionIdReference> missed = new List<DimensionIdReference>();
            if (asked == null || asked.Count == 0)
            {
                return missed;
            }

            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < asked.Count; i++)
            {
                if (asked[i].Hold == DimensionIdHold.TheName && asked[i].BecomesLocal.Length == 0)
                {
                    wanted.Add(KeyOf(asked[i]));
                }
            }

            List<DimensionIdReference> left =
                DimensionIdReferences.Find(template, oldId, alsoOutsideThisPack);
            for (int i = 0; i < left.Count; i++)
            {
                if (wanted.Contains(KeyOf(left[i])))
                {
                    missed.Add(left[i]);
                }
            }

            return missed;
        }

        private static string KeyOf(DimensionIdReference edge)
        {
            return (edge.Asset == null ? 0 : edge.Asset.GetInstanceID()) + "/" + edge.PropertyPath;
        }
    }
}
