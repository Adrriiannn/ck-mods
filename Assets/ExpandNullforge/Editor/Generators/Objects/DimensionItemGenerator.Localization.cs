using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The names and descriptions the mod writes into its localization file.
    /// </summary>
    internal static partial class DimensionItemGenerator
    {
        /// <summary>
        /// Writes the item names and tooltips into the mod's localization table. Without this an
        /// item shows its raw id in-game, so generation is not complete until it runs.
        /// </summary>
        private static void WriteLocalization(
            string modRoot,
            List<DimensionLocalizationCsv.Row> rows,
            List<string> retiredKeys,
            DimensionItemGenerationReport report)
        {
            if (rows.Count == 0)
            {
                return;
            }

            string folder = modRoot + "/Localization";
            if (!DimensionAssetFolders.EnsureExists(folder))
            {
                report.Warnings.Add(
                    "Could not create '" + folder +
                    "', so item names were not added to the localization table. Items will " +
                    "show their raw ids in-game.");
                return;
            }

            string path = folder + "/Localization.csv";
            string absolutePath = ToAbsolutePath(path);
            try
            {
                string existing = System.IO.File.Exists(absolutePath)
                    ? System.IO.File.ReadAllText(absolutePath)
                    : null;
                string merged = DimensionLocalizationCsv.Merge(existing, rows, retiredKeys);
                if (!string.Equals(existing, merged, StringComparison.Ordinal))
                {
                    System.IO.File.WriteAllText(absolutePath, merged);
                    AssetDatabase.ImportAsset(path);
                }
            }
            catch (Exception exception)
            {
                report.Warnings.Add(
                    "Could not update '" + path + "': " + exception.Message);
            }
        }

        /// <summary>
        /// The name of the mod that owns <paramref name="outputFolder"/>, used to namespace every
        /// object this run generates. Empty when no owning mod can be found, in which case the
        /// caller warns rather than inventing a prefix — a wrong namespace is worse than none,
        /// because it silently renames content the player may already have in a save.
        /// </summary>
        private static string ResolveModName(string outputFolder)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(outputFolder);
            if (settings == null)
            {
                return string.Empty;
            }

            // ModMetadata.name is the mod's canonical id; displayName is a label and may change
            // without the content changing, so it must not decide object identity.
            return settings.metadata.name ?? string.Empty;
        }

        private static string ResolveModRoot(string outputFolder)
        {
            return outputFolder.EndsWith("/Items", StringComparison.Ordinal)
                ? outputFolder.Substring(0, outputFolder.Length - "/Items".Length)
                : outputFolder;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            return System.IO.Path.Combine(projectRoot ?? string.Empty, assetPath)
                .Replace('\\', '/');
        }

        /// <summary>
        /// Every boss's floating-name row, and the pin's hover row when it differs.
        /// </summary>
        /// <remarks>
        /// Without the row, the nameplate and the map hover both render the raw term —
        /// "Names/mod_boss" floating over the fight. Visible and debuggable, but not a name.
        /// </remarks>
        private static void AppendBossNameLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<DimensionBossAsset> bosses)
        {
            if (rows == null || bosses == null)
            {
                return;
            }

            foreach (DimensionBossAsset boss in bosses)
            {
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string qualified = naming.QualifyGenerated(boss.BossId);
                DimensionLocalizationCsv.AddNameRow(
                    rows,
                    qualified,
                    string.IsNullOrEmpty(boss.DisplayName) ? boss.BossId : boss.DisplayName);

                if (!string.IsNullOrEmpty(boss.MapPin.HoverName))
                {
                    DimensionLocalizationCsv.AddNameRow(rows, qualified + "-pin", boss.MapPin.HoverName);
                }
            }
        }

        /// <summary>The named areas' title rows, under the same synthetic ids the registries use.</summary>
        private static void AppendNamedAreaTitleLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<DimensionNamedAreaAsset> namedAreas)
        {
            if (rows == null || namedAreas == null)
            {
                return;
            }

            foreach (DimensionNamedAreaAsset area in namedAreas)
            {
                if (area == null || !area.Enabled || !area.ShowTitleOnDiscovery ||
                    string.IsNullOrEmpty(area.AreaId))
                {
                    continue;
                }

                DimensionLocalizationCsv.AddBiomeTitleRow(
                    rows,
                    DimensionBiomeTitleTerms.ForBiome(naming.ModName, "area:" + area.AreaId),
                    string.IsNullOrEmpty(area.DisplayName) ? area.AreaId : area.DisplayName);
            }
        }

        private static void AppendBiomeTitleLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<BiomeTemplateAsset> biomes)
        {
            if (rows == null || biomes == null)
            {
                return;
            }

            foreach (BiomeTemplateAsset biome in biomes)
            {
                if (biome == null || !biome.Enabled || !biome.ShowTitleOnDiscovery)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                DimensionLocalizationCsv.AddBiomeTitleRow(
                    rows,
                    DimensionBiomeTitleTerms.ForBiome(naming.ModName, biome.BiomeId),
                    string.IsNullOrEmpty(biome.DisplayName) ? biome.BiomeId : biome.DisplayName);
            }
        }

        private static void ConfigureLocalization(
            GameObject root,
            DimensionItemAsset item,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report)
        {
            LocalizationAuthoring localization = EnsureComponent<LocalizationAuthoring>(root);

            // Must be the QUALIFIED name: the CSV rows this run writes are keyed by the qualified
            // object name, so an unqualified term key would look up a row that is not there and the
            // item would show its raw id in game.
            localization.termKey = naming.QualifyGenerated(item.ItemId);
            // LanguageGender is a PAIR, not an enum: a SystemLanguage and a Gender. Authors write it
            // as "English:Neutral" so one plain text field covers both halves, which is why this
            // does not go through the usual single-enum parse.
            localization.languageGenders = new List<LanguageGender>();
            string[] genders = item.NameGendersPerLanguage;
            for (int g = 0; g < genders.Length; g++)
            {
                if (string.IsNullOrEmpty(genders[g]))
                {
                    continue;
                }

                string[] halves = genders[g].Split(':');
                SystemLanguage language;
                Gender gender;
                if (halves.Length == 2 &&
                    System.Enum.TryParse(halves[0].Trim(), false, out language) &&
                    System.Enum.TryParse(halves[1].Trim(), false, out gender))
                {
                    localization.languageGenders.Add(
                        new LanguageGender { language = language, gender = gender });
                }
                else
                {
                    report.Warnings.Add(
                        Describe(item) + ": cannot read '" + genders[g] + "' as a language and a " +
                        "gender. Write it as English:Neutral.");
                }
            }

            if (localization.languageGenders.Count == 0)
            {
                TrySetArraySize(localization, "languageGenders", 0);
            }
        }
    }
}
