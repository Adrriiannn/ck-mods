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
    /// Turning a value into the C# or JSON text the emitter writes, and the file writes it ends in.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static string BuildIntArrayLiteral(List<int> values)
        {
            StringBuilder literal = new StringBuilder("new int[] { ");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    literal.Append(", ");
                }

                literal.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }

            literal.Append(" }");
            return literal.ToString();
        }

        private static string QualifyIfOwn(
            DimensionTemplateAsset template,
            string modName,
            string objectId)
        {
            return IsModOwnedObjectName(template, objectId)
                ? DimensionObjectNamespace.Qualify(modName, objectId)
                : objectId;
        }

        private static string QualifiedArrayLiteral(
            DimensionTemplateAsset template,
            string modName,
            string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return "new string[0]";
            }

            StringBuilder literal = new StringBuilder("new string[] { ");
            bool wrote = false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    continue;
                }

                if (wrote)
                {
                    literal.Append(", ");
                }

                literal.Append(ToCSharpString(QualifyIfOwn(template, modName, ids[i])));
                wrote = true;
            }

            literal.Append(" }");
            return wrote ? literal.ToString() : "new string[0]";
        }

        private static void WriteTextAssetIfChanged(string assetPath, string content)
        {
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath))
            {
                return;
            }

            string folder = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(absolutePath) &&
                File.ReadAllText(absolutePath) == content)
            {
                return;
            }

            File.WriteAllText(absolutePath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteBinaryAssetIfChanged(string assetPath, byte[] content)
        {
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath) || content == null)
            {
                return;
            }

            string folder = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (File.Exists(absolutePath))
            {
                byte[] existing = File.ReadAllBytes(absolutePath);
                if (existing.Length == content.Length)
                {
                    bool equal = true;
                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i] != content[i])
                        {
                            equal = false;
                            break;
                        }
                    }

                    if (equal)
                    {
                        return;
                    }
                }
            }

            File.WriteAllBytes(absolutePath, content);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AppendConstant(StringBuilder builder, string name, string value)
        {
            builder.Append("  private const string ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(ToCSharpString(value));
            builder.AppendLine(";");
        }

        private static void AppendFloatConstant(StringBuilder builder, string name, float value)
        {
            builder.Append("  private const float ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(FormatFloat(value));
            builder.AppendLine(";");
        }

        private static void AppendIntConstant(StringBuilder builder, string name, int value)
        {
            builder.Append("  private const int ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(";");
        }

        private static void AppendBoolConstant(StringBuilder builder, string name, bool value)
        {
            builder.Append("  private const bool ");
            builder.Append(name);
            builder.Append(" = ");
            builder.Append(value ? "true" : "false");
            builder.AppendLine(";");
        }

        private static string ToCSharpString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return ToJsonString(value);
        }

        private static string ToJsonString(string value)
        {
            string source = value ?? string.Empty;
            return "\"" +
                source
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t") +
                "\"";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "f";
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            StringBuilder builder = new StringBuilder();
            bool makeUpper = true;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (!valid)
                {
                    makeUpper = true;
                    continue;
                }

                if (builder.Length == 0 && character >= '0' && character <= '9')
                {
                    builder.Append(fallback);
                }

                if (makeUpper && character >= 'a' && character <= 'z')
                {
                    character = (char)(character - 32);
                }

                builder.Append(character);
                makeUpper = false;
            }

            return builder.Length == 0 ? fallback : builder.ToString();
        }

        private static string GetLastPathSegment(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        private static string GetFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? string.Empty : normalized.Substring(0, slash);
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets/"))
            {
                return string.Empty;
            }

            string assetsRoot = NormalizeAssetPath(Application.dataPath);
            return Path.Combine(
                assetsRoot,
                normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AbsolutePathToAssetPath(string absolutePath)
        {
            string normalized = NormalizeAssetPath(absolutePath);
            string assetsRoot = NormalizeAssetPath(Application.dataPath);
            if (normalized == assetsRoot)
            {
                return "Assets";
            }

            string prefix = assetsRoot + "/";
            if (!normalized.StartsWith(prefix))
            {
                return string.Empty;
            }

            return "Assets/" + normalized.Substring(prefix.Length);
        }
    }
}
