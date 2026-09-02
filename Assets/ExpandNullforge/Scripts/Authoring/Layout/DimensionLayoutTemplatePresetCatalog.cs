using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public readonly struct DimensionLayoutTemplatePresetDescriptor
    {
        public readonly string PresetId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly DimensionLayoutKind LayoutKind;
        public readonly int MinimumBiomeSlots;
        public readonly int RecommendedBiomeSlots;

        public DimensionLayoutTemplatePresetDescriptor(
            string presetId,
            string displayName,
            string description,
            DimensionLayoutKind layoutKind,
            int minimumBiomeSlots,
            int recommendedBiomeSlots)
        {
            PresetId = presetId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            LayoutKind = layoutKind;
            MinimumBiomeSlots = minimumBiomeSlots < 1 ? 1 : minimumBiomeSlots;
            RecommendedBiomeSlots = recommendedBiomeSlots < MinimumBiomeSlots
                ? MinimumBiomeSlots
                : recommendedBiomeSlots;
        }
    }

    public sealed class DimensionLayoutTemplatePresetRequest
    {
        public string PresetId = DimensionLayoutTemplatePresetCatalog.SingleBiomeSquarePresetId;
        public string PrimaryBiomeId = "starter";
        public string SecondaryBiomeId = string.Empty;
        public string ZoneId = "starter";
        public int HalfSizeTiles = 64;
        public int Columns = 3;
        public int Rows = 3;
        public int CellSizeTiles = 64;
        public int RingWidthTiles = 64;
        public int RingCount = 3;
        public string[] BiomeIds = new string[0];
    }

    public static class DimensionLayoutTemplatePresetCatalog
    {
        public const string SingleBiomeSquarePresetId = "single-biome-square";
        public const string CenteredGridPresetId = "centered-grid";
        public const string RadialRingsPresetId = "radial-rings";

        private static readonly DimensionLayoutTemplatePresetDescriptor[] BuiltInPresets =
        {
            new DimensionLayoutTemplatePresetDescriptor(
                SingleBiomeSquarePresetId,
                "Single Biome Starter",
                "One centered rectangular starter biome. Best for testing, arenas, rooms, and tightly authored dimensions.",
                DimensionLayoutKind.ManualRegions,
                1,
                1),
            new DimensionLayoutTemplatePresetDescriptor(
                RadialRingsPresetId,
                "Radial Rings Starter",
                "Circular/annular starter bands around local 0,0. Best for vanilla-like distance bands and core-outward progression.",
                DimensionLayoutKind.RadialRings,
                1,
                3)
        };

        public static IReadOnlyList<DimensionLayoutTemplatePresetDescriptor> GetBuiltInPresets()
        {
            return BuiltInPresets;
        }

        public static bool TryGetBuiltInPreset(
            string presetId,
            out DimensionLayoutTemplatePresetDescriptor preset)
        {
            string resolvedId = presetId ?? string.Empty;
            for (int i = 0; i < BuiltInPresets.Length; i++)
            {
                if (BuiltInPresets[i].PresetId == resolvedId)
                {
                    preset = BuiltInPresets[i];
                    return true;
                }
            }

            preset = default(DimensionLayoutTemplatePresetDescriptor);
            return false;
        }

        public static bool TryApplyBuiltInPreset(
            DimensionLayoutTemplateAsset layout,
            DimensionLayoutTemplatePresetRequest request,
            out string message)
        {
            message = string.Empty;
            if (layout == null)
            {
                message = "Layout template is missing.";
                return false;
            }

            DimensionLayoutTemplatePresetRequest resolved =
                request ?? new DimensionLayoutTemplatePresetRequest();
            string presetId = string.IsNullOrEmpty(resolved.PresetId)
                ? SingleBiomeSquarePresetId
                : resolved.PresetId;
            string primaryBiomeId = string.IsNullOrEmpty(resolved.PrimaryBiomeId)
                ? "starter"
                : resolved.PrimaryBiomeId;
            string secondaryBiomeId = resolved.SecondaryBiomeId ?? string.Empty;
            string zoneId = string.IsNullOrEmpty(resolved.ZoneId)
                ? primaryBiomeId
                : resolved.ZoneId;

            if (presetId == SingleBiomeSquarePresetId)
            {
                layout.ApplySingleBiomeSquarePreset(
                    primaryBiomeId,
                    zoneId,
                    resolved.HalfSizeTiles);
                message = "Single-biome square layout applied.";
                return true;
            }

            if (presetId == CenteredGridPresetId)
            {
                layout.ApplyCenteredGridPreset(
                    primaryBiomeId,
                    zoneId,
                    resolved.Columns,
                    resolved.Rows,
                    resolved.CellSizeTiles,
                    resolved.BiomeIds);
                message = "Centered grid layout applied.";
                return true;
            }

            if (presetId == RadialRingsPresetId)
            {
                layout.ApplyRadialRingsPreset(
                    primaryBiomeId,
                    secondaryBiomeId,
                    zoneId,
                    resolved.RingWidthTiles,
                    resolved.RingCount);
                message = "Radial-rings layout applied.";
                return true;
            }

            message = "Unknown layout preset: " + presetId;
            return false;
        }
    }
}
