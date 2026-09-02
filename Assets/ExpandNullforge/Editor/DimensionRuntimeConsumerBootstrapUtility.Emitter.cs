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
    /// Writing the bootstrap script a consumer mod runs at load.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static void EnsureGeneratedBootstrapScript(
            DimensionRuntimePortalOutput portalOutput,
            string modDisplayName,
            string scriptFolder,
            DimensionBounds tileMapBounds,
            DimensionTemplateAsset template)
        {
            string className =
                SanitizeIdentifier(modDisplayName, "DimensionMod") +
                SanitizeIdentifier(portalOutput.DimensionId, "Dimension") +
                "RuntimeBootstrap";
            string path = scriptFolder + "/" + className + ".cs";

            // Resolved the same way DimensionItemGenerator resolves it — settings.metadata.name, or
            // empty when there is no ModBuilderSettings. Using the display-name helper instead would
            // fall back to a FOLDER name where the generator falls back to empty, and the two would
            // then qualify object names differently: every recipe would point at a name that does
            // not exist.
            ModBuilderSettings modSettings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(scriptFolder);
            string modName = modSettings == null ? string.Empty : (modSettings.metadata.name ?? string.Empty);

            string content =
                BuildBootstrapScript(portalOutput, className, tileMapBounds, template, modName);
            WriteTextAssetIfChanged(path, content);
        }

        private static string BuildBootstrapScript(
            DimensionRuntimePortalOutput portalOutput,
            string className,
            DimensionBounds tileMapBounds,
            DimensionTemplateAsset template,
            string modName)
        {
            StringBuilder builder = new StringBuilder();
            AppendBootstrapHeader(builder, className);
            AppendBootstrapConstants(builder, portalOutput);
            AppendBootstrapFields(builder);
            AppendBootstrapLifecycle(builder);
            AppendBootstrapModObjectLoaded(builder, modName);
            AppendBootstrapUpdateLoop(builder);
            AppendBootstrapStaticExtras(builder, portalOutput, template, modName);
            AppendBootstrapMinimumDefinitions(builder);
            AppendBootstrapDefinitionFactories(builder, portalOutput, tileMapBounds, template);
            AppendBootstrapManifestApply(builder);
            AppendBootstrapPortalRegistration(builder, portalOutput);
            AppendBootstrapEnsureHelpers(builder);
            return builder.ToString();
        }
    }
}
