using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Biome Palette Template")]
    public sealed class BiomePaletteTemplateAsset : ScriptableObject
    {
        [SerializeField] private string paletteId = "biome-palette";
        [SerializeField] private string displayName = "Biome Palette";
        [SerializeField] private string resourceKey = "palette";
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private BiomePaletteEntryTemplate[] entries = new BiomePaletteEntryTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string PaletteId
        {
            get { return paletteId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string ResourceKey
        {
            get { return resourceKey ?? string.Empty; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public BiomePaletteEntryTemplate[] Entries
        {
            get { return entries ?? new BiomePaletteEntryTemplate[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void ConfigureIdentity(
            string newPaletteId,
            string newDisplayName,
            string newResourceKey,
            int newPriority,
            bool newEnabled,
            string newNotes)
        {
            paletteId = newPaletteId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            resourceKey = string.IsNullOrEmpty(newResourceKey) ? paletteId : newResourceKey;
            priority = newPriority;
            enabled = newEnabled;
            notes = newNotes ?? string.Empty;
        }

        public void SetEntries(IReadOnlyList<BiomePaletteEntryTemplate> newEntries)
        {
            if (newEntries == null || newEntries.Count == 0)
            {
                entries = new BiomePaletteEntryTemplate[0];
                return;
            }

            List<BiomePaletteEntryTemplate> resolvedEntries = new List<BiomePaletteEntryTemplate>();
            for (int i = 0; i < newEntries.Count; i++)
            {
                BiomePaletteEntryTemplate entry = newEntries[i];
                if (entry != null)
                {
                    resolvedEntries.Add(entry);
                }
            }

            entries = resolvedEntries.ToArray();
        }

        public void ClearEntries()
        {
            entries = new BiomePaletteEntryTemplate[0];
        }

        public void ApplySemanticTerrainPreset(
            string floorResourceKey,
            string wallResourceKey,
            string liquidResourceKey,
            string oreResourceKey,
            string objectResourceKey)
        {
            List<BiomePaletteEntryTemplate> presetEntries = new List<BiomePaletteEntryTemplate>();
            AddPresetEntry(presetEntries, "floor", "Floor", BiomePaletteSlotKind.Floor, DimensionAssetReferenceKind.Tile, floorResourceKey, "floor", 0);
            AddPresetEntry(presetEntries, "wall", "Wall", BiomePaletteSlotKind.Wall, DimensionAssetReferenceKind.Tile, wallResourceKey, "wall", 0);
            AddPresetEntry(presetEntries, "liquid", "Liquid", BiomePaletteSlotKind.Liquid, DimensionAssetReferenceKind.Tile, liquidResourceKey, "liquid", 0);
            AddPresetEntry(presetEntries, "ore", "Ore", BiomePaletteSlotKind.Ore, DimensionAssetReferenceKind.Tile, oreResourceKey, "ore", 0);
            AddPresetEntry(presetEntries, "object", "Object", BiomePaletteSlotKind.Object, DimensionAssetReferenceKind.Object, objectResourceKey, "object", 0);
            entries = presetEntries.ToArray();
        }

        private static void AddPresetEntry(
            List<BiomePaletteEntryTemplate> destination,
            string entryId,
            string displayName,
            BiomePaletteSlotKind slotKind,
            DimensionAssetReferenceKind assetKind,
            string resourceKey,
            string variantId,
            int priority)
        {
            if (destination == null || string.IsNullOrEmpty(resourceKey))
            {
                return;
            }

            destination.Add(new BiomePaletteEntryTemplate(
                entryId,
                displayName,
                slotKind,
                assetKind,
                resourceKey,
                variantId,
                priority,
                true,
                string.Empty));
        }

        public DimensionAssetReferenceDefinition ToPaletteAssetReference(
            string contentPackId,
            string dimensionId)
        {
            return new DimensionAssetReferenceDefinition(
                PaletteId,
                contentPackId,
                string.IsNullOrEmpty(DisplayName) ? PaletteId : DisplayName,
                DimensionAssetReferenceKind.Palette,
                string.IsNullOrEmpty(ResourceKey) ? PaletteId : ResourceKey,
                dimensionId,
                string.Empty,
                "palette",
                Priority,
                Enabled,
                Notes);
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            List<DimensionAssetReferenceDefinition> destination)
        {
            if (destination == null || !Enabled)
            {
                return;
            }

            destination.Add(ToPaletteAssetReference(contentPackId, dimensionId));

            BiomePaletteEntryTemplate[] paletteEntries = Entries;
            for (int i = 0; i < paletteEntries.Length; i++)
            {
                BiomePaletteEntryTemplate entry = paletteEntries[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                string entryId = BuildEntryAssetId(PaletteId, entry.EntryId, i);
                destination.Add(entry.ToAssetReference(entryId, contentPackId, dimensionId));
            }
        }

        private static string BuildEntryAssetId(string paletteId, string entryId, int index)
        {
            string resolvedEntryId = string.IsNullOrEmpty(entryId) ? "entry-" + index.ToString() : entryId;
            if (resolvedEntryId.IndexOf('.') >= 0 || resolvedEntryId.IndexOf(':') >= 0)
            {
                return resolvedEntryId;
            }

            if (string.IsNullOrEmpty(paletteId))
            {
                return resolvedEntryId;
            }

            return (paletteId ?? string.Empty) + "." + resolvedEntryId;
        }
    }

    [System.Serializable]
    public sealed class BiomePaletteEntryTemplate
    {
        [SerializeField] private string entryId = "floor";
        [SerializeField] private string displayName = "Floor";
        [SerializeField] private BiomePaletteSlotKind slotKind = BiomePaletteSlotKind.Floor;
        [SerializeField] private DimensionAssetReferenceKind assetKind = DimensionAssetReferenceKind.Tile;
        [SerializeField] private string resourceKey = string.Empty;
        [SerializeField] private string variantId = string.Empty;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public BiomePaletteEntryTemplate()
        {
        }

        public BiomePaletteEntryTemplate(
            string entryId,
            string displayName,
            BiomePaletteSlotKind slotKind,
            DimensionAssetReferenceKind assetKind,
            string resourceKey,
            string variantId,
            int priority,
            bool enabled,
            string notes)
        {
            Configure(
                entryId,
                displayName,
                slotKind,
                assetKind,
                resourceKey,
                variantId,
                priority,
                enabled,
                notes);
        }

        public string EntryId
        {
            get { return entryId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public BiomePaletteSlotKind SlotKind
        {
            get { return slotKind; }
        }

        public DimensionAssetReferenceKind AssetKind
        {
            get { return assetKind; }
        }

        public string ResourceKey
        {
            get { return resourceKey ?? string.Empty; }
        }

        public string VariantId
        {
            get
            {
                if (!string.IsNullOrEmpty(variantId))
                {
                    return variantId;
                }

                return slotKind.ToString().ToLowerInvariant();
            }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void Configure(
            string newEntryId,
            string newDisplayName,
            BiomePaletteSlotKind newSlotKind,
            DimensionAssetReferenceKind newAssetKind,
            string newResourceKey,
            string newVariantId,
            int newPriority,
            bool newEnabled,
            string newNotes)
        {
            entryId = newEntryId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            slotKind = newSlotKind;
            assetKind = newAssetKind;
            resourceKey = newResourceKey ?? string.Empty;
            variantId = newVariantId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
            notes = newNotes ?? string.Empty;
        }

        public DimensionAssetReferenceDefinition ToAssetReference(
            string assetId,
            string contentPackId,
            string dimensionId)
        {
            return new DimensionAssetReferenceDefinition(
                assetId,
                contentPackId,
                string.IsNullOrEmpty(DisplayName) ? assetId : DisplayName,
                AssetKind,
                ResourceKey,
                dimensionId,
                string.Empty,
                VariantId,
                Priority,
                Enabled,
                Notes);
        }
    }

    public enum BiomePaletteSlotKind
    {
        Custom = 0,
        Floor = 1,
        Wall = 2,
        Liquid = 3,
        Ore = 4,
        Object = 5,
        Decoration = 6,
        Structure = 7,
        Scene = 8,
        Mob = 9,
        Boss = 10,
        Item = 11,
        Sprite = 12,
        Material = 13,
        Audio = 14,
        Music = 15,
        Lighting = 16,
        Fog = 17,
        MapColor = 18
    }
}
