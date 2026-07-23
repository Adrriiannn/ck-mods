using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload (the asset shows as
    // "(…)" with m_Script fileID 0 and all dashboard references break).
    [CreateAssetMenu(menuName = "Dimensions API/Item")]
    public sealed class DimensionItemAsset : ScriptableObject
    {
        [SerializeField] private string itemId = "mod:item";
        [SerializeField] private string displayName = "Item";
        [Tooltip("Decides which Core Keeper authoring components the generated object needs. Anything you do not customize stays vanilla.")]
        [SerializeField] private DimensionItemArchetype archetype = DimensionItemArchetype.Material;
        [SerializeField] private DimensionItemKind kind = DimensionItemKind.BaseItem;
        [Tooltip("Tooltip text shown in-game. Leave blank for no description.")]
        [SerializeField] private string description = string.Empty;
        [Tooltip("Inventory icon. 16x16 PNG, Pixels Per Unit 16, Point (no) filter. This is the reliable way to give an item art; the icon id below is only used as a fallback lookup.")]
        [SerializeField] private Sprite iconSprite;
        [Tooltip("Optional small icon shown in the player's hand and while the item sits on the cursor. 10x10 PNG, Pixels Per Unit 16, Point filter. Leave empty to reuse the inventory icon.")]
        [SerializeField] private Sprite smallIconSprite;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private int maxStack = 999;
        [SerializeField] private string rarityId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        [Header("Archetype data (only the fields your archetype needs are used)")]
        [Tooltip("Loot table awarded when this object is destroyed. Required by ore, breakable, mob and boss archetypes.")]
        [SerializeField] private string lootTableId = string.Empty;
        [Tooltip("Health before the object breaks or dies. Required by breakable and creature archetypes.")]
        [SerializeField] private int healthPoints = 0;
        [Tooltip("Durability before the equipment wears out. Required by tool, weapon and armor archetypes.")]
        [SerializeField] private int durabilityPoints = 0;
        [Tooltip("Damage dealt per hit. Required by the weapon archetype.")]
        [SerializeField] private int damageAmount = 0;
        [Tooltip("Handheld cooldown in seconds. Required by consumable, tool and weapon archetypes.")]
        [SerializeField] private float cooldownSeconds = 0f;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        /// <summary>In-game tooltip text, written to the mod's localization table.</summary>
        public string Description
        {
            get { return description ?? string.Empty; }
        }

        /// <summary>
        /// Directly assigned sprite. Preferred over <c>IconId</c>, which has to be looked up by
        /// name and can silently fail to resolve.
        /// </summary>
        public Sprite IconSprite
        {
            get { return iconSprite; }
        }

        /// <summary>
        /// Small icon shown in-hand and on the cursor. Optional; when unset the generator reuses the
        /// inventory icon so the item is never left without a held sprite.
        /// </summary>
        public Sprite SmallIconSprite
        {
            get { return smallIconSprite; }
        }

        /// <summary>True when the item has art the generator can actually use.</summary>
        public bool HasVisual
        {
            get
            {
                return iconSprite != null ||
                       !string.IsNullOrEmpty(iconId) ||
                       !string.IsNullOrEmpty(objectId);
            }
        }

        /// <summary>Loot table id awarded on destruction (loot-bearing archetypes).</summary>
        public string LootTableId
        {
            get { return lootTableId ?? string.Empty; }
        }

        /// <summary>Health before breaking/dying (breakable and creature archetypes).</summary>
        public int HealthPoints
        {
            get { return Mathf.Max(0, healthPoints); }
        }

        /// <summary>Equipment durability (tool, weapon, armor archetypes).</summary>
        public int DurabilityPoints
        {
            get { return Mathf.Max(0, durabilityPoints); }
        }

        /// <summary>Damage per hit (weapon archetype).</summary>
        public int DamageAmount
        {
            get { return Mathf.Max(0, damageAmount); }
        }

        /// <summary>Handheld cooldown in seconds (consumable, tool, weapon archetypes).</summary>
        public float CooldownSeconds
        {
            get { return Mathf.Max(0f, cooldownSeconds); }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionItemKind Kind
        {
            get { return kind; }
        }

        /// <summary>Archetype that decides the generated object's authoring components.</summary>
        public DimensionItemArchetype Archetype
        {
            get { return archetype; }
        }

        /// <summary>
        /// The exact Core Keeper authoring components this item's generated object requires.
        /// The generator emits only these, and the dashboard shows only the matching fields.
        /// </summary>
        public DimensionItemAuthoringComponents RequiredComponents
        {
            get { return DimensionItemArchetypeRules.GetRequiredComponents(archetype); }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public int MaxStack
        {
            get { return Mathf.Max(1, maxStack); }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "icon",
                DisplayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                string.Empty,
                0,
                Enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                10,
                Enabled,
                Notes);
        }
    }
}
