using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What deleting one authored thing would leave behind.</summary>
    internal sealed class DimensionIdDeletePlan
    {
        internal DimensionIdDeletePlan(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            string id)
        {
            Template = template;
            Asset = asset;
            Field = field;
            Id = id ?? string.Empty;
            Edges = new List<DimensionIdReference>();
            Blockers = new List<string>();
            GhostFiles = new List<string>();
        }

        internal DimensionTemplateAsset Template { get; }

        internal Object Asset { get; }

        internal DimensionIdentityField Field { get; }

        internal string Id { get; }

        /// <summary>Everywhere that names it and would be left pointing at nothing.</summary>
        internal List<DimensionIdReference> Edges { get; }

        internal List<string> Blockers { get; }

        /// <summary>Generated files that would still be shipped if they were left.</summary>
        internal List<string> GhostFiles { get; }

        internal bool CanGo
        {
            get { return Blockers.Count == 0; }
        }

        /// <summary>Inbound places only — the thing's own name is not a dangling reference.</summary>
        internal int InboundCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Edges.Count; i++)
                {
                    if (Edges[i].Asset != Asset)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    /// <summary>
    /// Deleting an authored thing, having first said what points at it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS AT ALL. There are exactly two delete buttons in the whole framework — one
    /// for blocks and one for items. The other twenty-eight kinds of thing have none, so creators
    /// delete them in the Project view, which leaves a <c>null</c> sitting in the dimension's list
    /// and every string that named it pointing at nothing. There is no check anywhere for a null
    /// list entry, and none for a name that no longer resolves in most of these namespaces, so
    /// today that is silent in both directions.
    /// </para>
    /// <para>
    /// THREE ANSWERS, BECAUSE THERE ARE THREE. Leave it alone. Delete it and clear every field that
    /// named it, which is right when the thing is going away for good. Delete it and leave those
    /// fields, which is right when something else is about to take the name — and the count is said
    /// out loud either way, because "seven things point at this" is the fact that decides it.
    /// </para>
    /// </remarks>
    internal static class DimensionIdDelete
    {
        /// <summary>Works out what would happen. Writes nothing.</summary>
        internal static DimensionIdDeletePlan Plan(
            DimensionTemplateAsset template,
            Object asset,
            DimensionIdentityField field,
            bool alsoOutsideThisPack)
        {
            string id = DimensionIdRename.ReadId(asset, field);
            DimensionIdDeletePlan plan = new DimensionIdDeletePlan(template, asset, field, id);
            if (template == null || asset == null || field == null)
            {
                plan.Blockers.Add("There is nothing open to delete.");
                return plan;
            }

            // A block's two item assets belong to the block. Deleting one of them on its own leaves
            // the block naming an item that is gone; the block's own delete removes them properly.
            DimensionItemAsset item = asset as DimensionItemAsset;
            if (item != null)
            {
                DimensionTilesetAsset[] blocks = template.Tilesets;
                for (int i = 0; blocks != null && i < blocks.Length; i++)
                {
                    if (blocks[i] == null)
                    {
                        continue;
                    }

                    if (string.Equals(item.ItemId, blocks[i].GroundBlockItemId, StringComparison.Ordinal) ||
                        string.Equals(item.ItemId, blocks[i].WallBlockItemId, StringComparison.Ordinal))
                    {
                        plan.Blockers.Add(
                            "\"" + item.DisplayName + "\" belongs to the block \"" +
                            blocks[i].BlockName + "\". Delete that block and this goes with it; " +
                            "deleting it on its own leaves the block naming an item that is gone.");
                        return plan;
                    }
                }
            }

            if (!string.IsNullOrEmpty(id))
            {
                plan.Edges.AddRange(
                    DimensionIdReferences.Find(template, id, alsoOutsideThisPack));
                plan.GhostFiles.AddRange(DimensionGeneratedFootprint.Of(template, field, id));
            }

            // THE SLOTS THAT HOLD IT, which are the ones a delete actually breaks. A name left
            // behind is a string that resolves to nothing; a slot left behind is a Missing
            // reference in a monster's loot, a biome's scene pool or a dungeon's room list, and
            // nothing in this framework validates for one. The dialogue used to say "Nothing points
            // at it." over three of them.
            plan.Edges.AddRange(
                DimensionIdReferences.FindHolders(
                    template, asset, alsoOutsideThisPack, field.ContainerProperty));

            return plan;
        }

        /// <summary>
        /// Deletes it: the list entry, the asset file, the generated files, and — when asked — every
        /// field that named it.
        /// </summary>
        /// <remarks>
        /// Clearing the pointers and taking the asset out of the dimension's list happen FIRST and
        /// in one undo group; the file deletions happen last and are outside it, which is why they
        /// are last. An interrupted delete then leaves a thing that still exists and some cleared
        /// pointers, which a creator can see and put back — rather than a deleted thing and a
        /// project still full of its name. The report says plainly what Ctrl+Z will and will not
        /// do afterwards, because undo here is genuinely half a promise.
        /// </remarks>
        internal static bool Apply(
            DimensionIdDeletePlan plan,
            bool clearWhatPointedAtIt,
            IReadOnlyList<DimensionIdReference> clear,
            out string report)
        {
            if (plan == null || !plan.CanGo)
            {
                report = plan == null || plan.Blockers.Count == 0
                    ? "There was nothing to delete."
                    : plan.Blockers[0];
                return false;
            }

            int cleared = 0;
            int leftForeign = 0;
            List<DimensionIdReference> ours = Ordered(plan, clear, out leftForeign);

            // ONE UNDO GROUP FOR EVERYTHING UNDO CAN REACH, the list removal included. It used to
            // be closed before the asset was taken out of the dimension's own array, so the first
            // Ctrl+Z after a delete put a reference to a now-deleted asset back into that array —
            // a Missing entry, which is the exact state this class exists to prevent — and a second
            // was needed to get the pointers back. What Ctrl+Z cannot do either way is bring the
            // file back, and the report below says so rather than leaving it to be discovered.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Delete " + plan.Id);
            int group = Undo.GetCurrentGroup();

            List<Object> touched = new List<Object>();
            if (clearWhatPointedAtIt)
            {
                for (int i = 0; i < ours.Count; i++)
                {
                    if (!touched.Contains(ours[i].Asset))
                    {
                        touched.Add(ours[i].Asset);
                    }
                }
            }

            if (plan.Template != null && !touched.Contains(plan.Template))
            {
                touched.Add(plan.Template);
            }

            Undo.RecordObjects(touched.ToArray(), "Delete " + plan.Id);

            if (clearWhatPointedAtIt)
            {
                for (int i = 0; i < ours.Count; i++)
                {
                    if (Clear(ours[i], plan.Asset))
                    {
                        cleared++;
                    }
                }
            }

            RemoveFromItsList(plan);
            Undo.CollapseUndoOperations(group);

            List<string> stuck = DimensionGeneratedFootprint.Remove(plan.GhostFiles);

            string path = AssetDatabase.GetAssetPath(plan.Asset);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();

            report = "Deleted the " + plan.Field.Thing + " \"" + plan.Id + "\"" +
                     (cleared > 0
                         ? " and cleared " + cleared +
                           (cleared == 1 ? " place that pointed at it." : " places that pointed at it.")
                         : ".");
            if (!clearWhatPointedAtIt && plan.InboundCount > 0)
            {
                report += " " + plan.InboundCount +
                          (plan.InboundCount == 1
                              ? " place still points at it."
                              : " places still point at it.");
            }

            if (clearWhatPointedAtIt && leftForeign > 0)
            {
                report += " " + leftForeign +
                          (leftForeign == 1
                              ? " place in another dimension in this project was left alone."
                              : " places in other dimensions in this project were left alone.");
            }

            for (int i = 0; i < stuck.Count; i++)
            {
                report += " '" + stuck[i] + "' could not be removed and is still shipped.";
            }

            report += "\n\nCtrl+Z puts back what was changed here. It cannot bring the file back — " +
                      "deleting an asset is outside undo — so undoing this leaves the project " +
                      "pointing at something that is no longer there.";
            return true;
        }

        /// <summary>
        /// The places this delete may clear, in the order they have to be cleared in, and how many
        /// were left alone because they belong to somebody else.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FOREIGN ROWS ARE NEVER CLEARED. The rename lists another dimension's rows separately and
        /// leaves them unticked; delete has no ticks and used to hand the whole list to the clear,
        /// so "Delete it and empty them" quietly emptied fields in another modder's pack. They are
        /// counted and said out loud instead.
        /// </para>
        /// <para>
        /// DESCENDING BY ARRAY INDEX, because a slot holding the asset is REMOVED from its list
        /// rather than nulled — a null in a scene pool is the same invisible hole a deleted asset
        /// leaves — and removing element 1 moves element 2 down. Highest index first means every
        /// index still to be used is still correct. Strings inside a struct element are blanked
        /// where they are and their paths do not move.
        /// </para>
        /// </remarks>
        private static List<DimensionIdReference> Ordered(
            DimensionIdDeletePlan plan,
            IReadOnlyList<DimensionIdReference> clear,
            out int leftForeign)
        {
            leftForeign = 0;
            List<DimensionIdReference> ours = new List<DimensionIdReference>();
            for (int i = 0; clear != null && i < clear.Count; i++)
            {
                if (clear[i].Asset == null || clear[i].Asset == plan.Asset)
                {
                    continue;
                }

                if (clear[i].OutsideThisPack)
                {
                    leftForeign++;
                    continue;
                }

                ours.Add(clear[i]);
            }

            ours.Sort((left, right) => IndexOf(right.PropertyPath).CompareTo(IndexOf(left.PropertyPath)));
            return ours;
        }

        /// <summary>The last array index in a serialized path, or -1 when it has none.</summary>
        private static int IndexOf(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return -1;
            }

            int open = propertyPath.LastIndexOf('[');
            int close = open < 0 ? -1 : propertyPath.IndexOf(']', open);
            int index;
            return close > open && int.TryParse(
                       propertyPath.Substring(open + 1, close - open - 1), out index)
                ? index
                : -1;
        }

        /// <summary>Empties one place that pointed at the deleted thing.</summary>
        /// <remarks>
        /// A NAME IS BLANKED WHERE IT IS; A SLOT IS TAKEN OUT OF ITS LIST. Blanking a slot leaves a
        /// null element in a scene pool or a room list, and nothing anywhere validates for one —
        /// the same invisible hole a Project-view delete leaves, which is what this class was
        /// written to stop. A slot that is not in a list has nowhere to go and is nulled.
        /// </remarks>
        private static bool Clear(DimensionIdReference edge, Object deleted)
        {
            if (edge.Asset == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(edge.Asset);
            SerializedProperty property = serialized.FindProperty(edge.PropertyPath);
            if (property == null)
            {
                return false;
            }

            if (edge.Hold == DimensionIdHold.TheThingItself)
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue != deleted)
                {
                    return false;
                }

                if (!RemoveFromItsArray(serialized, edge.PropertyPath))
                {
                    property.objectReferenceValue = null;
                    serialized.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(edge.Asset);
                return true;
            }

            if (property.propertyType != SerializedPropertyType.String ||
                !string.Equals(property.stringValue, edge.RawValue, StringComparison.Ordinal))
            {
                return false;
            }

            // A NAME THAT IS ITS OWN LIST ENTRY IS REMOVED, not blanked. Blanking left an empty row
            // sitting in the list — in an ore scatter list, an allowed-biome list, a projectile
            // list — which reads as "one of these is unset" rather than "one of these is gone. A
            // name inside a row of some other list (a loot entry's item, a room's object) has the
            // rest of its row to belong to, so that one is emptied where it is.
            if (!RemoveFromItsArray(serialized, edge.PropertyPath))
            {
                property.stringValue = string.Empty;
                serialized.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(edge.Asset);
            return true;
        }

        /// <summary>
        /// Takes one element out of the array its path names, or false when the path is not an
        /// array element.
        /// </summary>
        private static bool RemoveFromItsArray(SerializedObject serialized, string propertyPath)
        {
            const string Marker = ".Array.data[";
            int marker = propertyPath.LastIndexOf(Marker, StringComparison.Ordinal);
            if (marker < 0 || !propertyPath.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }

            string arrayPath = propertyPath.Substring(0, marker);
            string index = propertyPath.Substring(
                marker + Marker.Length, propertyPath.Length - marker - Marker.Length - 1);
            int at;
            SerializedProperty array = serialized.FindProperty(arrayPath);
            if (array == null || !array.isArray || !int.TryParse(index, out at) ||
                at < 0 || at >= array.arraySize)
            {
                return false;
            }

            // Unity's first DeleteArrayElementAtIndex on an object-reference array nulls the slot
            // rather than removing it; the second removes the null. The same two-step the
            // framework's own RemoveObjectReference uses.
            array.DeleteArrayElementAtIndex(at);
            if (at < array.arraySize &&
                array.GetArrayElementAtIndex(at).propertyType ==
                SerializedPropertyType.ObjectReference &&
                array.GetArrayElementAtIndex(at).objectReferenceValue == null)
            {
                array.DeleteArrayElementAtIndex(at);
            }

            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// Takes the asset out of the list the dimension keeps it in, so no null is left behind.
        /// </summary>
        /// <remarks>
        /// A null in one of those arrays is invisible: nothing validates for it, every generator
        /// skips it, and the page it appears on draws it as "Missing". Deleting in the Project view
        /// is exactly how one gets there.
        /// </remarks>
        private static void RemoveFromItsList(DimensionIdDeletePlan plan)
        {
            if (plan.Field.ContainerProperty.Length == 0)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(plan.Template);
            SerializedProperty property = serialized.FindProperty(plan.Field.ContainerProperty);
            if (property == null)
            {
                return;
            }

            if (!property.isArray)
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == plan.Asset)
                {
                    property.objectReferenceValue = null;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(plan.Template);
                }

                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue != plan.Asset)
                {
                    continue;
                }

                // Unity's first DeleteArrayElementAtIndex on an object-reference array nulls the
                // slot rather than removing it; the second removes the null. Same shape the
                // framework's own RemoveObjectReference uses.
                property.DeleteArrayElementAtIndex(i);
                if (i < property.arraySize &&
                    property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    property.DeleteArrayElementAtIndex(i);
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(plan.Template);
                return;
            }
        }
    }
}
