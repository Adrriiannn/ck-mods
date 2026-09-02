using ExpandNullforge.Authoring;
using UnityEditor;

namespace ExpandNullforge.EditorTools
{
    internal static class DimensionScriptableDataContextUtility
    {
        private static int cachedTemplateInstanceId;
        private static ScriptableDataEditorUtility.Context cachedContext;
        private static bool hasCachedContext;

        static DimensionScriptableDataContextUtility()
        {
            EditorApplication.projectChanged += InvalidateCache;
        }

        public static bool TryScopeToTemplate(
            DimensionTemplateAsset template,
            out string error)
        {
            error = string.Empty;
            if (template == null)
            {
                error = "Select a Dimension Asset before creating a SpriteAsset override.";
                return false;
            }

            int templateInstanceId = template.GetInstanceID();
            if (hasCachedContext &&
                cachedTemplateInstanceId == templateInstanceId &&
                cachedContext.isValid)
            {
                if (cachedContext.isReadOnly)
                {
                    error =
                        "The Scriptable Data context for the selected dimension's mod is read-only. " +
                        "No SpriteAsset was created.";
                    return false;
                }

                if (ScriptableDataEditorUtility.currentContext != cachedContext)
                {
                    ScriptableDataEditorUtility.SetContext(cachedContext);
                }

                return true;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                error =
                    "Could not determine which PugMod owns the selected Dimension Asset at " +
                    templatePath +
                    ". No SpriteAsset was created.";
                return false;
            }

            string dataDirectory = NormalizeDirectory(modRoot + "/Data");
            ScriptableDataEditorUtility.Context context;
            if (!TryFindContext(dataDirectory, out context))
            {
                ScriptableDataEditorUtility.CacheContexts();
                TryFindContext(dataDirectory, out context);
            }

            if (context.isValid)
            {
                Cache(templateInstanceId, context);
                if (context.isReadOnly)
                {
                    error =
                        "The Scriptable Data context for the selected dimension's mod is read-only at " +
                        dataDirectory +
                        ". No SpriteAsset was created.";
                    return false;
                }

                EnsureOverloadingEnabled(context);

                if (ScriptableDataEditorUtility.currentContext != context)
                {
                    ScriptableDataEditorUtility.SetContext(context);
                }

                return true;
            }

            string contextName = GetLastPathSegment(modRoot);
            int contextIndex = ScriptableDataEditorUtility.AddContext(
                dataDirectory,
                contextName);
            if (contextIndex < 0)
            {
                error =
                    "Could not create the Scriptable Data context for the selected dimension's mod at " +
                    dataDirectory +
                    ". No SpriteAsset was created.";
                return false;
            }

            context = ScriptableDataEditorUtility.GetContext(contextIndex);
            Cache(templateInstanceId, context);
            EnsureOverloadingEnabled(context);

            ScriptableDataEditorUtility.SetContext(context);
            return true;
        }

        private static void EnsureOverloadingEnabled(
            ScriptableDataEditorUtility.Context context)
        {
            if (context.directoryInfo == null || context.directoryInfo.enableOverloading)
            {
                return;
            }

            context.directoryInfo.enableOverloading = true;
            EditorUtility.SetDirty(context.directoryInfo);
            AssetDatabase.SaveAssetIfDirty(context.directoryInfo);
        }

        private static void Cache(
            int templateInstanceId,
            ScriptableDataEditorUtility.Context context)
        {
            cachedTemplateInstanceId = templateInstanceId;
            cachedContext = context;
            hasCachedContext = context.isValid;
        }

        private static void InvalidateCache()
        {
            cachedTemplateInstanceId = 0;
            cachedContext = default(ScriptableDataEditorUtility.Context);
            hasCachedContext = false;
        }

        private static bool TryFindContext(
            string dataDirectory,
            out ScriptableDataEditorUtility.Context context)
        {
            for (int i = 0; i < ScriptableDataEditorUtility.contextsCount; i++)
            {
                ScriptableDataEditorUtility.Context candidate =
                    ScriptableDataEditorUtility.GetContext(i);
                if (!string.Equals(
                        NormalizeDirectory(candidate.directory),
                        dataDirectory,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                context = candidate;
                return true;
            }

            context = default(ScriptableDataEditorUtility.Context);
            return false;
        }

        private static string NormalizeDirectory(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetLastPathSegment(string path)
        {
            string normalized = NormalizeDirectory(path);
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }
    }
}
