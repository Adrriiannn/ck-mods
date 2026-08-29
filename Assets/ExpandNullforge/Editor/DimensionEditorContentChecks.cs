using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The editor-only half of content validation: checks that need AssetDatabase or prefab
    /// files, injected into <see cref="DimensionContentValidationUtility"/> through its
    /// delegate seam so the runtime assembly never references UnityEditor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is also where the validation cache learns about change: any object edit or asset
    /// import bumps the stamp, so within one workspace rebuild the full validation computes
    /// once and every later preview build in the same rebuild hits the memo.
    /// </para>
    /// <para>
    /// TIMING: everything here reads through AssetDatabase, which returns stale results
    /// inside a <c>StartAssetEditing</c> batch. Every current caller reaches this from a
    /// user action or a <c>delayCall</c>, never from inside a generator batch — keep it
    /// that way. The post-Build workspace rebuild re-validates after the batch closes,
    /// which is the correct window for prefab existence to be seen.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class DimensionEditorContentChecks
    {
        static DimensionEditorContentChecks()
        {
            DimensionContentValidationUtility.EditorChecks = Run;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            DimensionContentValidationUtility.BumpChangeStamp();
        }

        private static List<DimensionAuthoringIssue> Run(DimensionTemplateAsset template)
        {
            List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
            AddPortalParity(template, template.PortalVisualProfile, "placed", issues);
            AddPortalParity(template, template.ItemPortalVisualProfile, "instant", issues);
            AddGeneratedPrefabIssues(template, issues);
            return issues;
        }

        private static void AddPortalParity(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            string which,
            List<DimensionAuthoringIssue> issues)
        {
            if (profile == null)
            {
                // No profile means the vanilla portal's own art — nothing to break.
                return;
            }

            List<DimensionPortalParityValidator.Finding> findings =
                DimensionPortalParityValidator.Validate(template, profile, out _);
            for (int i = 0; i < findings.Count; i++)
            {
                DimensionPortalParityValidator.Finding finding = findings[i];
                if (finding.Severity == DimensionPortalParityValidator.Severity.Ok)
                {
                    continue;
                }

                issues.Add(new DimensionAuthoringIssue(
                    finding.Severity == DimensionPortalParityValidator.Severity.Error
                        ? DimensionAuthoringSeverity.Error
                        : DimensionAuthoringSeverity.Warning,
                    "portal-art-" + which + "-" +
                    (string.IsNullOrEmpty(finding.Layer)
                        ? "profile"
                        : finding.Layer.ToLowerInvariant()) + "-invalid",
                    finding.Message,
                    "Portal",
                    template.DimensionId,
                    false,
                    default));
            }
        }

        /// <summary>
        /// Whether each enabled item's generated prefab exists yet, and whether the item
        /// changed after it was written.
        /// </summary>
        /// <remarks>
        /// Info by design, both of them: Build the Dimension runs the whole content pipeline
        /// before the manifest on every press, so a missing or stale prefab is self-healing
        /// and must never gate the very button that fixes it. The file-time comparison
        /// over-triggers on a no-op re-save, which is why the message says "regenerates",
        /// not "broken" — an honest staleness proof needs a persisted generation manifest,
        /// and faking one with file times at Warning severity would be worse than none.
        /// </remarks>
        private static void AddGeneratedPrefabIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            string templatePath = AssetDatabase.GetAssetPath(template);
            string modRoot =
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                string prefabPath = modRoot + "/Items/" +
                    DimensionGeneratedPrefabUtility.SanitizeFileName(item.ItemId) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    issues.Add(new DimensionAuthoringIssue(
                        DimensionAuthoringSeverity.Info,
                        "generated-prefab-missing",
                        "'" + item.DisplayName +
                        "' has no generated prefab yet. Build the Dimension writes it.",
                        "Item",
                        item.ItemId,
                        false,
                        default));
                }
                else
                {
                    string itemPath = AssetDatabase.GetAssetPath(item);
                    if (!string.IsNullOrEmpty(itemPath) &&
                        System.IO.File.GetLastWriteTimeUtc(itemPath) >
                        System.IO.File.GetLastWriteTimeUtc(prefabPath))
                    {
                        issues.Add(new DimensionAuthoringIssue(
                            DimensionAuthoringSeverity.Info,
                            "generated-prefab-stale",
                            "'" + item.DisplayName +
                            "' changed after its prefab was generated. Build regenerates it.",
                            "Item",
                            item.ItemId,
                            false,
                            default));
                    }
                }
            }
        }
    }

    /// <summary>Flushes the validation memo when assets appear or change on disk.</summary>
    internal sealed class DimensionContentValidationImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // A generated prefab landing on disk must be seen by the currency check on the
            // very next validation, or Review would keep reporting it missing until an
            // unrelated edit happened to bump the stamp.
            DimensionContentValidationUtility.BumpChangeStamp();
        }
    }
}
