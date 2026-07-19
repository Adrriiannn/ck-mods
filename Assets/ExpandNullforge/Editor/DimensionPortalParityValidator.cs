using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Editor-side parity trace for a dimension's portal artwork. Walks each visual layer
    /// from the profile reference to its resolved SpriteAsset, its animation-zero color
    /// texture, its SpriteAssetManifest membership, and its ownership (consumer package vs.
    /// shared framework starter). Its purpose is to catch the framework's headline risk —
    /// a customized Studio preview that resolves to a stale, missing, or unpackaged asset at
    /// build/runtime — before a mod is built. It is deliberately read-only.
    /// </summary>
    internal static class DimensionPortalParityValidator
    {
        internal enum Severity
        {
            Ok,
            Warning,
            Error
        }

        internal struct Finding
        {
            public string Layer;
            public Severity Severity;
            public string Message;
        }

        private struct LayerRef
        {
            public string Layer;
            public string Property;
            public bool IsSwirl;
        }

        private static readonly LayerRef[] Layers =
        {
            new LayerRef { Layer = "Frame", Property = "portalFrameSpriteAsset" },
            new LayerRef { Layer = "Charge", Property = "chargeWaveSpriteAsset" },
            new LayerRef { Layer = "Milestones", Property = "milestoneSpriteAsset" },
            new LayerRef { Layer = "Center", Property = "centerEffectSpriteAsset" },
            new LayerRef { Layer = "Swirls", Property = "centerSwirlSpriteAsset", IsSwirl = true }
        };

        [MenuItem("Dimensions API/Validate Selected Portal Assets")]
        private static void ValidateSelectionMenu()
        {
            DimensionTemplateAsset template = Selection.activeObject as DimensionTemplateAsset;
            if (template == null)
            {
                Debug.LogWarning(
                    "Dimensions API parity: select a Dimension Asset (DimensionTemplateAsset) " +
                    "in the Project window, then run this again.");
                return;
            }

            DimensionPortalVisualProfileAsset profile = template.PortalVisualProfile;
            if (profile == null)
            {
                Debug.LogWarning(
                    "Dimensions API parity: '" + template.name +
                    "' has no portal visual profile assigned.", template);
                return;
            }

            Validate(template, profile, out string report);
            Debug.Log(report, template);
        }

        /// <summary>
        /// Produces a per-layer parity report for the portal profile and returns the raw
        /// findings. <paramref name="report"/> is a human-readable summary.
        /// </summary>
        internal static List<Finding> Validate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string report)
        {
            List<Finding> findings = new List<Finding>();
            if (profile == null)
            {
                findings.Add(new Finding
                {
                    Layer = "Profile",
                    Severity = Severity.Error,
                    Message = "No portal visual profile was supplied."
                });
                report = Format(template, findings);
                return findings;
            }

            string packageFolder = string.Empty;
            if (template != null)
            {
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out _,
                    out packageFolder);
            }

            SpriteAssetManifest manifest = ResolveManifest(template);
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();

            for (int i = 0; i < Layers.Length; i++)
            {
                ValidateLayer(
                    Layers[i],
                    profile,
                    serialized,
                    packageFolder,
                    manifest,
                    findings);
            }

            report = Format(template, findings);
            return findings;
        }

        private static void ValidateLayer(
            LayerRef layer,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serialized,
            string packageFolder,
            SpriteAssetManifest manifest,
            List<Finding> findings)
        {
            SerializedProperty reference = serialized.FindProperty(layer.Property);
            if (reference == null)
            {
                findings.Add(Make(layer.Layer, Severity.Warning,
                    "The profile no longer exposes '" + layer.Property + "'."));
                return;
            }

            SpriteAsset asset;
            string kind;
            bool resolved;
            if (layer.IsSwirl)
            {
                if (!profile.CenterSwirlOverrideVanilla)
                {
                    findings.Add(Make(layer.Layer, Severity.Ok,
                        "Vanilla inner particles (no custom SpriteAsset)."));
                    return;
                }

                resolved = DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    profile,
                    reference,
                    packageFolder,
                    out asset);
                kind = asset != null &&
                       DimensionPortalSwirlArtworkEditorUtility.IsFrameworkAsset(asset)
                    ? "Framework"
                    : "Managed";
                if (!resolved)
                {
                    findings.Add(Make(layer.Layer, Severity.Error,
                        "Override is enabled but the custom swirl SpriteAsset could not be " +
                        "resolved in Scriptable Data (build would fall back or fail)."));
                    return;
                }
            }
            else
            {
                DimensionPortalArtworkLayer artworkLayer = ToArtworkLayer(layer.Layer);
                DimensionPortalArtworkReferenceKind referenceKind =
                    DimensionPortalArtworkEditorUtility.ClassifyReference(
                        reference,
                        profile,
                        artworkLayer,
                        out asset);
                kind = referenceKind.ToString();
                switch (referenceKind)
                {
                    case DimensionPortalArtworkReferenceKind.Empty:
                        findings.Add(Make(layer.Layer, Severity.Ok,
                            "Uses the exact vanilla artwork (no override)."));
                        return;
                    case DimensionPortalArtworkReferenceKind.Unresolved:
                        findings.Add(Make(layer.Layer, Severity.Error,
                            "The referenced SpriteAsset address could not be resolved in " +
                            "Scriptable Data — the built portal would be missing this layer."));
                        return;
                }

                resolved = asset != null;
            }

            if (asset == null)
            {
                findings.Add(Make(layer.Layer, Severity.Error,
                    "Reference classified as '" + kind + "' but no SpriteAsset resolved."));
                return;
            }

            string assetPath = NormalizePath(AssetDatabase.GetAssetPath(asset));

            // Ownership: a customized layer should be consumer-owned, not a mutable framework
            // asset. A Framework/Empty kind is fine (intentional read-only reference); a
            // Managed/External kind that lives outside the profile package is a red flag.
            if ((kind == "Managed" || kind == "External") &&
                !string.IsNullOrEmpty(packageFolder) &&
                !assetPath.StartsWith(NormalizePath(packageFolder) + "/",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Make(layer.Layer, Severity.Warning,
                    "Customized artwork resolves outside this portal's package (" + assetPath +
                    "). It may be lost or shared across profiles."));
            }

            // Manifest membership: a consumer SpriteAsset must be listed in the mod's
            // SpriteAssetManifest or it will not load in the built mod.
            bool consumerOwned = kind == "Managed" || kind == "External";
            if (consumerOwned && manifest != null &&
                (manifest.spriteAssets == null || !manifest.spriteAssets.Contains(asset)))
            {
                findings.Add(Make(layer.Layer, Severity.Error,
                    "The consumer SpriteAsset '" + asset.name +
                    "' is not registered in the mod's SpriteAssetManifest and will not load " +
                    "in the built portal."));
                return;
            }

            // Animation-zero color texture presence.
            Texture2D color = ResolveAnimationZeroColor(asset);
            if (color == null)
            {
                findings.Add(Make(layer.Layer, Severity.Error,
                    "The resolved SpriteAsset '" + asset.name +
                    "' has no animation-zero color texture."));
                return;
            }

            findings.Add(Make(layer.Layer, Severity.Ok,
                kind + " -> " + asset.name + " (" + color.width + "x" + color.height + ")"));
        }

        private static SpriteAssetManifest ResolveManifest(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return null;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                NormalizePath(modRoot) + "/SpriteAssetManifest.asset");
        }

        private static Texture2D ResolveAnimationZeroColor(SpriteAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            FrameAnimation animation = asset.GetAnimationAt(0);
            if (animation != null && animation.spriteData != null &&
                animation.spriteData.texture != null)
            {
                return animation.spriteData.texture;
            }

            return null;
        }

        private static DimensionPortalArtworkLayer ToArtworkLayer(string layer)
        {
            switch (layer)
            {
                case "Charge":
                    return DimensionPortalArtworkLayer.ChargeSweep;
                case "Milestones":
                    return DimensionPortalArtworkLayer.Milestones;
                case "Center":
                    return DimensionPortalArtworkLayer.Center;
                default:
                    return DimensionPortalArtworkLayer.Frame;
            }
        }

        private static Finding Make(string layer, Severity severity, string message)
        {
            return new Finding { Layer = layer, Severity = severity, Message = message };
        }

        private static string Format(DimensionTemplateAsset template, List<Finding> findings)
        {
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity == Severity.Error)
                {
                    errors++;
                }
                else if (findings[i].Severity == Severity.Warning)
                {
                    warnings++;
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("Dimensions API portal parity — ");
            builder.Append(template == null ? "portal" : template.name);
            builder.Append(": ");
            builder.Append(errors == 0 && warnings == 0 ? "all layers OK" :
                errors + " error(s), " + warnings + " warning(s)");
            for (int i = 0; i < findings.Count; i++)
            {
                builder.Append('\n');
                builder.Append(findings[i].Severity == Severity.Error ? "  [ERROR]   " :
                    findings[i].Severity == Severity.Warning ? "  [WARNING] " : "  [ok]      ");
                builder.Append(findings[i].Layer);
                builder.Append(": ");
                builder.Append(findings[i].Message);
            }

            return builder.ToString();
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
