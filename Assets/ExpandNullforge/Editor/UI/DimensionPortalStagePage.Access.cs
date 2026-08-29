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
            if (!result.Executed)
            {
                Debug.LogWarning("[ExpandNullforge] " + result.Message);
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

        // -------------------------------------------------------------- the offering --

        /// <summary>
        /// What the portal asks to be given before it opens.
        /// </summary>
        /// <remarks>
        /// Only the placed portal ever shows an offering window: the generator puts the window and
        /// its inventory on the entry portal's entity and on nothing else, so an offering authored
        /// on the item portal would be asked for and never askable. The card says so rather than
        /// hiding, because a rule can be switched to an offering mode from either tab.
        /// </remarks>
        private VisualElement BuildOfferingCard(bool instant)
        {
            VisualElement group = DimensionsApiControls.Group("The offering", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!accessRule.UsesRequiredItems)
            {
                body.Add(Note(
                    "This portal asks for nothing. Choose an offering under \"When it opens\" to " +
                    "give it a window players put items into.",
                    false));
                return group;
            }

            if (instant)
            {
                body.Add(Note(
                    "A portal torn open by an item has no window to put anything into, so the " +
                    "items below are never asked for. Put the offering on the placed portal, or " +
                    "make the item itself the price.",
                    true));
                return group;
            }

            SerializedProperty items = serializedAccessRule.FindProperty("requiredItems");
            if (items == null || !items.isArray)
            {
                body.Add(Note("This rule has no offering list any more.", true));
                return group;
            }

            if (items.arraySize == 0)
            {
                body.Add(Note(
                    "Nothing is asked for yet, so the portal stays shut and no player can open it.",
                    true));
            }

            for (int i = 0; i < items.arraySize; i++)
            {
                body.Add(BuildOfferingRow(items.GetArrayElementAtIndex(i), i));
            }

            if (items.arraySize > 1)
            {
                body.Add(Note(
                    "Two rows asking for the same item become one slot holding the total.",
                    false));
            }

            Button add = DimensionsApiControls.GhostButton("Ask for something", AddOfferingRow);
            add.tooltip = "Adds another slot to the portal's window.";
            body.Add(add);
            return group;
        }

        private VisualElement BuildOfferingRow(SerializedProperty element, int index)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");

            SerializedProperty itemId = element.FindPropertyRelative("itemId");
            SerializedProperty displayName = element.FindPropertyRelative("displayName");
            SerializedProperty amount = element.FindPropertyRelative("amount");
            SerializedProperty consume = element.FindPropertyRelative("consumeOnTravel");
            SerializedProperty look = element.FindPropertyRelative("slotLook");
            SerializedProperty sprite = element.FindPropertyRelative("slotSprite");
            SerializedProperty dimness = element.FindPropertyRelative("slotDimness");
            if (itemId == null || amount == null)
            {
                return card;
            }

            card.Add(BoundField(
                itemId,
                "Item",
                "What goes in this slot, by the game's name for it — one of the game's own items, " +
                "or one of yours."));
            if (displayName != null)
            {
                card.Add(BoundField(
                    displayName,
                    "Called",
                    "The name a player is shown when the portal refuses them. Leave it empty and " +
                    "the item's own name is used."));
            }

            card.Add(BoundField(amount, "How many", "How many of the item the slot must hold."));
            if (consume != null)
            {
                card.Add(BoundField(
                    consume,
                    "Used up",
                    "On, the items are taken when a player travels and have to be put in again. " +
                    "Off, they stay in the portal and keep it open."));
            }

            if (look != null)
            {
                card.Add(BuildOfferingLookField(look));
                if (look.intValue == (int)Portals.DimensionPortalOfferingLook.CustomSprite &&
                    sprite != null)
                {
                    card.Add(BoundField(
                        sprite,
                        "Your picture",
                        "The hint drawn in the empty slot."));
                }

                if (dimness != null &&
                    look.intValue != (int)Portals.DimensionPortalOfferingLook.Mystery)
                {
                    card.Add(BoundField(
                        dimness,
                        "How faint",
                        "How faint the hint is, from 0 to 1. Leave it at 0 to keep the game's own " +
                        "dimming."));
                }
            }

            int removeAt = index;
            Button remove = DimensionsApiControls.GhostButton(
                "Stop asking for this",
                () => RemoveOfferingRow(removeAt));
            card.Add(remove);
            return card;
        }

        private VisualElement BuildOfferingLookField(SerializedProperty look)
        {
            List<string> choices = new List<string>
            {
                "The item, dimmed",
                "A black silhouette",
                "A picture of your own",
            };
            int current = Mathf.Clamp(look.intValue, 0, choices.Count - 1);
            PopupField<string> picker = new PopupField<string>(choices, current);
            string path = look.propertyPath;
            picker.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || serializedAccessRule == null)
                {
                    return;
                }

                serializedAccessRule.Update();
                SerializedProperty own = serializedAccessRule.FindProperty(path);
                if (own == null || own.intValue == index)
                {
                    return;
                }

                own.intValue = index;
                serializedAccessRule.ApplyModifiedProperties();
                NotifyRuleEdited();
                DeferredRefresh();
            });
            return DimensionsApiControls.Field(
                "The hint",
                "What the empty slot shows. The dimmed item comes with its tooltip, the " +
                "silhouette shows nothing at all, and a picture of your own can be anything.",
                picker);
        }

        private void AddOfferingRow()
        {
            if (serializedAccessRule == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty items = serializedAccessRule.FindProperty("requiredItems");
            if (items == null || !items.isArray)
            {
                return;
            }

            int index = items.arraySize;
            items.InsertArrayElementAtIndex(index);
            SerializedProperty added = items.GetArrayElementAtIndex(index);
            // Inserting copies the row before it, or zero-fills the first one. A zero amount is
            // read as one by the rule but shows as an empty box, and a copied item id would look
            // like a second slot while merging into the first, so the row is seeded by hand.
            added.FindPropertyRelative("itemId").stringValue = string.Empty;
            added.FindPropertyRelative("displayName").stringValue = string.Empty;
            added.FindPropertyRelative("amount").intValue = 1;
            added.FindPropertyRelative("consumeOnTravel").boolValue = true;
            added.FindPropertyRelative("slotLook").intValue =
                (int)Portals.DimensionPortalOfferingLook.GhostOfTheItem;
            added.FindPropertyRelative("slotSprite").objectReferenceValue = null;
            added.FindPropertyRelative("slotDimness").floatValue = 0f;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void RemoveOfferingRow(int index)
        {
            RemoveArrayElement("requiredItems", index);
        }

        // ------------------------------------------------------------ where you arrive --

        private VisualElement BuildArrivalCard()
        {
            VisualElement group = DimensionsApiControls.Group("Where you arrive", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "toLocalPosition",
                "Where a player lands",
                "The spot inside the dimension a player steps out at, counted in tiles from the " +
                "middle of the dimension."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "requireGeneratedAreaOnUse",
                "Wait for the ground",
                "On, the portal holds a player until the ground where they would land has been " +
                "built. Off, they can arrive before it exists."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "allowFallbackPositionOnUse",
                "Land nearby if that spot is blocked",
                "On, a player blocked from landing on that exact tile is put down on the nearest " +
                "free one. Off, the travel is refused instead."));
            return group;
        }

        // --------------------------------------------------------- how players get it --

        private VisualElement BuildHowPlayersGetItCard(bool instant)
        {
            VisualElement group = DimensionsApiControls.Group("How players get it", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(RedrawingToggle(
                "craftable",
                "Can be crafted",
                instant
                    ? "Whether the item that opens this portal can be made at a bench."
                    : "Whether the portal can be made at a bench. What it costs comes from the " +
                      "recipe you give it."));
            if (accessRule.Craftable)
            {
                body.Add(BuildCraftingStationRow());
            }

            body.Add(RedrawingToggle(
                "droppable",
                "Can be dropped",
                instant
                    ? "Whether creatures can drop the item that opens this portal."
                    : "Whether creatures can drop the portal."));
            if (accessRule.Droppable)
            {
                AddDropTargets(body);
            }

            if (instant)
            {
                AddItemPortalSettings(body);
            }
            else
            {
                AddFoundInTheWorld(body);
            }

            body.Add(Note(
                "Everything here reaches the game the next time you build the dimension.",
                false));
            return group;
        }

        /// <summary>
        /// The bench a portal is made at: a text field with the game's own benches behind a button.
        /// </summary>
        /// <remarks>
        /// Left as text on purpose. The bench can belong to another mod, and no list this page
        /// could carry would know that mod's name for it — so the menu fills the field in for the
        /// common cases and typing stays possible for the rest.
        /// </remarks>
        private VisualElement BuildCraftingStationRow()
        {
            SerializedProperty station = serializedAccessRule.FindProperty("craftingStationObjectId");
            if (station == null)
            {
                return new VisualElement();
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            TextField field = new TextField();
            field.BindProperty(station);
            field.style.flexGrow = 1;
            row.Add(field);

            Button pick = DimensionsApiControls.GhostButton(
                "Pick",
                () => ShowStationMenu(station.propertyPath));
            pick.tooltip = "Choose one of the game's benches, or one of your own.";
            pick.style.marginLeft = 6;
            row.Add(pick);

            return DimensionsApiControls.Field(
                "Made at",
                "Which bench this is crafted at. Leave it empty for the Wooden Workbench. A bench " +
                "from another mod works too, typed exactly as that mod names it.",
                row);
        }

        private void ShowStationMenu(string propertyPath)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Wooden Workbench (leave empty)"), false, () =>
                SetRuleString(propertyPath, string.Empty));
            for (int i = 0; i < VanillaCraftingStations.Length; i++)
            {
                string label = VanillaCraftingStations[i][0];
                string value = VanillaCraftingStations[i][1];
                menu.AddItem(new GUIContent("The game's/" + label), false, () =>
                    SetRuleString(propertyPath, value));
            }

            DimensionWorkbenchAsset[] mine = template == null
                ? new DimensionWorkbenchAsset[0]
                : template.GlobalWorkbenches;
            for (int i = 0; mine != null && i < mine.Length; i++)
            {
                DimensionWorkbenchAsset bench = mine[i];
                if (bench == null || string.IsNullOrEmpty(bench.WorkbenchId))
                {
                    continue;
                }

                string label = string.IsNullOrEmpty(bench.DisplayName)
                    ? bench.WorkbenchId
                    : bench.DisplayName;
                string value = bench.WorkbenchId;
                menu.AddItem(new GUIContent("Yours/" + label), false, () =>
                    SetRuleString(propertyPath, value));
            }

            menu.ShowAsContext();
        }

        private void AddDropTargets(VisualElement body)
        {
            SerializedProperty targets = serializedAccessRule.FindProperty("dropTargets");
            if (targets == null || !targets.isArray)
            {
                body.Add(Note("This rule has no drop list any more.", true));
                return;
            }

            if (targets.arraySize == 0)
            {
                body.Add(Note(
                    "Nothing drops it yet. Add a creature below, or no player will ever find one.",
                    true));
            }

            for (int i = 0; i < targets.arraySize; i++)
            {
                body.Add(BuildDropTargetRow(targets.GetArrayElementAtIndex(i), i));
            }

            Button add = DimensionsApiControls.GhostButton("Add a creature", AddDropTargetRow);
            add.tooltip = "Adds another creature that can drop this.";
            body.Add(add);
        }

        private VisualElement BuildDropTargetRow(SerializedProperty element, int index)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");

            SerializedProperty targetId = element.FindPropertyRelative("targetObjectId");
            SerializedProperty weight = element.FindPropertyRelative("weight");
            SerializedProperty chance = element.FindPropertyRelative("chancePercent");
            SerializedProperty minAmount = element.FindPropertyRelative("minAmount");
            SerializedProperty maxAmount = element.FindPropertyRelative("maxAmount");
            if (targetId == null)
            {
                return card;
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            TextField field = new TextField();
            field.BindProperty(targetId);
            field.style.flexGrow = 1;
            row.Add(field);
            Button pick = DimensionsApiControls.GhostButton(
                "Pick",
                () => ShowDropTargetMenu(targetId.propertyPath));
            pick.style.marginLeft = 6;
            pick.tooltip = "Choose a creature from the game, or one of yours.";
            row.Add(pick);
            card.Add(DimensionsApiControls.Field(
                "Dropped by",
                "The creature that can drop this, by the game's name for it. A creature from " +
                "another mod works too, typed exactly as that mod names it.",
                row));

            if (chance != null)
            {
                card.Add(BoundField(
                    chance,
                    "Chance",
                    "Out of a hundred, how often the drop is rolled at all. A hundred with " +
                    "nothing else in the roll makes it certain."));
            }

            if (weight != null)
            {
                card.Add(BoundField(
                    weight,
                    "Weight",
                    "How this drop measures against the creature's other drops when the game " +
                    "picks one. Higher comes up more often."));
            }

            if (minAmount != null)
            {
                card.Add(BoundField(minAmount, "Fewest", "The smallest number dropped at once."));
            }

            if (maxAmount != null)
            {
                card.Add(BoundField(maxAmount, "Most", "The largest number dropped at once."));
            }

            int removeAt = index;
            card.Add(DimensionsApiControls.GhostButton(
                "Take this creature off the list",
                () => RemoveArrayElement("dropTargets", removeAt)));
            return card;
        }

        private void ShowDropTargetMenu(string propertyPath)
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < VanillaDropTargets.Length; i++)
            {
                string label = VanillaDropTargets[i][0];
                string value = VanillaDropTargets[i][1];
                menu.AddItem(new GUIContent("The game's/" + label), false, () =>
                    SetRuleString(propertyPath, value));
            }

            DimensionBossAsset[] bosses = template == null
                ? new DimensionBossAsset[0]
                : template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string label = string.IsNullOrEmpty(boss.DisplayName) ? boss.BossId : boss.DisplayName;
                string value = boss.BossId;
                menu.AddItem(new GUIContent("Your bosses/" + label), false, () =>
                    SetRuleString(propertyPath, value));
            }

            DimensionMobAsset[] mobs = template == null
                ? new DimensionMobAsset[0]
                : template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                string label = string.IsNullOrEmpty(mob.DisplayName) ? mob.MobId : mob.DisplayName;
                string value = mob.MobId;
                menu.AddItem(new GUIContent("Your creatures/" + label), false, () =>
                    SetRuleString(propertyPath, value));
            }

            menu.ShowAsContext();
        }

        private void AddDropTargetRow()
        {
            if (serializedAccessRule == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty targets = serializedAccessRule.FindProperty("dropTargets");
            if (targets == null || !targets.isArray)
            {
                return;
            }

            int index = targets.arraySize;
            targets.InsertArrayElementAtIndex(index);
            SerializedProperty added = targets.GetArrayElementAtIndex(index);
            // Seeded by hand for the same reason the offering rows are: an inserted row is a copy
            // of its neighbour or a block of zeros, and a zero chance is a drop that never drops.
            added.FindPropertyRelative("targetObjectId").stringValue = string.Empty;
            added.FindPropertyRelative("displayName").stringValue = string.Empty;
            added.FindPropertyRelative("isBoss").boolValue = false;
            added.FindPropertyRelative("weight").intValue = 1;
            added.FindPropertyRelative("chancePercent").floatValue = 100f;
            added.FindPropertyRelative("minAmount").intValue = 1;
            added.FindPropertyRelative("maxAmount").intValue = 1;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void AddItemPortalSettings(VisualElement body)
        {
            SerializedProperty item = serializedAccessRule.FindProperty("portalItemObjectId");
            if (item != null)
            {
                Label value = new Label(string.IsNullOrEmpty(item.stringValue)
                    ? "not made yet"
                    : item.stringValue);
                value.AddToClassList("dim-readonly-value");
                body.Add(DimensionsApiControls.Field(
                    "The item",
                    "The item a player uses to tear this portal open. The framework makes it and " +
                    "keeps its name in step with the dimension, so there is nothing to type.",
                    value));
            }

            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "itemPortalDurationSeconds",
                "How long it stays open",
                "Seconds the torn-open portal stands before it closes. It is also how long the " +
                "item cannot be used again."));
        }

        /// <summary>
        /// Letting the game grow the portal in its own world, so a player can find one.
        /// </summary>
        /// <remarks>
        /// The biomes are the game's own and only the game's own: the Overworld never samples a
        /// custom biome, so a custom one named here would be dropped at build time with a warning
        /// rather than quietly never matching.
        /// </remarks>
        private void AddFoundInTheWorld(VisualElement body)
        {
            body.Add(RedrawingToggle(
                "generatedInWorld",
                "Found in the world",
                "On, the game grows this portal in its own world, standing on a small cleared " +
                "patch, so a player can come across one instead of crafting it."));
            if (!accessRule.GeneratedInWorld)
            {
                return;
            }

            SerializedProperty biomes = serializedAccessRule.FindProperty("worldBiomeNames");
            VisualElement chips = DimensionsApiControls.ChipRow();
            if (biomes != null && biomes.isArray)
            {
                for (int i = 0; i < biomes.arraySize; i++)
                {
                    int removeAt = i;
                    Button chip = new Button(() => RemoveArrayElement("worldBiomeNames", removeAt))
                    {
                        text = NameWorldBiome(biomes.GetArrayElementAtIndex(i).stringValue) + "  ×"
                    };
                    chip.AddToClassList("dim-chip");
                    chip.AddToClassList("dim-chip-removable");
                    chip.tooltip = "Click to stop the portal growing here.";
                    chips.Add(chip);
                }
            }

            if (chips.childCount == 0)
            {
                chips.Add(DimensionsApiControls.Chip("nowhere yet", "warn"));
            }

            body.Add(DimensionsApiControls.Field(
                "Grows in",
                "Which of the game's own places one can be found in. With none of them chosen, " +
                "none is ever grown.",
                chips));

            Button add = DimensionsApiControls.GhostButton("Add a place", ShowWorldBiomeMenu);
            add.tooltip = "Pick one of the game's own places for the portal to grow in.";
            body.Add(add);

            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "worldMaxOccurrences",
                "How many in a world",
                "The most the game will grow in one world."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "worldMinDistanceFromCore",
                "No closer to the Core than",
                "How far from the Core the nearest one may be, in tiles."));
        }

        private void ShowWorldBiomeMenu()
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < WorldBiomes.Length; i++)
            {
                string value = WorldBiomes[i][1];
                menu.AddItem(new GUIContent(WorldBiomes[i][0]), false, () => AddWorldBiome(value));
            }

            menu.ShowAsContext();
        }

        /// <summary>The player's name for a place, or the stored name when it is not one of ours.</summary>
        private static string NameWorldBiome(string storedName)
        {
            for (int i = 0; i < WorldBiomes.Length; i++)
            {
                if (string.Equals(WorldBiomes[i][1], storedName, System.StringComparison.Ordinal))
                {
                    return WorldBiomes[i][0];
                }
            }

            return Spaced(storedName);
        }

        private void AddWorldBiome(string biomeName)
        {
            if (serializedAccessRule == null || string.IsNullOrEmpty(biomeName))
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty biomes = serializedAccessRule.FindProperty("worldBiomeNames");
            if (biomes == null || !biomes.isArray)
            {
                return;
            }

            for (int i = 0; i < biomes.arraySize; i++)
            {
                if (string.Equals(
                        biomes.GetArrayElementAtIndex(i).stringValue,
                        biomeName,
                        System.StringComparison.Ordinal))
                {
                    return;
                }
            }

            int index = biomes.arraySize;
            biomes.InsertArrayElementAtIndex(index);
            biomes.GetArrayElementAtIndex(index).stringValue = biomeName;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
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

        // ------------------------------------------------------------------ plumbing --

        /// <summary>
        /// A bound control for a property of the rule, labelled and explained.
        /// </summary>
        /// <remarks>
        /// The shared helper takes a path from the root of an object, which array rows do not have
        /// to hand — so rows pass the property itself and this fills in the same chrome.
        /// </remarks>
        private static VisualElement BoundField(
            SerializedProperty property,
            string label,
            string tooltip)
        {
            VisualElement control;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    TextField field = new TextField();
                    field.BindProperty(property);
                    control = field;
                    break;
                }

                case SerializedPropertyType.Integer:
                {
                    IntegerField field = new IntegerField();
                    field.BindProperty(property);
                    control = field;
                    break;
                }

                case SerializedPropertyType.Float:
                {
                    FloatField field = new FloatField();
                    field.BindProperty(property);
                    control = field;
                    break;
                }

                case SerializedPropertyType.Boolean:
                {
                    Toggle field = new Toggle();
                    field.BindProperty(property);
                    control = field;
                    break;
                }

                default:
                {
                    PropertyField field = new PropertyField(property, string.Empty);
                    field.BindProperty(property);
                    control = field;
                    break;
                }
            }

            return DimensionsApiControls.Field(label, tooltip, control);
        }

        /// <summary>
        /// A toggle that decides whether the controls under it exist at all, so turning it redraws
        /// the column rather than leaving a settings gap the author has to click away to notice.
        /// </summary>
        private VisualElement RedrawingToggle(string propertyPath, string label, string tooltip)
        {
            VisualElement field = DimensionsApiControls.Bound(
                serializedAccessRule, propertyPath, label, tooltip);
            Toggle toggle = field.Q<Toggle>();
            if (toggle != null)
            {
                AfterBinding(toggle, () =>
                    toggle.RegisterValueChangedCallback(evt => DeferredRefresh()));
            }

            return field;
        }

        private void SetRuleString(string propertyPath, string value)
        {
            if (serializedAccessRule == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty property = serializedAccessRule.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void RemoveArrayElement(string arrayPath, int index)
        {
            if (serializedAccessRule == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty array = serializedAccessRule.FindProperty(arrayPath);
            if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
            {
                return;
            }

            array.DeleteArrayElementAtIndex(index);
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void NotifyRuleEdited()
        {
            NotifyRuleEdited(false);
        }

        private void NotifyRuleEdited(bool returnPortal)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null)
            {
                studio.NotifyRuleEdited(returnPortal ? returnRule : accessRule);
            }
        }

        private void StopRuleEditsAtTheirCard(VisualElement card)
        {
            StopRuleEditsAtTheirCard(card, false);
        }

        /// <summary>
        /// Keeps a card's edits from being counted as changes to the portal's artwork.
        /// </summary>
        /// <remarks>
        /// Registered only once the card has finished binding. Binding a field fires a change event
        /// as it takes the property's value, and a handler that is already listening treats that as
        /// a real edit — which on this page used to mean an artwork rebake, a synchronous import, a
        /// refresh, and around again forever.
        /// </remarks>
        private void StopRuleEditsAtTheirCard(VisualElement card, bool returnPortal)
        {
            AfterBinding(card, () =>
            {
                card.RegisterCallback<ChangeEvent<bool>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<int>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<float>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<string>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<Vector2>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<Object>>(evt => RuleEdited(evt, returnPortal));
            });
        }

        private void RuleEdited(EventBase evt, bool returnPortal)
        {
            NotifyRuleEdited(returnPortal);
            evt.StopPropagation();
        }

        private VisualElement Note(string text, bool warning)
        {
            Label note = new Label(text);
            note.AddToClassList("dim-note");
            if (warning)
            {
                note.AddToClassList("dim-note-warn");
            }

            return note;
        }

        /// <summary>A run-together name broken into words, so a page never shows code casing.</summary>
        private static string Spaced(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            System.Text.StringBuilder spaced = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                {
                    spaced.Append(' ');
                }

                spaced.Append(value[i]);
            }

            return spaced.ToString();
        }
    }
}
