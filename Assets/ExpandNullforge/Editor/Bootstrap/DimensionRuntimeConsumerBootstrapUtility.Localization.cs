using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
using ExpandNullforge.Scenes;
using Pug.Sprite;
using PugMod;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The portal text data blocks and the localization rows that name them.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static void EnsurePortalTextDataBlocks(
            DimensionRuntimePortalOutput portalOutput,
            string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot) ||
                string.IsNullOrEmpty(portalOutput.PortalObjectName))
            {
                return;
            }

            string folder = modRoot + "/Data/TextDataBlock/Items";
            DimensionAssetFolders.Ensure(folder);

            string displayName = portalOutput.PortalDisplayName;
            string description = BuildPortalDescription(portalOutput);
            EnsurePortalTextDataBlockAsset(
                folder,
                portalOutput.PortalObjectName,
                displayName,
                description);

            string aliasObjectName = BuildPortalLocalizationAliasObjectName(portalOutput.PortalObjectName);
            if (!string.IsNullOrEmpty(aliasObjectName) &&
                !string.Equals(aliasObjectName, portalOutput.PortalObjectName, System.StringComparison.Ordinal))
            {
                EnsurePortalTextDataBlockAsset(
                    folder,
                    aliasObjectName,
                    displayName,
                    description);
            }
        }

        private static void EnsurePortalTextDataBlockAsset(
            string folder,
            string objectName,
            string displayName,
            string description)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            string path = folder + "/" + SanitizeAssetFileName(objectName, "DimensionPortal") + ".asset";
            string content = BuildPortalTextDataBlockYaml(objectName, displayName, description);
            WriteTextAssetIfChanged(path, content);
        }

        private static string BuildPortalTextDataBlockYaml(
            string objectName,
            string displayName,
            string description)
        {
            long addressLow = DimensionSpriteAssetAddress.Part(objectName, 0x6E756C6C666F7267UL);
            long addressHigh = DimensionSpriteAssetAddress.Part(objectName, 0x657870616E646E66UL);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!114 &11400000");
            builder.AppendLine("MonoBehaviour:");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
            builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
            builder.AppendLine("  m_GameObject: {fileID: 0}");
            builder.AppendLine("  m_Enabled: 1");
            builder.AppendLine("  m_EditorHideFlags: 0");
            builder.Append("  m_Script: {fileID: 2108018792, guid: ")
                .Append(TextDataBlockScriptGuid)
                .AppendLine(", type: 3}");
            builder.Append("  m_Name: ").AppendLine(ToUnityYamlString(objectName));
            builder.AppendLine("  m_EditorClassIdentifier: ");
            builder.AppendLine("  m_overload:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_address:");
            builder.Append("    m_low: ").AppendLine(addressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("    m_high: ").AppendLine(addressHigh.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  m_dynamicCollections:");
            builder.AppendLine("    m_list: []");
            builder.AppendLine("  m_localizedTexts:");
            builder.AppendLine("    keys:");
            AppendEnglishLanguageAddressListItem(builder);
            builder.AppendLine("    values:");
            builder.AppendLine("    - m_language:");
            builder.AppendLine("        m_address:");
            AppendEnglishLanguageAddress(builder, "          ");
            builder.Append("      title: ").AppendLine(ToUnityYamlString(displayName));
            builder.Append("      description: ").AppendLine(ToUnityYamlString(description));
            builder.AppendLine("  m_localizationHint: ");
            builder.AppendLine("  m_prevImportPrimaryEntry:");
            builder.AppendLine("    m_language:");
            builder.AppendLine("      m_address:");
            AppendEnglishLanguageAddress(builder, "        ");
            builder.Append("    title: ").AppendLine(ToUnityYamlString(displayName));
            builder.Append("    description: ").AppendLine(ToUnityYamlString(description));
            builder.AppendLine("  m_shouldBeLocalized: 1");
            builder.AppendLine("  m_header: Items");
            builder.AppendLine("  references:");
            builder.AppendLine("    version: 2");
            builder.AppendLine("    RefIds: []");
            return builder.ToString();
        }

        private static void AppendEnglishLanguageAddressListItem(StringBuilder builder)
        {
            builder.Append("    - m_low: ")
                .AppendLine(EnglishLanguageAddressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("      m_high: ")
                .AppendLine(EnglishLanguageAddressHigh.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendEnglishLanguageAddress(StringBuilder builder, string indent)
        {
            builder.Append(indent)
                .Append("m_low: ")
                .AppendLine(EnglishLanguageAddressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append(indent)
                .Append("m_high: ")
                .AppendLine(EnglishLanguageAddressHigh.ToString(CultureInfo.InvariantCulture));
        }

        private static void EnsurePortalLocalization(
            DimensionRuntimePortalOutput portalOutput,
            string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot) ||
                string.IsNullOrEmpty(portalOutput.PortalObjectName))
            {
                return;
            }

            string localizationFolder = modRoot + "/Localization";
            DimensionAssetFolders.Ensure(localizationFolder);

            string path = localizationFolder + "/Localization.csv";
            string absolutePath = AssetPathToAbsolutePath(path);
            string displayName = portalOutput.PortalDisplayName;
            string description = BuildPortalDescription(portalOutput);
            List<GeneratedLocalizationRow> generatedRows = new List<GeneratedLocalizationRow>();
            AddPortalLocalizationRows(
                generatedRows,
                portalOutput.PortalObjectName,
                displayName,
                description);

            string aliasObjectName = BuildPortalLocalizationAliasObjectName(portalOutput.PortalObjectName);
            if (!string.IsNullOrEmpty(aliasObjectName) &&
                !string.Equals(aliasObjectName, portalOutput.PortalObjectName, System.StringComparison.Ordinal))
            {
                AddPortalLocalizationRows(
                    generatedRows,
                    aliasObjectName,
                    displayName,
                    description);
            }

            string content = BuildPortalLocalizationCsv(
                absolutePath,
                portalOutput.PortalObjectName,
                generatedRows);
            WriteTextAssetIfChanged(path, content);
        }

        private static void AddPortalLocalizationRows(
            List<GeneratedLocalizationRow> rows,
            string objectName,
            string displayName,
            string description)
        {
            if (rows == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            // The game's LocalizationManager replaces ':' with '_' in every term before lookup,
            // so keys must be written in that form or they can never resolve.
            string itemKey = "Items/" + objectName.Replace(':', '_');
            rows.Add(new GeneratedLocalizationRow(itemKey, displayName));
            rows.Add(new GeneratedLocalizationRow(itemKey + "Desc", description));
        }

        private static string BuildPortalLocalizationAliasObjectName(string portalObjectName)
        {
            const string portalSuffix = "_Portal";
            if (string.IsNullOrEmpty(portalObjectName) ||
                !portalObjectName.EndsWith(portalSuffix, System.StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return portalObjectName.Substring(0, portalObjectName.Length - portalSuffix.Length) + "Portal";
        }

        private static string BuildPortalLocalizationCsv(
            string absolutePath,
            string portalObjectName,
            List<GeneratedLocalizationRow> generatedRows)
        {
            const string fallbackHeader = "Key\tType\tDesc\tEnglish";
            List<string> lines = new List<string>();
            List<string> ownedKeys = BuildOwnedPortalLocalizationKeys(portalObjectName, generatedRows);
            if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
            {
                string[] existingLines = File.ReadAllText(absolutePath)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');
                for (int i = 0; i < existingLines.Length; i++)
                {
                    string line = existingLines[i];
                    if (i == 0)
                    {
                        line = line.TrimStart('\ufeff');
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (lines.Count == 0 && !line.StartsWith("Key\t", System.StringComparison.Ordinal))
                    {
                        lines.Add(fallbackHeader);
                    }

                    if (CsvLineHasAnyKey(line, ownedKeys))
                    {
                        continue;
                    }

                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                lines.Add(fallbackHeader);
            }

            int columnCount = lines[0].Split('\t').Length;
            if (columnCount < 4)
            {
                lines[0] = fallbackHeader;
                columnCount = 4;
            }

            if (generatedRows != null)
            {
                foreach (GeneratedLocalizationRow row in generatedRows)
                {
                    lines.Add(BuildLocalizationRow(row.Key, row.EnglishText, columnCount));
                }
            }

            return string.Join("\n", lines.ToArray()) + "\n";
        }

        private static List<string> BuildOwnedPortalLocalizationKeys(
            string portalObjectName,
            List<GeneratedLocalizationRow> generatedRows)
        {
            List<string> ownedKeys = new List<string>();
            if (!string.IsNullOrEmpty(portalObjectName))
            {
                ownedKeys.Add("terms/" + portalObjectName);
                ownedKeys.Add("terms/" + portalObjectName + "Desc");

                // Colon-form keys are unresolvable (the game replaces ':' with '_' before every
                // lookup) — own them so stale rows written before the fix are dropped on rewrite.
                if (portalObjectName.IndexOf(':') >= 0)
                {
                    ownedKeys.Add("Items/" + portalObjectName);
                    ownedKeys.Add("Items/" + portalObjectName + "Desc");
                }
            }

            if (generatedRows != null)
            {
                foreach (GeneratedLocalizationRow row in generatedRows)
                {
                    if (!string.IsNullOrEmpty(row.Key))
                    {
                        ownedKeys.Add(row.Key);
                    }
                }
            }

            return ownedKeys;
        }

        private static bool CsvLineHasAnyKey(string line, List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            foreach (string key in keys)
            {
                if (CsvLineHasKey(line, key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CsvLineHasKey(string line, string key)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            int tabIndex = line.IndexOf('\t');
            string rowKey = tabIndex < 0 ? line : line.Substring(0, tabIndex);
            return string.Equals(rowKey, key, System.StringComparison.Ordinal);
        }

        private static string BuildLocalizationRow(
            string key,
            string englishText,
            int columnCount)
        {
            string[] columns = new string[Mathf.Max(4, columnCount)];
            columns[0] = SanitizeLocalizationCell(key);
            columns[1] = "Text";
            columns[2] = string.Empty;
            columns[3] = SanitizeLocalizationCell(englishText);
            return string.Join("\t", columns);
        }

        private readonly struct GeneratedLocalizationRow
        {
            public readonly string Key;
            public readonly string EnglishText;

            public GeneratedLocalizationRow(string key, string englishText)
            {
                Key = key ?? string.Empty;
                EnglishText = englishText ?? string.Empty;
            }
        }

        private static string SanitizeLocalizationCell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private static string ToUnityYamlString(string value)
        {
            string sanitized = SanitizeLocalizationCell(value);
            return "\"" +
                sanitized
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") +
                "\"";
        }

        private static string SanitizeAssetFileName(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                bool invalid = c == '/' || c == '\\';
                for (int j = 0; j < invalidChars.Length && !invalid; j++)
                {
                    invalid = c == invalidChars[j];
                }

                builder.Append(invalid ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static string BuildPortalDescription(DimensionRuntimePortalOutput portalOutput)
        {
            return "Portal to " + portalOutput.DimensionDisplayName + ".";
        }
    }
}
