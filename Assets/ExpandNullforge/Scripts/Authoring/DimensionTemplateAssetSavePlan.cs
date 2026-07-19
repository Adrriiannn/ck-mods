using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public readonly struct DimensionTemplateAssetSavePlanEntry
    {
        public readonly Object Asset;
        public readonly string Role;
        public readonly string SuggestedPath;
        public readonly string SuggestedName;
        public readonly bool Required;
        public readonly string Notes;

        public DimensionTemplateAssetSavePlanEntry(
            Object asset,
            string role,
            string suggestedPath,
            string suggestedName,
            bool required,
            string notes)
        {
            Asset = asset;
            Role = role ?? string.Empty;
            SuggestedPath = suggestedPath ?? string.Empty;
            SuggestedName = suggestedName ?? string.Empty;
            Required = required;
            Notes = notes ?? string.Empty;
        }
    }

    public sealed class DimensionTemplateAssetSavePlan
    {
        public DimensionTemplateAssetSavePlan(
            string rootFolder,
            string dimensionId,
            string code,
            string message,
            IReadOnlyList<DimensionTemplateAssetSavePlanEntry> entries)
        {
            RootFolder = rootFolder ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Entries = entries ?? new List<DimensionTemplateAssetSavePlanEntry>();
        }

        public string RootFolder { get; private set; }

        public string DimensionId { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public IReadOnlyList<DimensionTemplateAssetSavePlanEntry> Entries { get; private set; }
    }

    public static class DimensionTemplateAssetSavePlanUtility
    {
        private const string DefaultRootFolder = "Assets/DimensionAssets";

        public static DimensionTemplateAssetSavePlan CreateForStarterGraph(
            DimensionTemplateStarterGraph graph,
            string rootFolder)
        {
            string normalizedRoot = NormalizeFolder(rootFolder);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                normalizedRoot = DefaultRootFolder;
            }

            if (graph == null || graph.Dimension == null)
            {
                return new DimensionTemplateAssetSavePlan(
                    normalizedRoot,
                    string.Empty,
                    "starter-graph-missing",
                    "No starter dimension graph is available.",
                    new List<DimensionTemplateAssetSavePlanEntry>());
            }

            string dimensionId = SafeName(graph.Dimension.DimensionId, graph.Dimension.name, "dimension");
            string dimensionFolder = EndsWithFolderSegment(normalizedRoot, dimensionId)
                ? normalizedRoot
                : normalizedRoot + "/" + dimensionId;
            List<DimensionTemplateAssetSavePlanEntry> entries =
                new List<DimensionTemplateAssetSavePlanEntry>();

            AddEntry(entries, graph.Dimension, "Dimension", dimensionFolder, dimensionId, true,
                "Root Dimension Asset. Save this first so editor tooling has one primary asset to select.");
            AddEntry(entries, graph.PortalVisualProfile, "Portal Visual Profile", dimensionFolder, "PortalVisualProfile", false,
                "Vanilla-default portal appearance profile. Edit this asset through the dashboard to customize each portal visual layer.");
            AddEntry(entries, graph.Layout, "Layout", dimensionFolder + "/Layout", "layout", true,
                "Biome positioning template used by the dimension.");
            AddEntry(entries, graph.Biome, "Biome", dimensionFolder + "/Biomes", SafeName(graph.Biome, "starter-biome"), true,
                "Starter biome template.");
            AddEntry(entries, graph.EnvironmentProfile, "Environment Profile", dimensionFolder + "/Environment", SafeName(graph.EnvironmentProfile, "environment"), false,
                "Reusable environment defaults for lighting, music, ambient, and map color.");
            AddEntry(entries, graph.Palette, "Biome Palette", dimensionFolder + "/Palettes", SafeName(graph.Palette, "palette"), false,
                "Semantic asset palette for terrain, objects, mobs, bosses, audio, and custom references.");
            AddEntry(entries, graph.GenerationProfile, "Generation Profile", dimensionFolder + "/Generation", SafeName(graph.GenerationProfile, "generation"), false,
                "Reusable generation defaults for the biome.");

            AddEntries(entries, graph.GenerationPasses, "Generation Pass", dimensionFolder + "/Generation/Passes", false,
                "Ordered generation pass. A starter graph without provider-backed passes is editable/exportable but not runtime-generatable.");
            AddEntries(entries, graph.GenerationTables, "Generation Table", dimensionFolder + "/Generation/Tables", false,
                "Weighted generation table.");
            AddEntries(entries, graph.PortalAccessRules, "Portal Access Rule", dimensionFolder + "/Access", false,
                "Portal entry and return rules. Placed entry portals use a vanilla-style activation cooldown; generated return portals stay immediately available.");

            return new DimensionTemplateAssetSavePlan(
                normalizedRoot,
                dimensionId,
                "ready",
                "Save plan prepared. This plan is advisory only and does not create folders or assets.",
                entries);
        }

        private static void AddEntries<T>(
            List<DimensionTemplateAssetSavePlanEntry> entries,
            IReadOnlyList<T> assets,
            string role,
            string folder,
            bool required,
            string notes)
            where T : Object
        {
            if (entries == null || assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                AddEntry(entries, asset, role, folder, SafeName(asset, role), required, notes);
            }
        }

        private static void AddEntry(
            List<DimensionTemplateAssetSavePlanEntry> entries,
            Object asset,
            string role,
            string folder,
            string assetName,
            bool required,
            string notes)
        {
            if (entries == null || asset == null)
            {
                return;
            }

            string safeAssetName = SafeName(assetName, asset.name, role);
            string normalizedFolder = NormalizeFolder(folder);
            string suggestedPath = normalizedFolder + "/" + safeAssetName + ".asset";
            entries.Add(new DimensionTemplateAssetSavePlanEntry(
                asset,
                role,
                suggestedPath,
                safeAssetName,
                required,
                notes));
        }

        private static string SafeName(Object asset, string fallback)
        {
            return SafeName(asset == null ? string.Empty : asset.name, fallback, "asset");
        }

        private static string SafeName(string primary, string secondary, string fallback)
        {
            string value = !string.IsNullOrEmpty(primary)
                ? primary
                : !string.IsNullOrEmpty(secondary)
                    ? secondary
                    : fallback;
            if (string.IsNullOrEmpty(value))
            {
                value = "asset";
            }

            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_' ||
                    c == '.';
                if (!valid)
                {
                    buffer[i] = '-';
                }
            }

            return new string(buffer);
        }

        private static bool EndsWithFolderSegment(string folder, string segment)
        {
            string normalizedFolder = NormalizeFolder(folder);
            string normalizedSegment = SafeName(segment, string.Empty, string.Empty);
            if (string.IsNullOrEmpty(normalizedFolder) ||
                string.IsNullOrEmpty(normalizedSegment))
            {
                return false;
            }

            return normalizedFolder == normalizedSegment ||
                normalizedFolder.EndsWith("/" + normalizedSegment);
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            string normalized = folder.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }
    }
}
