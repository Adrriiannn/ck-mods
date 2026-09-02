using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Portal access rules, the portal items a template needs, and their default icons.
    /// </summary>
    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {
        public static DimensionFrameworkAuthoringAssetActionResult CreatePortalAccessRule(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding portal access.");
            }

            int index = template.PortalAccessRules.Length + 1;
            string baseId = ResolveScopedId(template, "Portal" + index.ToString());
            DimensionPortalAccessRuleAsset rule =
                CreateAsset<DimensionPortalAccessRuleAsset>(
                    template,
                    "Portals",
                    "PortalAccessRule" + index.ToString());
            rule.Configure(
                baseId + ".rule",
                baseId,
                baseId + ".presentation",
                template.DisplayName + " Portal",
                DimensionIds.Overworld,
                Vector2.zero,
                template.DimensionId,
                Vector2.zero,
                DimensionPortalAccessKind.PlacedPortal,
                DimensionPortalActivationMode.VanillaCooldown,
                2.0f,
                true,
                true,
                "Enter " + template.DisplayName,
                "The portal is not active yet.",
                string.Empty,
                string.Empty,
                string.Empty,
                index,
                true,
                new DimensionPortalRequiredItemTemplate[0]);
            AppendObjectReference(template, "portalAccessRules", rule);
            SaveAndSelect(rule);
            return Success(rule, "Created a placed-portal access rule.");
        }

        /// <summary>
        /// Makes sure a dimension has a rule for one kind of portal, and returns the rule that
        /// owns it either way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The repair button behind the Portal Studio's Access card, and deliberately NOT an "add
        /// a rule" list operation. A dimension has exactly one effective rule per kind, because the
        /// generator takes the first enabled match; a second rule of the same kind produces a
        /// service record with no portal in the world and nothing else. So this heals a gap and
        /// refuses to widen one: if a rule of that kind already exists it is handed back untouched,
        /// disabled or not.
        /// </para>
        /// <para>
        /// The defaults come from the same starter factory that seeds a brand-new dimension, so a
        /// repaired portal behaves exactly like a fresh one.
        /// </para>
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult EnsurePortalAccessRule(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind)
        {
            if (template == null)
            {
                return Failure("Open a dimension before preparing its portal.");
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            for (int i = 0; rules != null && i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].AccessKind == accessKind)
                {
                    return Success(rules[i], "This portal already has its rule.");
                }
            }

            DimensionPortalAccessRuleAsset created = DimensionTemplateStarterFactory
                .CreatePortalAccessRule(accessKind, template.DimensionId, template.DisplayName);
            if (created == null)
            {
                return Failure("This portal's rule could not be prepared.");
            }

            DimensionPortalAccessRuleAsset saved = SaveNewAsset(
                template,
                "Portals",
                string.IsNullOrEmpty(created.name) ? "PortalAccessRule" : created.name,
                created);
            AppendObjectReference(template, "portalAccessRules", saved);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
            return Success(saved, "Prepared this portal's rule.");
        }

        /// <summary>
        /// Counts enabled instantaneous item portals (V2) that do not yet have an item asset, without
        /// creating anything (safe to call from OnGUI).
        /// </summary>
        public static int CountMissingPortalItems(DimensionTemplateAsset template)
        {
            int count = 0;
            ForEachMissingPortalItem(template, (rule, itemId, label) => count++);
            return count;
        }

        /// <summary>
        /// Ensures every enabled item portal (V2) has a real, editable item asset in the template's
        /// items, creating a default for any that is missing. The item id is locked to the rule's
        /// <c>PortalItemObjectId</c> because the runtime portal registry matches on it; everything else
        /// (icon, name, recipe) is left for the creator to customize in the item editor.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult EnsurePortalItems(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating portal items.");
            }

            // Broken or deleted entries (missing asset files, or assets whose script binding was
            // severed) read back as null, hide the real item from the duplicate check, and every
            // generate would then create "PortalItem 1", "PortalItem 2", ... — compact them away
            // before deciding anything is missing.
            CompactGlobalItems(template);

            int created = 0;
            DimensionItemAsset lastCreated = null;
            ForEachMissingPortalItem(template, (rule, itemId, label) =>
            {
                DimensionItemAsset item = CreateAsset<DimensionItemAsset>(template, "Resources", "PortalItem");
                SetSerializedString(item, "itemId", itemId);
                SetSerializedString(item, "displayName", label + " Portal");
                SetSerializedString(item, "description",
                    "Right-click to open a temporary portal to " + label + ".");
                SetSerializedEnum(item, "archetype", (int)DimensionItemArchetype.Material);
                SetSerializedEnum(item, "kind", (int)DimensionItemKind.PortalItem);
                // objectId only satisfies the generator's "has a visual" gate so the first generate is
                // valid; a real icon comes from the creator dragging a sprite into the Icon sprite field.
                SetSerializedString(item, "objectId", itemId);
                // The migration flag goes first, or the item's own OnValidate would still be
                // deciding stacking from the old number and would overwrite the line below.
                SetSerializedBool(item, "stackableWasMigrated", true);
                SetSerializedBool(item, "stackable", false);
                SetSerializedBool(item, "enabled", true);
                AssignDefaultPortalIcons(item);
                AppendObjectReference(template, "globalItems", item);
                lastCreated = item;
                created++;
            });

            if (created == 0)
            {
                return Success(null, "Every enabled item portal already has an item.");
            }

            AssetDatabase.SaveAssets();
            return Success(
                lastCreated,
                "Created " + created + " portal item" + (created == 1 ? string.Empty : "s") + ".");
        }

        // Optional framework-shipped default icons for auto-created portal items. Drop a 16x16
        // PortalItemIcon.png and a 10x10 PortalItemIcon_inHand.png here (Sprite, PPU 16, Point filter)
        // and every new portal item starts with them; absent, the item is created iconless.
        private const string DefaultPortalIconPath =
            "Assets/ExpandNullforge/DefaultArt/PortalItemIcon.png";

        private const string DefaultPortalSmallIconPath =
            "Assets/ExpandNullforge/DefaultArt/PortalItemIcon_inHand.png";

        /// <summary>
        /// Fills any empty portal-item icon slot across the template with the framework default icons,
        /// so a creator sees them applied without dragging. Never overwrites an icon the creator has
        /// already set. Returns the number of items changed.
        /// </summary>
        public static int ApplyDefaultPortalIcons(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return 0;
            }

            DimensionItemAsset[] items = template.GlobalItems;
            if (items == null)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item != null &&
                    item.Kind == DimensionItemKind.PortalItem &&
                    AssignDefaultPortalIcons(item))
                {
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Assigns the framework default icons to a single item, filling only slots that are still
        /// empty. Returns true if anything changed. The default sprites are loaded by path, so the
        /// import post-processor must have turned the PNGs into Sprites first.
        /// </summary>
        /// <summary>True once at least one framework default portal icon has imported as a Sprite.</summary>
        public static bool DefaultPortalIconsExist()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalIconPath) != null ||
                   AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalSmallIconPath) != null;
        }

        private static bool AssignDefaultPortalIcons(DimensionItemAsset item)
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalIconPath);
            Sprite smallIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalSmallIconPath);
            if (icon == null && smallIcon == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(item);
            bool changed = false;

            if (icon != null)
            {
                SerializedProperty iconProperty = serialized.FindProperty("iconSprite");
                if (iconProperty != null && iconProperty.objectReferenceValue == null)
                {
                    iconProperty.objectReferenceValue = icon;
                    changed = true;
                }
            }

            if (smallIcon != null)
            {
                SerializedProperty smallIconProperty = serialized.FindProperty("smallIconSprite");
                if (smallIconProperty != null && smallIconProperty.objectReferenceValue == null)
                {
                    smallIconProperty.objectReferenceValue = smallIcon;
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }

            return changed;
        }

        private static void ForEachMissingPortalItem(
            DimensionTemplateAsset template,
            System.Action<DimensionPortalAccessRuleAsset, string, string> onMissing)
        {
            if (template == null)
            {
                return;
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            if (rules == null)
            {
                return;
            }

            System.Collections.Generic.HashSet<string> seen =
                new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            DimensionItemAsset[] items = template.GlobalItems;
            if (items != null)
            {
                foreach (DimensionItemAsset existing in items)
                {
                    if (existing != null && !string.IsNullOrEmpty(existing.ItemId))
                    {
                        seen.Add(existing.ItemId);
                    }
                }
            }

            string label = string.IsNullOrEmpty(template.DisplayName)
                ? template.DimensionId
                : template.DisplayName;
            if (string.IsNullOrEmpty(label))
            {
                label = "Dimension";
            }

            foreach (DimensionPortalAccessRuleAsset rule in rules)
            {
                if (rule == null || !rule.IsItemPortal || !rule.Enabled)
                {
                    continue;
                }

                string itemId = rule.PortalItemObjectId;
                // seen.Add is false when the id already exists (a creator item or a duplicate rule).
                if (string.IsNullOrEmpty(itemId) || !seen.Add(itemId))
                {
                    continue;
                }

                onMissing(rule, itemId, label);
            }
        }
    }
}
