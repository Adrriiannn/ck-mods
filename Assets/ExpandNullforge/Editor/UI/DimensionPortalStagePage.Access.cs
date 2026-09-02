using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Who may walk through the portal on the open tab, and what it asks for first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These settings live on a portal access rule, which is its own asset beside the dimension.
    /// The dimension keeps a flat list of them and nothing points from a portal to its rule: the
    /// generator takes the FIRST enabled rule whose kind and dimension match, and quietly skips the
    /// rest. That is why this page never offers to add a rule to the list. It shows the one rule
    /// that owns the portal on the open tab, offers to build that one rule if it is missing, and
    /// says out loud when other rules of the same kind are being ignored — three portals, three
    /// visible rules, nothing unowned.
    /// </para>
    /// <para>
    /// Everything hidden here is hidden for a reason and the reasons differ. Ids (rule, portal,
    /// presentation) are plumbing the framework writes for itself. The icon, effect and sound-cue
    /// ids are carried faithfully into the presentation record and then read by nothing, so
    /// offering them would be a promise the game does not keep — the portal's real activation
    /// sound is on the Sound card above. Whether the portal ships at all is the "Included in the
    /// mod" button at the top of the page, and which kind of portal a rule is comes from the tab
    /// it was found under.
    /// </para>
    /// </remarks>
    internal sealed partial class DimensionPortalStagePage
    {
        /// <summary>
        /// Benches the game already has, offered so nobody has to know how the game spells them.
        /// </summary>
        /// <remarks>
        /// Names, not numbers, and these are the game's own object names: the generator turns one
        /// of these into a number while it builds, and anything it does not recognise it treats as
        /// a bench from another mod and looks up while the world loads.
        /// </remarks>
        private static readonly string[][] VanillaCraftingStations =
        {
            new[] { "Wooden Workbench", "WoodenWorkBench" },
            new[] { "Tin Workbench", "TinWorkbench" },
            new[] { "Copper Workbench", "CopperWorkbench" },
            new[] { "Iron Workbench", "IronWorkBench" },
            new[] { "Scarlet Workbench", "ScarletWorkBench" },
            new[] { "Octarine Workbench", "OctarineWorkbench" },
            new[] { "Galaxite Workbench", "GalaxiteWorkbench" },
            new[] { "Solarite Workbench", "SolariteWorkbench" },
            new[] { "Jewelry Workbench", "JewelryWorkBench" },
            new[] { "Fishing Workbench", "FishingWorkBench" },
            new[] { "Cooking Pot", "CookingPot" },
            new[] { "Furnace", "Furnace" },
            new[] { "Copper Anvil", "CopperAnvil" },
            new[] { "Iron Anvil", "IronAnvil" },
            new[] { "Scarlet Anvil", "ScarletAnvil" },
            new[] { "Octarine Anvil", "OctarineAnvil" },
            new[] { "Galaxite Anvil", "GalaxiteAnvil" },
            new[] { "Solarite Anvil", "SolariteAnvil" },
        };

        /// <summary>
        /// Things in the game worth dropping a portal from, by the name a player knows them by and
        /// the name the game knows them by.
        /// </summary>
        /// <remarks>
        /// A short list on purpose. Anything at all can be typed into the row instead — the drop is
        /// written into whatever creature the name resolves to, including one from another mod —
        /// and a list trying to hold every creature in the game would go stale faster than it
        /// helped.
        /// </remarks>
        private static readonly string[][] VanillaDropTargets =
        {
            new[] { "Glurch the Abominous Mass", "SlimeBoss" },
            new[] { "Ghorm the Devourer", "BossLarva" },
            new[] { "Malugaz the Corrupted", "ShamanBoss" },
            new[] { "The Hive Mother", "LarvaHiveBoss" },
            new[] { "Azeos the Sky Titan", "BirdBoss" },
            new[] { "Omoroth the Sea Titan", "OctopusBoss" },
            new[] { "Ra-Akar the Sand Titan", "ScarabBoss" },
            new[] { "King Slime", "KingSlime" },
            new[] { "Ivy the Poisonous Mass", "PoisonSlimeBoss" },
            new[] { "Morpha the Aquatic Mass", "SlipperySlimeBoss" },
            new[] { "Igneous the Molten Mass", "LavaSlimeBoss" },
            new[] { "Caveling", "Caveling" },
            new[] { "Caveling Brute", "CavelingBrute" },
            new[] { "Caveling Gardener", "CavelingGardener" },
            new[] { "Larva", "Larva" },
            new[] { "Big Larva", "BigLarva" },
            new[] { "Slime Blob", "SlimeBlob" },
            new[] { "Mushroom Enemy", "MushroomEnemy" },
            new[] { "Crab", "CrabEnemy" },
            new[] { "Desert Brute", "DesertBrute" },
        };

        /// <summary>
        /// The places in the game's own world a portal can be grown in, by the name a player knows
        /// and the name the game's own biome list uses.
        /// </summary>
        /// <remarks>
        /// Curated rather than read straight off the game's list, and the omissions are the point.
        /// The list still holds a cut biome and the Great Wall itself, neither of which the world
        /// ever samples, so offering them would be a control that quietly does nothing. Custom
        /// biomes are not offered for the same reason: the Overworld only ever produces the game's
        /// own, so naming one here could never match a single cell.
        /// </remarks>
        private static readonly string[][] WorldBiomes =
        {
            new[] { "The Undergrounds", "Slime" },
            new[] { "The Clay Caves", "Larva" },
            new[] { "The Forgotten Ruins", "Stone" },
            new[] { "Azeos' Wilderness", "Nature" },
            new[] { "The Sunken Sea", "Sea" },
            new[] { "The Desert of Beginnings", "Desert" },
            new[] { "The Shimmering Frontier", "Crystal" },
            new[] { "The Passage", "Passage" },
            new[] { "Breaker's Reach", "Excavation" },
        };

        /// <summary>The rule the open tab is editing, and the studio's view of it.</summary>
        private DimensionPortalAccessRuleAsset accessRule;
        private SerializedObject serializedAccessRule;

        private DimensionPortalAccessRuleAsset returnRule;
        private SerializedObject serializedReturnRule;

        /// <summary>
        /// The cards for the portal on the open tab, then the small one for the way back out.
        /// </summary>
        internal void AddAccessCards(DimensionPortalAppearanceStudio studio)
        {
            if (settingsHost == null || template == null)
            {
                return;
            }

            bool instant = studio != null && studio.InstantPortalMode;
            DimensionPortalAccessKind kind = instant
                ? DimensionPortalAccessKind.InventoryItem
                : DimensionPortalAccessKind.PlacedPortal;

            // The rule the game will really use comes first. Only when nothing owns the portal does
            // the page fall back to any rule of that kind — otherwise a dimension whose first rule
            // is switched off would show settings that change nothing while a later, live rule
            // quietly ran the portal.
            DimensionPortalAccessRuleAsset owner =
                DimensionPortalRuleOwnership.FindForTemplate(template, kind);
            accessRule = owner ?? DimensionPortalRuleOwnership.FindAnyOfKind(template, kind);
            serializedAccessRule = accessRule == null ? null : new SerializedObject(accessRule);

            // One host for every card that edits the way-in rule. Edits to a rule are not edits to
            // the portal's artwork, and the page's blanket listener cannot tell the difference — so
            // they are caught here, one element above the cards, before they can reach it and be
            // counted as unsaved artwork.
            VisualElement accessHost = new VisualElement();
            settingsHost.Add(accessHost);

            VisualElement group = DimensionsApiControls.Group("Access", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);
            accessHost.Add(group);

            if (accessRule == null)
            {
                body.Add(BuildMissingRuleState(kind, instant));
                settingsHost.Add(BuildReturnCard());
                return;
            }

            AddOwnershipStatus(body, kind, instant);
            AddOpeningTheWay(body);
            accessHost.Add(BuildOfferingCard(instant));
            accessHost.Add(BuildArrivalCard());
            accessHost.Add(BuildHowPlayersGetItCard(instant));
            StopRuleEditsAtTheirCard(accessHost);

            settingsHost.Add(BuildReturnCard());
        }

        /// <summary>
        /// What the page shows when a dimension has no rule for the portal on this tab at all.
        /// </summary>
        /// <remarks>
        /// One button that builds exactly one rule, with the same defaults a brand-new dimension is
        /// born with. Not an "add" that could be pressed twice: a second rule of the same kind is
        /// never reached by the generator, so offering one would only build something inert.
        /// </remarks>
        private VisualElement BuildMissingRuleState(DimensionPortalAccessKind kind, bool instant)
        {
            Button prepare = DimensionsApiControls.PrimaryButton(
                "Prepare this portal's rules",
                () => PrepareRule(kind));
            return DimensionsApiControls.EmptyState(
                instant
                    ? "This dimension has no rule for its item portal"
                    : "This dimension has no rule for its placed portal",
                "The rule is where the portal's waiting time, its offering and how a player comes " +
                "by it are kept. Building one gives this portal the same settings a new dimension " +
                "starts with, and nothing else about the dimension changes.",
                prepare);
        }

        private void PrepareRule(DimensionPortalAccessKind kind)
        {
            DimensionFrameworkAuthoringAssetActionResult result =
                DimensionFrameworkAuthoringAssetUtility.EnsurePortalAccessRule(template, kind);
            if (report != null)
            {
                // Both outcomes, not only the failure. A creator who clicks "Build the rule" and
                // is told nothing cannot tell a rule that was made from a click that missed.
                report(
                    result.Message,
                    result.Executed ? MessageType.Info : MessageType.Warning);
            }

            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null)
            {
                studio.NotifyTemplateEdited();
            }

            DeferredRefresh();
        }

        /// <summary>
        /// Which rule owns the portal on this tab, and what is being ignored beside it.
        /// </summary>
        /// <remarks>
        /// The owner is worked out with the generator's own first-match walk, so this line can
        /// never claim a rule the game will not use. Three things can go wrong and each is said in
        /// full: the rule is left out of the build, the rule points at another dimension, or there
        /// is more than one enabled rule of this kind and only the first is real.
        /// </remarks>
        private void AddOwnershipStatus(
            VisualElement body,
            DimensionPortalAccessKind kind,
            bool instant)
        {
            string portalWords = instant ? "item portal" : "placed portal";
            string dimensionName = string.IsNullOrEmpty(template.DisplayName)
                ? template.DimensionId
                : template.DisplayName;

            DimensionPortalAccessRuleAsset owner =
                DimensionPortalRuleOwnership.FindForTemplate(template, kind);
            if (owner == accessRule)
            {
                body.Add(Note(
                    "These are the settings for the " + portalWords + " of " + dimensionName + ".",
                    false));
            }
            else if (!accessRule.Enabled)
            {
                body.Add(Note(
                    "This " + portalWords + " is left out of the build, so nothing below reaches " +
                    "the game yet. Use \"Not included\" at the top of the page to put it back in.",
                    true));
            }
            else
            {
                body.Add(Note(
                    "This rule leads to \"" + accessRule.ToDimensionId + "\", not to \"" +
                    template.DimensionId + "\", so the game builds no " + portalWords + " for this " +
                    "dimension at all. Point it back at this dimension to fix that.",
                    true));
                Button repair = DimensionsApiControls.GhostButton(
                    "Point it at this dimension",
                    RepairRuleDestination);
                repair.tooltip = "Sets the dimension this portal leads to back to " +
                                 template.DimensionId + ".";
                body.Add(repair);
            }

            List<DimensionPortalAccessRuleAsset> matches =
                DimensionPortalRuleOwnership.FindAllForTemplate(template, kind);
            if (matches.Count > 1 && owner != null)
            {
                string ownerName = string.IsNullOrEmpty(owner.DisplayName)
                    ? owner.name
                    : owner.DisplayName;
                body.Add(Note(
                    matches.Count + " rules for the " + portalWords + " are switched on, and only " +
                    "\"" + ownerName + "\" is used. The others make no portal and no recipe. " +
                    "Delete them, or point them at another dimension.",
                    true));
            }
        }

        private void RepairRuleDestination()
        {
            if (serializedAccessRule == null || template == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty destination = serializedAccessRule.FindProperty("toDimensionId");
            if (destination == null)
            {
                return;
            }

            destination.stringValue = template.DimensionId;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        // ---------------------------------------------------------- opening the way --

        private void AddOpeningTheWay(VisualElement body)
        {
            SerializedProperty mode = serializedAccessRule.FindProperty("activationMode");
            if (mode == null)
            {
                return;
            }

            List<string> choices = new List<string>
            {
                "After a short wait",
                "When an offering is given",
                "An offering, then a short wait",
                "Always open",
            };
            int current = Mathf.Clamp(mode.intValue, 0, choices.Count - 1);
            PopupField<string> picker = new PopupField<string>(choices, current);
            picker.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || serializedAccessRule == null)
                {
                    return;
                }

                serializedAccessRule.Update();
                SerializedProperty own = serializedAccessRule.FindProperty("activationMode");
                if (own == null || own.intValue == index)
                {
                    return;
                }

                own.intValue = index;
                serializedAccessRule.ApplyModifiedProperties();
                NotifyRuleEdited();
                // The offering card and the waiting time both appear and vanish with this
                // choice, so the column has to be redrawn rather than left showing a control
                // the rule no longer honours.
                DeferredRefresh();
            });
            body.Add(DimensionsApiControls.Field(
                "When it opens",
                "A short wait is the game's own portal behaviour. An offering holds the portal " +
                "shut until the items below are put into it. Always open never makes a player " +
                "wait at all.",
                picker));

            // Exactly the modes the rule's own cooldown is read in: the other two force it to
            // zero, so showing a number a player would never experience would be a lie.
            if (current == (int)DimensionPortalActivationMode.VanillaCooldown ||
                current == (int)DimensionPortalActivationMode.RequiredItemsThenCooldown)
            {
                body.Add(DimensionsApiControls.Bound(
                    serializedAccessRule,
                    "activationCooldownSeconds",
                    "The wait",
                    "How many seconds a player waits between one use of this portal and the next."));
            }

            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "promptText",
                "What a player reads",
                "The words on the prompt when a player stands at the open portal. Leave it empty " +
                "and the game writes \"Enter\" and the portal's name."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "lockedPromptText",
                "What a player reads before it opens",
                "The words on the prompt while the portal is still shut. Leave it empty and the " +
                "game says the portal is not active yet."));
        }

        // ------------------------------------------------------------- the way back --

        /// <summary>
        /// The portal inside the dimension that takes a player home.
        /// </summary>
        /// <remarks>
        /// Small on purpose, and it has no "included in the mod" of its own: a dimension a player
        /// cannot leave is never a feature, so the way out is always built. It is shown under both
        /// tabs because it belongs to the dimension rather than to either way in.
        /// </remarks>
        private VisualElement BuildReturnCard()
        {
            VisualElement group = DimensionsApiControls.Group("The way back", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            returnRule = DimensionPortalRuleOwnership.FindAnyOfKind(
                template,
                DimensionPortalAccessKind.GeneratedReturnPortal);
            if (returnRule == null)
            {
                Button prepare = DimensionsApiControls.PrimaryButton(
                    "Prepare the way back",
                    () => PrepareRule(DimensionPortalAccessKind.GeneratedReturnPortal));
                body.Add(DimensionsApiControls.EmptyState(
                    "This dimension has no rule for its return portal",
                    "Without one the framework still builds a way out, using its own wording and " +
                    "the middle of the dimension. Building the rule lets you change both.",
                    prepare));
                return group;
            }

            serializedReturnRule = new SerializedObject(returnRule);
            body.Add(DimensionsApiControls.Bound(
                serializedReturnRule,
                "promptText",
                "What a player reads",
                "The words on the prompt at the return portal. Leave it empty and the game says " +
                "to return to the core."));
            body.Add(DimensionsApiControls.Bound(
                serializedReturnRule,
                "fromLocalPosition",
                "Where it stands",
                "The spot inside the dimension the return portal is built at, counted in tiles " +
                "from the middle of the dimension."));
            body.Add(DimensionsApiControls.Bound(
                serializedReturnRule,
                "toLocalPosition",
                "Where a player lands",
                "The spot in the world outside a player steps back out at."));
            body.Add(DimensionsApiControls.Bound(
                serializedReturnRule,
                "interactable",
                "Can be used",
                "Off, the return portal stands there and refuses. An arena arms its own way out " +
                "when the fight is won; anywhere else this should stay on, or players are stuck."));
            body.Add(Note(
                "The way back is always built, so it has no \"included in the mod\" of its own.",
                false));

            StopRuleEditsAtTheirCard(group, true);
            return group;
        }
    }
}
