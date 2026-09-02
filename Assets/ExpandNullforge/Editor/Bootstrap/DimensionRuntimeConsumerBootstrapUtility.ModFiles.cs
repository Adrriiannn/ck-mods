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
    /// Editing the consumer mod own asmdef and mod.json so it loads and can be joined.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static void EnsureConsumerAssemblyReferences(string modRoot)
        {
            string absoluteRoot = AssetPathToAbsolutePath(modRoot);
            if (string.IsNullOrEmpty(absoluteRoot) || !Directory.Exists(absoluteRoot))
            {
                return;
            }

            string[] asmdefs = Directory.GetFiles(absoluteRoot, "*.asmdef", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < asmdefs.Length; i++)
            {
                string asmdefPath = AbsolutePathToAssetPath(asmdefs[i]);
                if (string.IsNullOrEmpty(asmdefPath))
                {
                    continue;
                }

                EnsureAssemblyReferences(asmdefPath);
            }
        }

        private static void EnsureAssemblyReferences(string asmdefPath)
        {
            string absolutePath = AssetPathToAbsolutePath(asmdefPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return;
            }

            string text = File.ReadAllText(absolutePath);
            bool useGuids = text.Contains("\"useGUIDs\": true");
            string frameworkReference = useGuids
                ? AssetDatabase.AssetPathToGUID("Assets/ExpandNullforge/ExpandNullforge.asmdef")
                : FrameworkAssemblyReference;
            string apiReference = useGuids
                ? AssetDatabase.AssetPathToGUID("Assets/ExpandNullforge/API/ExpandNullforge.API.asmdef")
                : ApiAssemblyReference;
            if (EnsureJsonStringArrayContains(
                absolutePath,
                "references",
                new[] { frameworkReference, apiReference }))
            {
                AssetDatabase.ImportAsset(asmdefPath);
            }
        }

        /// <summary>
        /// Sets the two switches on the creator's mod that decide whether it runs at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both are off on a new mod, and neither shows a problem while you are building. The first
        /// one, "accesses extra assemblies", decides which assemblies the game lets the mod's code
        /// see when it compiles the mod ON LOAD. Everything this framework generates is written
        /// against the framework's own namespaces, so without it the generated script can fail to
        /// compile inside the game while it compiles perfectly here.
        /// </para>
        /// <para>
        /// The second one says the mod has to be present on both sides of a multiplayer game. A
        /// dimension is made of objects, blocks and systems that the server spawns and the client
        /// draws, so a player without the mod cannot join a server that has it. Left off, the game
        /// refuses the join with "BadProtocolVersion" and never names the mod, which is a long
        /// afternoon for whoever is trying to work out why their friend cannot connect.
        /// </para>
        /// <para>
        /// Both are set every generate rather than only when absent: they are ordinary tick boxes in
        /// the mod's own window, and a creator who turns one off gets a mod that does not load or
        /// cannot be joined, with nothing anywhere saying why. The report says what was changed.
        /// </para>
        /// </remarks>
        private static void EnsureTheModCanLoadAndBeJoined(string templatePath)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(templatePath);
            if (settings == null)
            {
                return;
            }

            bool changed = false;

            if (!settings.metadata.accessesExtraAssemblies)
            {
                settings.metadata.accessesExtraAssemblies = true;
                changed = true;
                Debug.Log(
                    "[ExpandNullforge] Turned on \"accesses extra assemblies\" for '" +
                    settings.metadata.name + "'. Without it the game cannot compile the generated " +
                    "script when the mod loads, even though it builds here.");
            }

            if (settings.metadata.requiredOn != ModMetadata.ModExistsOn.ClientAndServer)
            {
                settings.metadata.requiredOn = ModMetadata.ModExistsOn.ClientAndServer;
                changed = true;
                Debug.Log(
                    "[ExpandNullforge] Marked '" + settings.metadata.name + "' as needed on both " +
                    "the client and the server. A dimension is spawned by the server and drawn by " +
                    "the client, so a player without the mod cannot join a server that has it.");
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }

        private static void EnsureFrameworkModDependency(string templatePath)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(templatePath);
            if (settings == null)
            {
                return;
            }

            if (settings.metadata.dependencies == null)
            {
                settings.metadata.dependencies = new List<ModMetadata.Dependency>();
            }

            for (int i = 0; i < settings.metadata.dependencies.Count; i++)
            {
                ModMetadata.Dependency dependency = settings.metadata.dependencies[i];
                if (dependency.modName == FrameworkModName)
                {
                    if (!dependency.required)
                    {
                        dependency.required = true;
                        settings.metadata.dependencies[i] = dependency;
                        EditorUtility.SetDirty(settings);
                    }

                    return;
                }
            }

            settings.metadata.dependencies.Add(new ModMetadata.Dependency
            {
                modName = FrameworkModName,
                required = true
            });
            EditorUtility.SetDirty(settings);
        }

        private static bool EnsureJsonStringArrayContains(
            string absoluteJsonPath,
            string propertyName,
            IReadOnlyList<string> values)
        {
            if (string.IsNullOrEmpty(absoluteJsonPath) ||
                string.IsNullOrEmpty(propertyName) ||
                values == null ||
                values.Count == 0)
            {
                return false;
            }

            string text = File.ReadAllText(absoluteJsonPath);
            int nameIndex = text.IndexOf("\"" + propertyName + "\"", System.StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return false;
            }

            int bracketStart = text.IndexOf('[', nameIndex);
            if (bracketStart < 0)
            {
                return false;
            }

            int bracketEnd = FindMatchingArrayBracket(text, bracketStart);
            if (bracketEnd < 0)
            {
                return false;
            }

            string body = text.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            List<string> missing = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (!body.Contains(ToJsonString(value)))
                {
                    missing.Add(value);
                }
            }

            if (missing.Count == 0)
            {
                return false;
            }

            string itemIndent = ResolveArrayItemIndent(text, bracketStart);
            string closingIndent = ResolveClosingIndent(text, bracketEnd);
            StringBuilder insertion = new StringBuilder();
            string trimmedBody = body.Trim();
            if (trimmedBody.Length > 0)
            {
                int lastNonWhitespace = FindLastNonWhitespace(text, bracketEnd - 1, bracketStart + 1);
                if (lastNonWhitespace >= 0 && text[lastNonWhitespace] != ',')
                {
                    insertion.Append(",");
                }
            }

            for (int i = 0; i < missing.Count; i++)
            {
                insertion.AppendLine();
                insertion.Append(itemIndent);
                insertion.Append(ToJsonString(missing[i]));
                if (i < missing.Count - 1)
                {
                    insertion.Append(",");
                }
            }

            insertion.AppendLine();
            insertion.Append(closingIndent);

            string updated =
                text.Substring(0, bracketEnd) +
                insertion +
                text.Substring(bracketEnd);
            File.WriteAllText(absoluteJsonPath, updated, Encoding.UTF8);
            return true;
        }

        private static int FindMatchingArrayBracket(string text, int bracketStart)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = bracketStart; i < text.Length; i++)
            {
                char character = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '[')
                {
                    depth++;
                }
                else if (character == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string ResolveArrayItemIndent(string text, int bracketStart)
        {
            int lineStart = text.LastIndexOf('\n', bracketStart);
            string baseIndent = lineStart < 0
                ? string.Empty
                : ReadIndent(text, lineStart + 1);
            return baseIndent + "    ";
        }

        private static string ResolveClosingIndent(string text, int bracketEnd)
        {
            int lineStart = text.LastIndexOf('\n', bracketEnd);
            return lineStart < 0 ? string.Empty : ReadIndent(text, lineStart + 1);
        }

        private static string ReadIndent(string text, int start)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < text.Length; i++)
            {
                char character = text[i];
                if (character != ' ' && character != '\t')
                {
                    break;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private static int FindLastNonWhitespace(string text, int start, int min)
        {
            for (int i = start; i >= min; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
