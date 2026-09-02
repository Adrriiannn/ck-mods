using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The card at the foot of every thing's page: what uses it, what it is called, changing its
    /// name, and getting rid of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY IT IS ONE CARD AND NOT FOUR. "What uses this", "rename" and "delete" are the same
    /// question asked three ways, and the answer to all three is the same list. Splitting them
    /// would mean building the list three times and, worse, letting a creator press delete on a
    /// page that never showed them the list.
    /// </para>
    /// <para>
    /// THE NAME IS EDITED INTO A DRAFT. Every id field in the studio is bound straight to the
    /// property, which means every keystroke is a committed rename: typing "EmberBolt" over "Blade"
    /// passes the asset through "B", "Bl", "Bla", each a real serialized value a generate could
    /// pick up. Nothing else here works on top of a live-bound name field, so the draft comes
    /// first.
    /// </para>
    /// <para>
    /// AND RENAMING IS OFFERED SECOND. For nearly every kind of thing here the name a player reads
    /// is a separate field that costs nothing to change, and that is what somebody who says
    /// "rename" almost always means. So the card offers that first, plainly, and treats changing
    /// the id as the rarer, guarded act it is.
    /// </para>
    /// </remarks>
    internal static class DimensionIdentityCard
    {
        /// <summary>How many rows of a long list are shown before it is folded.</summary>
        private const int ShownBeforeFolding = 6;

        /// <summary>
        /// Builds the card for one thing, or null when this kind of thing has no name this
        /// framework knows how to rename.
        /// </summary>
        /// <param name="nameAlreadyOnThePage">
        /// True when a card above already offers <c>displayName</c>. It usually does, and drawing a
        /// second editor for one value on one page is the exact fault the "Everything else" fold
        /// was fixed for: two controls over one property, neither aware of the other.
        /// </param>
        internal static VisualElement Build(
            DimensionTemplateAsset template,
            Object asset,
            bool nameAlreadyOnThePage,
            System.Action refresh)
        {
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(asset);
            if (template == null || asset == null || names.Count == 0)
            {
                return null;
            }

            VisualElement card = DimensionsApiControls.Group(
                "Its name, and what points at it",
                "Where this thing's name is written, and what happens if you change it.");
            VisualElement body = DimensionsApiControls.BodyOf(card);

            SerializedObject serialized = new SerializedObject(asset);
            bool hasDisplayName = serialized.FindProperty("displayName") != null;
            if (hasDisplayName && !nameAlreadyOnThePage)
            {
                body.Add(DimensionsApiControls.Bound(
                    serialized,
                    "displayName",
                    "What it is called",
                    "The name a player reads. Changing it costs nothing and breaks nothing — " +
                    "nothing in your dimension points at a thing by this."));
            }
            else if (hasDisplayName)
            {
                // The commonest thing somebody means by "rename". Said here, where they came
                // looking, and pointing back at the free answer rather than repeating the control.
                body.Add(Note(
                    "To change what a player calls this, edit its name above — that costs nothing " +
                    "and breaks nothing. The id below is a different thing: it is what the rest of " +
                    "your dimension types when it means this one.",
                    false));
            }

            // ONE DELETE BUTTON PER THING, not one per name. A scene has two names and got two
            // "Delete this scene" buttons, and the second planned against templateId — a string
            // nothing points at — so it printed "Nothing points at it." and deleted a scene that
            // boss arenas and biome pools did point at. The button belongs to the name other
            // content uses, which is the one already marked InboundNamesUseIt.
            int deleteOn = -1;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].InboundNamesUseIt && names[i].ContainerProperty.Length > 0 &&
                    names[i].DeleteIsOfferedHere)
                {
                    deleteOn = i;
                    break;
                }
            }

            for (int i = 0; i < names.Count; i++)
            {
                body.Add(BuildNameSection(template, asset, names[i], i == deleteOn, refresh));
            }

            // AN UNDONE RENAME HAS TO REDRAW THE PAGE. Nothing in the studio listens for undo, so
            // pressing Ctrl+Z after a rename put the assets back and left the old text on screen —
            // a page saying one thing while the project says another, which is worse than either.
            // Registered on the card and dropped with it, so it cannot outlive the page it repaints.
            UnityEditor.Undo.UndoRedoCallback repaint = () => refresh();
            card.RegisterCallback<AttachToPanelEvent>(evt => UnityEditor.Undo.undoRedoPerformed += repaint);
            card.RegisterCallback<DetachFromPanelEvent>(evt => UnityEditor.Undo.undoRedoPerformed -= repaint);
            return card;
        }

        private static VisualElement BuildNameSection(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            bool carriesTheDeleteButton,
            System.Action refresh)
        {
            VisualElement section = new VisualElement();
            string id = DimensionIdRename.ReadId(asset, field);

            TextField draft = new TextField { value = id };
            draft.isDelayed = true;
            section.Add(DimensionsApiControls.Field(field.Label, field.Explains, draft));

            Toggle wider = new Toggle("Also look outside this pack") { value = false };
            wider.tooltip =
                "Loads every other dimension in this Unity project and looks there too. Off by " +
                "default because it reads every one of them, and because rewriting somebody " +
                "else's content is a bigger thing than rewriting your own.";
            section.Add(wider);

            VisualElement answers = new VisualElement();
            section.Add(answers);

            System.Action redraw = null;
            redraw = () =>
            {
                answers.Clear();
                string typed = draft.value ?? string.Empty;
                bool changing = !string.Equals(typed.Trim(), id, System.StringComparison.Ordinal) &&
                                typed.Trim().Length > 0;
                if (!changing)
                {
                    AddWhatUsesThis(answers, template, asset, field, id, wider.value);
                    if (carriesTheDeleteButton)
                    {
                        AddDeleteRow(answers, template, asset, field, wider.value, refresh);
                    }

                    return;
                }

                AddRenameAnswers(answers, template, asset, field, typed.Trim(), wider.value, draft, id, refresh);
            };

            DimensionsApiControls.AfterBinding(section, () =>
            {
                draft.RegisterValueChangedCallback(evt => redraw());
                wider.RegisterValueChangedCallback(evt => redraw());
            });

            redraw();
            return section;
        }

        // ------------------------------------------------------------- what uses this ---

        private static void AddWhatUsesThis(
            VisualElement host,
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            string id,
            bool wider)
        {
            if (string.IsNullOrEmpty(id))
            {
                host.Add(Note("This " + field.Thing + " has no name yet, so nothing can point at it.", true));
                return;
            }

            if (!field.InboundNamesUseIt)
            {
                host.Add(Note(
                    "Nothing you author points at a " + field.Thing + " by this name, so there is " +
                    "no list to show. It is the name the running game files this under.",
                    false));
                return;
            }

            List<DimensionIdReference> edges =
                DimensionIdReferences.Find(template, id, wider);

            // The slots that hold the asset itself. Without these the card told a creator "Nothing
            // uses this yet" about a loot table three monsters drop, because no string anywhere
            // says its name — the creatures hold the asset.
            edges.AddRange(DimensionIdReferences.FindHolders(
                template, asset, wider, field.ContainerProperty));

            List<DimensionIdReference> inbound = WithoutTheThingItself(edges, template, asset, field);
            if (inbound.Count == 0)
            {
                host.Add(Note("Nothing uses this yet.", false));
                return;
            }

            host.Add(Heading(
                inbound.Count == 1
                    ? "One place uses this"
                    : inbound.Count + " places use this"));
            AddRows(host, inbound, null, null);
            host.Add(Note(
                "Found by looking for the name itself rather than by knowing what each field " +
                "means, so a row here may be a different thing that happens to be called the " +
                "same. Read them before you act on them.",
                false));
        }

        // -------------------------------------------------------------------- rename ---

        private static void AddRenameAnswers(
            VisualElement host,
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            string typed,
            bool wider,
            TextField draft,
            string id,
            System.Action refresh)
        {
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, asset, field, typed, wider);

            for (int i = 0; i < plan.Blockers.Count; i++)
            {
                host.Add(Note(plan.Blockers[i], true));
            }

            for (int i = 0; i < plan.Warnings.Count; i++)
            {
                host.Add(Note(plan.Warnings[i], true));
            }

            // SAID BEFORE THE LIST, not after it, and on the screen where a write is about to
            // happen. The read-only list carries a grey note about the over-report; the rename
            // screen carried none, which is the wrong way round.
            if (plan.TheNameIsSharedInThisPack)
            {
                host.Add(Note(
                    SharedNameSentence(plan),
                    true));
            }

            List<DimensionIdReference> inbound = WithoutTheThingItself(plan.Edges, template, asset, field);
            Dictionary<string, Toggle> ticks = new Dictionary<string, Toggle>();
            Label counted = null;
            if (plan.CanGo && inbound.Count > 0)
            {
                // THE HEADING COUNTS THE TICKS, not the rows. It used to count every row in the
                // list, rows that cannot be ticked included, so "2 places would be rewritten"
                // stood over a rename that wrote one.
                counted = Heading(string.Empty);
                host.Add(counted);
                AddRows(host, inbound, ticks, plan);
            }
            else if (plan.CanGo)
            {
                host.Add(Note("Nothing points at it, so only the name itself changes.", false));
            }

            if (counted != null)
            {
                System.Action recount = () =>
                {
                    int ticked = 0;
                    foreach (KeyValuePair<string, Toggle> tick in ticks)
                    {
                        if (tick.Value.value)
                        {
                            ticked++;
                        }
                    }

                    counted.text = ticked == 0
                        ? "Nothing here would be rewritten"
                        : ticked == 1
                            ? "One place would be rewritten"
                            : ticked + " places would be rewritten";
                };

                foreach (KeyValuePair<string, Toggle> tick in ticks)
                {
                    tick.Value.RegisterValueChangedCallback(evt => recount());
                }

                recount();
            }

            for (int i = 0; i < plan.GhostFiles.Count; i++)
            {
                host.Add(Note(
                    "'" + plan.GhostFiles[i] + "' was built under the old name. A rename never " +
                    "deletes a file — undoing one cannot bring a deleted file back — so it stays " +
                    "where it is, and a mod ships every prefab in its folders whether anything " +
                    "points at them or not. Nothing takes it away on its own: a generator's own " +
                    "tidy-up only knows the names it is building now, not the one this was built " +
                    "under. Delete it in the Project view.",
                    true));
            }

            // THE TWO ANSWERS ARE ONLY OFFERED WHERE THEY ARE REAL. A block can be pinned to the
            // identity it already has, so "keep the identity" and "start fresh" are two different
            // outcomes and both belong on the page. A biome has no such pin: its id IS what a
            // played world stores, and there is nowhere to record that it used to be something
            // else. Offering the same two buttons there would put a promise on one of them that
            // nothing keeps, so a biome gets one button and the warning above it.
            bool canKeepTheIdentity = field.PinProperty.Length > 0;
            VisualElement buttons = DimensionsApiControls.ChipRow();
            if (plan.CanGo)
            {
                buttons.Add(DimensionsApiControls.PrimaryButton(
                    canKeepTheIdentity ? "Rename, keep the identity" : "Rename it",
                    () => DoRename(
                        plan,
                        inbound,
                        ticks,
                        canKeepTheIdentity
                            ? DimensionRenameIdentityAnswer.KeepTheIdentity
                            : DimensionRenameIdentityAnswer.LeaveThePinAlone,
                        wider,
                        refresh)));
                if (canKeepTheIdentity)
                {
                    buttons.Add(DimensionsApiControls.GhostButton(
                        "Rename and start fresh",
                        () => DoRename(
                            plan,
                            inbound,
                            ticks,
                            DimensionRenameIdentityAnswer.StartFresh,
                            wider,
                            refresh)));
                }
            }

            buttons.Add(DimensionsApiControls.GhostButton("Cancel", () =>
            {
                draft.SetValueWithoutNotify(id);
                refresh();
            }));
            host.Add(buttons);
        }

        private static void DoRename(
            DimensionIdRenamePlan plan,
            List<DimensionIdReference> inbound,
            Dictionary<string, Toggle> ticks,
            DimensionRenameIdentityAnswer identity,
            bool wider,
            System.Action refresh)
        {
            List<DimensionIdReference> chosen = new List<DimensionIdReference>();
            int leftOnPurpose = 0;
            for (int i = 0; i < inbound.Count; i++)
            {
                Toggle tick;
                if (!ticks.TryGetValue(KeyOf(inbound[i]), out tick) || tick.value)
                {
                    chosen.Add(inbound[i]);
                }
                else if (inbound[i].Hold == DimensionIdHold.TheName)
                {
                    leftOnPurpose++;
                }
            }

            // The pin is written inside Apply's undo group now, so "Ctrl+Z puts every touched
            // asset back in one step" is true of the identity token as well.
            string report;
            if (!DimensionIdRename.Apply(plan, chosen, identity, out report))
            {
                EditorUtility.DisplayDialog("That rename did not happen", report, "All right");
                refresh();
                return;
            }

            if (leftOnPurpose > 0)
            {
                report += " " + leftOnPurpose +
                          (leftOnPurpose == 1
                              ? " place you left unticked still says \""
                              : " places you left unticked still say \"") +
                          plan.OldId + "\".";
            }

            // The point of this is that it can fail. A rename that reports success and leaves
            // references behind is worse than one that refuses, because the creator stops looking.
            // Asked only of the places that were TICKED, and over the same reach they were written
            // over: counting the ones deliberately left made a rename that did exactly what was
            // asked report that it had not, and tell the creator to run it again — which cannot
            // finish it, because the identity already holds the new name.
            List<DimensionIdReference> missed = DimensionIdRename.WhatWasAskedForAndDidNotHappen(
                plan.Template, plan.OldId, chosen, wider);
            if (missed.Count > 0)
            {
                report += "\n\n" + missed.Count +
                          (missed.Count == 1
                              ? " place you ticked still says "
                              : " places you ticked still say ") +
                          "\"" + plan.OldId + "\", so this rename did not finish. Run it again — " +
                          "the name itself is written last, so old to new picks up exactly what " +
                          "was missed.";
                for (int i = 0; i < missed.Count && i < ShownBeforeFolding; i++)
                {
                    report += "\n  " + NameOf(missed[i].Asset) + " — " + missed[i].FieldName;
                }
            }

            DimensionContentValidationUtility.BumpChangeStamp();
            EditorUtility.DisplayDialog("Renamed", report, "All right");
            refresh();
        }

        // -------------------------------------------------------------------- delete ---

        private static void AddDeleteRow(
            VisualElement host,
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            bool wider,
            System.Action refresh)
        {
            if (field.ContainerProperty.Length == 0)
            {
                return;
            }

            Button remove = DimensionsApiControls.GhostButton(
                "Delete this " + field.Thing,
                () => AskAboutDeleting(template, asset, field, wider, refresh));
            remove.tooltip =
                "Says what points at it first. Deleting one of these in the Project view instead " +
                "leaves a hole in the list and every name that pointed at it pointing at nothing.";
            VisualElement row = DimensionsApiControls.ChipRow();
            row.Add(remove);
            host.Add(row);
        }

        private static void AskAboutDeleting(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            bool wider,
            System.Action refresh)
        {
            DimensionIdDeletePlan plan = DimensionIdDelete.Plan(template, asset, field, wider);
            if (!plan.CanGo)
            {
                EditorUtility.DisplayDialog(
                    "That cannot be deleted here", plan.Blockers[0], "All right");
                return;
            }

            // SAID EVERY TIME, on every branch. The file itself is deleted outside undo, and a
            // card that has just taught a creator that Ctrl+Z puts a rename back owes them this
            // one sentence before they press anything.
            const string CannotBeUndone =
                "\n\nThis cannot be undone. Ctrl+Z afterwards puts the pointers back, but the " +
                "file is gone and they would be pointing at nothing.";

            string message = "\"" + plan.Id + "\" and everything built from it go.";
            for (int i = 0; i < plan.GhostFiles.Count; i++)
            {
                message += "\n  " + plan.GhostFiles[i];
            }

            List<DimensionIdReference> inbound = WithoutTheThingItself(plan.Edges, template, asset, field);
            if (inbound.Count == 0)
            {
                message += "\n\nNothing points at it." + CannotBeUndone;
                if (!EditorUtility.DisplayDialog("Delete this " + field.Thing + "?", message, "Delete it", "Leave it"))
                {
                    return;
                }

                Finish(plan, false, inbound, refresh);
                return;
            }

            int holding = 0;
            int foreign = 0;
            for (int i = 0; i < inbound.Count; i++)
            {
                if (inbound[i].Hold == DimensionIdHold.TheThingItself)
                {
                    holding++;
                }

                if (inbound[i].OutsideThisPack)
                {
                    foreign++;
                }
            }

            message += "\n\n" + inbound.Count +
                       (inbound.Count == 1 ? " place points at it:" : " places point at it:");
            for (int i = 0; i < inbound.Count && i < ShownBeforeFolding; i++)
            {
                message += "\n  " + NameOf(inbound[i].Asset) + " — " + inbound[i].FieldName +
                           (inbound[i].Hold == DimensionIdHold.TheThingItself
                               ? " (holds it)"
                               : " (says \"" + inbound[i].RawValue + "\")");
            }

            if (inbound.Count > ShownBeforeFolding)
            {
                message += "\n  and " + (inbound.Count - ShownBeforeFolding) + " more.";
            }

            if (holding > 0)
            {
                message += "\n\n" + holding +
                           (holding == 1
                               ? " of those holds this " + field.Thing +
                                 " itself rather than naming it. Left alone it becomes a Missing " +
                                 "slot, and nothing in this framework checks for one."
                               : " of those hold this " + field.Thing +
                                 " itself rather than naming it. Left alone they become Missing " +
                                 "slots, and nothing in this framework checks for one.");
            }

            if (foreign > 0)
            {
                message += "\n\n" + foreign +
                           (foreign == 1
                               ? " place is in another dimension in this project and is left alone " +
                                 "either way."
                               : " places are in other dimensions in this project and are left " +
                                 "alone either way.");
            }

            message += "\n\nEmpty those places as well, or leave them pointing at something that " +
                       "is no longer there? Leaving them is right when something else is about to " +
                       "take the name." + CannotBeUndone;

            int answer = EditorUtility.DisplayDialogComplex(
                "Delete this " + field.Thing + "?",
                message,
                "Delete it and empty them",
                "Leave it alone",
                "Delete it and leave them");
            if (answer == 1)
            {
                return;
            }

            Finish(plan, answer == 0, inbound, refresh);
        }

        private static void Finish(
            DimensionIdDeletePlan plan,
            bool clear,
            List<DimensionIdReference> inbound,
            System.Action refresh)
        {
            string report;
            DimensionIdDelete.Apply(plan, clear, inbound, out report);
            DimensionContentValidationUtility.BumpChangeStamp();
            EditorUtility.DisplayDialog("Deleted", report, "All right");
            refresh();
        }

        // ------------------------------------------------------------------ the rows ---

        /// <summary>
        /// Everything that names it except the thing's own name for itself.
        /// </summary>
        /// <remarks>
        /// A second name on the same asset — a creature's <c>objectId</c> beside its <c>mobId</c> —
        /// stays in the list, because a creator who set both to the same string usually does want
        /// both changed. What it does NOT get is a tick already in it: rewriting a second identity
        /// is a second rename, and it is not the one that was asked for.
        /// </remarks>
        private static List<DimensionIdReference> WithoutTheThingItself(
            List<DimensionIdReference> edges,
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field)
        {
            List<DimensionIdReference> kept = new List<DimensionIdReference>();
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Asset == asset &&
                    string.Equals(edges[i].PropertyPath, field.Property, System.StringComparison.Ordinal))
                {
                    continue;
                }

                // THE DIMENSION'S OWN LIST IS NOT A POINTER AT IT, it is where it lives. Now that
                // the sweep sees held slots, that list is one of them, and leaving it in would put
                // "MyDimension — Global Loot Tables 3" at the top of every answer and mean
                // "Nothing points at it" could never be said. The delete takes it out of that list
                // on its own, every time, whichever answer is given.
                if (edges[i].Hold == DimensionIdHold.TheThingItself &&
                    edges[i].Asset == template &&
                    field.ContainerProperty.Length > 0 &&
                    edges[i].PropertyPath.StartsWith(
                        field.ContainerProperty, System.StringComparison.Ordinal))
                {
                    continue;
                }

                kept.Add(edges[i]);
            }

            return kept;
        }

        /// <summary>
        /// One sentence saying that this pack uses the same word twice, and what that does to the
        /// list below.
        /// </summary>
        private static string SharedNameSentence(DimensionIdRenamePlan plan)
        {
            string others = string.Empty;
            for (int i = 0; i < plan.Claimants.Count; i++)
            {
                string one = "a " + plan.Claimants[i].Thing + " called \"" +
                             plan.Claimants[i].Asset.name + "\"";
                others = others.Length == 0 ? one : others + ", and " + one;
            }

            return "This pack uses the word \"" + plan.OldId + "\" twice: this " +
                   plan.Field.Thing + ", and " + others + ". That is allowed — they are different " +
                   "kinds of thing — but it means a place that says \"" + plan.OldId + "\" may " +
                   "mean either of them, and this list is found by looking for the word. Rows " +
                   "that could mean the other one start unticked and say so. Tick the ones you " +
                   "know are this " + plan.Field.Thing + ".";
        }

        private static void AddRows(
            VisualElement host,
            List<DimensionIdReference> edges,
            Dictionary<string, Toggle> ticks,
            DimensionIdRenamePlan plan)
        {
            VisualElement mine = new VisualElement();
            VisualElement elsewhere = new VisualElement();
            VisualElement held = new VisualElement();
            int outside = 0;
            int holding = 0;

            for (int i = 0; i < edges.Count; i++)
            {
                VisualElement row = BuildRow(edges[i], ticks, plan);
                if (edges[i].Hold == DimensionIdHold.TheThingItself)
                {
                    holding++;
                    held.Add(row);
                }
                else if (edges[i].OutsideThisPack)
                {
                    outside++;
                    elsewhere.Add(row);
                }
                else
                {
                    mine.Add(row);
                }
            }

            host.Add(mine);
            if (holding > 0)
            {
                host.Add(Heading(
                    holding == 1
                        ? "One place holds it, and needs no change"
                        : holding + " places hold it, and need no change"));
                host.Add(Note(
                    "These do not name it — they hold the thing itself, so they go on holding it " +
                    "whatever it is called and a rename has nothing to do to them. They are here " +
                    "because they are places that point at it, and because deleting it is what " +
                    "would leave them holding nothing.",
                    false));
                host.Add(held);
            }

            if (outside == 0)
            {
                return;
            }

            host.Add(Heading(
                outside == 1
                    ? "One place in another dimension in this project"
                    : outside + " places in other dimensions in this project"));
            host.Add(Note(
                "Left alone unless you tick them. Rewriting another pack's content is a bigger " +
                "thing than rewriting your own, and a pack that is not open here cannot be " +
                "looked at at all.",
                true));
            host.Add(elsewhere);
        }

        private static VisualElement BuildRow(
            DimensionIdReference edge,
            Dictionary<string, Toggle> ticks,
            DimensionIdRenamePlan plan)
        {
            VisualElement row = DimensionsApiControls.ChipRow();
            string sentence = edge.Hold == DimensionIdHold.TheThingItself
                ? NameOf(edge.Asset) + " — " + edge.FieldName + " holds it"
                : NameOf(edge.Asset) + " — " + edge.FieldName + " says \"" + edge.RawValue + "\"";

            // A held row is grouped by what it holds rather than by whose pack it is in, so the one
            // fact that would otherwise be lost is put back on the row.
            if (edge.Hold == DimensionIdHold.TheThingItself && edge.OutsideThisPack)
            {
                sentence += ", in another dimension in this project";
            }
            if (edge.IsAName)
            {
                sentence += ", which is a name of its own";
            }

            if (edge.BuiltFrom.Length > 0)
            {
                sentence += ", " + edge.BuiltFrom;
                if (edge.BecomesLocal.Length > 0)
                {
                    sentence += ", and becomes \"" + edge.BecomesLocal + "\"";
                }
            }

            if (ticks != null)
            {
                // WHY THE TICK IS ASKED OF THE PLAN. "Is it safe to rewrite this by default" is a
                // question about the whole pack — whether anything else answers to the same word —
                // and the row cannot see that. It also comes back with the reason, which goes into
                // the row itself: a rule a creator can only find in the code is not a rule they
                // can act on.
                string because = string.Empty;
                bool ticked = plan != null && plan.StartsTicked(edge, out because);
                if (because.Length > 0)
                {
                    sentence += ". Left off: " + because;
                }

                Toggle tick = new Toggle(sentence) { value = ticked };
                tick.tooltip = edge.PropertyPath;
                if (edge.Hold == DimensionIdHold.TheThingItself)
                {
                    // Nothing to rewrite, so there is nothing to say yes to. Shown rather than
                    // hidden, because it IS a place that points at this.
                    tick.SetEnabled(false);
                }

                ticks[KeyOf(edge)] = tick;
                row.Add(tick);
            }
            else
            {
                row.Add(DimensionsApiControls.Chip(sentence));
            }

            Button go = DimensionsApiControls.GhostButton("Go to it", () =>
            {
                Selection.activeObject = edge.Asset;
                EditorGUIUtility.PingObject(edge.Asset);
            });
            go.tooltip = "Selects it in the project so you can look at it.";
            row.Add(go);
            return row;
        }

        private static string KeyOf(DimensionIdReference edge)
        {
            return (edge.Asset == null ? 0 : edge.Asset.GetInstanceID()) + "/" + edge.PropertyPath;
        }

        private static string NameOf(Object asset)
        {
            return asset == null ? "something that is gone" : asset.name;
        }

        private static Label Heading(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("dim-h3");
            return label;
        }

        private static Label Note(string text, bool warn)
        {
            Label note = new Label(text);
            note.AddToClassList("dim-note");
            if (warn)
            {
                note.AddToClassList("dim-note-warn");
            }

            return note;
        }
    }
}
