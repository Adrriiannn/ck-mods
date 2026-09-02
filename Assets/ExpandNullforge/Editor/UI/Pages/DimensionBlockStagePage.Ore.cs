using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The ores a block can be mined for, and adding one to it.
    /// </summary>
    internal sealed partial class DimensionBlockStagePage
    {
        // ------------------------------------------------------------------- the ore ---

        private VisualElement BuildWhatCanBeMined(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Ores",
                "veins in its walls");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!type.HasStates)
            {
                body.Add(Note("Only terrain blocks have walls for a vein to sit in.", false));
                return group;
            }

            body.Add(Note(
                "Veins this block's walls can hold. The vein art comes from the ore corner of your " +
                "sheet, and building the mod wires the rest.",
                false));
            body.Add(Note(
                "One ore per block. The game finds a vein by taking the first match on this block, " +
                "so anything after the first could never be reached. The game has that same " +
                "limit in its own blocks.",
                false));

            SerializedProperty ores = serialized.FindProperty("ores");
            VisualElement chips = DimensionsApiControls.ChipRow();
            if (ores != null && ores.isArray && ores.arraySize > 0)
            {
                for (int i = 0; i < ores.arraySize; i++)
                {
                    SerializedProperty element = ores.GetArrayElementAtIndex(i);
                    SerializedProperty id = element.FindPropertyRelative("oreItemId");
                    SerializedProperty custom = element.FindPropertyRelative("isCustomItem");
                    if (id == null || custom == null)
                    {
                        continue;
                    }

                    chips.Add(BuildOreChip(i, id.stringValue, custom.boolValue));
                }
            }

            if (chips.childCount == 0)
            {
                Label none = DimensionsApiControls.Chip("nothing to mine yet");
                chips.Add(none);
            }

            body.Add(DimensionsApiControls.Field(
                "Ore Veins",
                "Click one to take it away. Whatever is listed first is the one the game will find.",
                chips));

            // The vein numbers. Their other home is a page nothing routes to, so without
            // them here every ore this page adds is paint-only whatever the
            // defaults say — the author has no way to see or change how much of it grows.
            if (ores != null && ores.isArray && ores.arraySize > 0)
            {
                SerializedProperty first = ores.GetArrayElementAtIndex(0);
                SerializedProperty abundance = first.FindPropertyRelative("abundance");
                SerializedProperty veinMin = first.FindPropertyRelative("veinSizeMin");
                SerializedProperty veinMax = first.FindPropertyRelative("veinSizeMax");
                if (abundance != null && veinMin != null && veinMax != null)
                {
                    Slider abundanceSlider = new Slider(0f, 5f)
                    {
                        value = abundance.floatValue,
                        showInputField = true
                    };
                    abundanceSlider.RegisterValueChangedCallback(evt =>
                    {
                        serialized.Update();
                        SerializedProperty ownOres = serialized.FindProperty("ores");
                        if (ownOres != null && ownOres.arraySize > 0)
                        {
                            ownOres.GetArrayElementAtIndex(0)
                                .FindPropertyRelative("abundance").floatValue = evt.newValue;
                            serialized.ApplyModifiedProperties();
                        }
                    });
                    body.Add(DimensionsApiControls.Field(
                        "How Much Grows",
                        "About how many veins per 100 wall tiles. 0 means paint-only: veins " +
                        "appear only where you placed one yourself.",
                        abundanceSlider));

                    MinMaxSlider veinSize = new MinMaxSlider(
                        veinMin.intValue, veinMax.intValue, 1, 16);
                    veinSize.RegisterValueChangedCallback(evt =>
                    {
                        serialized.Update();
                        SerializedProperty ownOres = serialized.FindProperty("ores");
                        if (ownOres != null && ownOres.arraySize > 0)
                        {
                            SerializedProperty el = ownOres.GetArrayElementAtIndex(0);
                            el.FindPropertyRelative("veinSizeMin").intValue =
                                Mathf.RoundToInt(evt.newValue.x);
                            el.FindPropertyRelative("veinSizeMax").intValue =
                                Mathf.Max(
                                    Mathf.RoundToInt(evt.newValue.x),
                                    Mathf.RoundToInt(evt.newValue.y));
                            serialized.ApplyModifiedProperties();
                        }
                    });
                    body.Add(DimensionsApiControls.Field(
                        "Vein Size",
                        "Smallest and largest vein, in blocks. The game's own run 3 to 6.",
                        veinSize));
                }
            }

            body.Add(BuildOreAdder());
            return group;
        }

        private VisualElement BuildOreChip(int index, string storedValue, bool custom)
        {
            string label = custom ? NameOfOwnItem(storedValue) : Spaced(storedValue);
            Button chip = new Button(() => RemoveOre(index))
            {
                text = label + "  ×"
            };
            chip.AddToClassList("dim-chip");
            chip.AddToClassList("dim-chip-removable");
            if (custom)
            {
                chip.AddToClassList("dim-chip-link");
            }

            chip.tooltip = custom
                ? "One of your own things, dropped by this block's walls. Click to take it away."
                : "One of the game's own ores, dropped by this block's walls. Click to take it away.";
            return chip;
        }

        private void RemoveOre(int index)
        {
            serialized.Update();
            SerializedProperty ores = serialized.FindProperty("ores");
            if (ores == null || !ores.isArray || index < 0 || index >= ores.arraySize)
            {
                return;
            }

            ores.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            DeferredDetail();
            repaint();
        }

        /// <summary>
        /// Picking what a vein drops, by name. The game's own ores first, then anything this
        /// dimension makes, so nobody has to know what an item is called behind the scenes.
        /// </summary>
        private VisualElement BuildOreAdder()
        {
            List<string> labels = new List<string>();
            List<string> values = new List<string>();
            List<bool> customFlags = new List<bool>();

            IReadOnlyList<string> vanilla = DimensionTilesetStudio.VanillaOres;
            for (int i = 0; i < vanilla.Count; i++)
            {
                labels.Add(Spaced(vanilla[i]));
                values.Add(vanilla[i]);
                customFlags.Add(false);
            }

            DimensionItemAsset[] mine = template == null
                ? new DimensionItemAsset[0]
                : template.GlobalItems;
            for (int i = 0; i < mine.Length; i++)
            {
                DimensionItemAsset item = mine[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                string shown = string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;
                labels.Add("Yours: " + shown);
                values.Add(item.ItemId);
                customFlags.Add(true);
            }

            if (labels.Count == 0)
            {
                return Note("There is nothing to put in a vein yet. Make an item first.", false);
            }

            DropdownField adder = new DropdownField();
            adder.choices = labels;
            adder.RegisterValueChangedCallback(evt =>
            {
                int at = adder.index;
                if (at >= 0)
                {
                    AddOre(values[at], customFlags[at]);
                }
            });

            return DimensionsApiControls.Field(
                "Add Ore",
                "Pick what a vein in this block drops. One of the game's ores makes a vein pointing " +
                "at the real thing, and one of yours becomes the vein itself.",
                adder);
        }

        private void AddOre(string value, bool custom)
        {
            serialized.Update();
            SerializedProperty ores = serialized.FindProperty("ores");
            if (ores == null || !ores.isArray)
            {
                return;
            }

            int index = ores.arraySize;
            ores.InsertArrayElementAtIndex(index);
            SerializedProperty added = ores.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("oreItemId").stringValue = value;
            added.FindPropertyRelative("isCustomItem").boolValue = custom;
            // InsertArrayElementAtIndex knows nothing of field initializers: it clones the
            // previous element or zero-fills. A zero abundance means paint-only — the runtime
            // drops the rule and every ore added here silently never grew a vein. Seed the
            // class's own defaults explicitly.
            added.FindPropertyRelative("abundance").floatValue = 1f;
            added.FindPropertyRelative("veinSizeMin").intValue = 3;
            added.FindPropertyRelative("veinSizeMax").intValue = 6;
            serialized.ApplyModifiedProperties();
            DeferredDetail();
            repaint();
        }

        private string NameOfOwnItem(string itemId)
        {
            DimensionItemAsset[] mine = template == null
                ? new DimensionItemAsset[0]
                : template.GlobalItems;
            for (int i = 0; i < mine.Length; i++)
            {
                if (mine[i] != null && string.Equals(mine[i].ItemId, itemId, System.StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(mine[i].DisplayName) ? mine[i].name : mine[i].DisplayName;
                }
            }

            // Never the stored id. If the item behind it is gone, say so in words rather than
            // showing a name only the framework uses.
            return "Something of yours that is no longer here";
        }
    }
}
