using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// How a player comes by the portal: crafting, drops, the world itself.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
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
    }
}
