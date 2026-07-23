using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Runtime Manifest")]
    public sealed class DimensionRuntimeManifestAsset : ScriptableObject
    {
        [SerializeField] private string manifestId = "mod:DimensionManifest";
        [SerializeField] private string displayName = "Dimension Runtime Manifest";
        [SerializeField] private DimensionTemplateAsset sourceTemplate;
        [SerializeField] private bool updateExisting = true;
        [SerializeField] private bool requireExistingOwnershipRecords;
        [SerializeField] private string generatedFromDimensionId = string.Empty;
        [SerializeField] private string generatedFromContentPackId = string.Empty;
        [SerializeField] private string lastBuildCode = string.Empty;
        [SerializeField] private string lastBuildMessage = string.Empty;
        [SerializeField] private int serializedManifestSchemaVersion;
        [SerializeField] private string serializedManifestJson = string.Empty;
        [SerializeField] private int dimensionCount;
        [SerializeField] private int biomeCount;
        [SerializeField] private int zoneCount;
        [SerializeField] private int portalCount;
        [SerializeField] private int sceneCount;
        [SerializeField] private int resourceNodeCount;
        [SerializeField] private int spawnRuleCount;
        [SerializeField] private int generationPassCount;
        [SerializeField] private int generationTableCount;
        [SerializeField] private int ownershipBindingCount;
        [SerializeField] private int assetReferenceCount;

        [Tooltip("Item ids whose prefabs were generated for this dimension. The runtime declares these so it can report any that never registered with the game.")]
        [SerializeField] private string[] generatedItemIds = new string[0];

        // Stored as a Newtonsoft.Json string of the flat DimensionTileMapSnapshot. Unity's
        // JsonUtility cannot be used here: in Core Keeper's recompiled-mod runtime it returns an
        // object with empty arrays whenever an element is a mod-defined type, so the palette and
        // layers come back empty. Newtonsoft reflects independently of Unity's serialization
        // backend, so arrays of mod types round-trip correctly at runtime.
        [Tooltip("The dimension's painted tile map, as a Newtonsoft-serialized snapshot. The runtime registers it so the tile-map generation provider can write it into the world.")]
        [SerializeField] private string tileMapJson = string.Empty;

        [System.NonSerialized] private DimensionTileMapModel cachedTileMap;
        [System.NonSerialized] private bool tileMapCacheValid;

        public string ManifestId
        {
            get { return manifestId ?? string.Empty; }
        }

        /// <summary>Dimension this manifest generates.</summary>
        public string GeneratedFromDimensionId
        {
            get { return generatedFromDimensionId ?? string.Empty; }
        }

        /// <summary>Content pack that owns this manifest.</summary>
        public string GeneratedFromContentPackId
        {
            get { return generatedFromContentPackId ?? string.Empty; }
        }

        /// <summary>
        /// The painted tile map for this dimension, or null if it has none. The runtime registers
        /// it into <c>DimensionTileMapRegistry</c> so the tile-map provider can generate it.
        /// </summary>
        public DimensionTileMapModel TileMap
        {
            get
            {
                if (!tileMapCacheValid)
                {
                    cachedTileMap = string.IsNullOrEmpty(tileMapJson)
                        ? null
                        : DimensionTileMapModel.FromSnapshot(
                            Newtonsoft.Json.JsonConvert.DeserializeObject<DimensionTileMapSnapshot>(tileMapJson));
                    tileMapCacheValid = true;
                }

                return cachedTileMap;
            }
        }

        /// <summary>True when a non-empty painted map is present.</summary>
        public bool HasTileMap
        {
            get
            {
                DimensionTileMapModel map = TileMap;
                return map != null && map.PaintedTileCount() > 0;
            }
        }

        /// <summary>Records the dimension's painted map. Called by the editor authoring flow.</summary>
        public void SetTileMap(DimensionTileMapModel map)
        {
            tileMapJson = map == null
                ? string.Empty
                : Newtonsoft.Json.JsonConvert.SerializeObject(map.ToSnapshot());
            cachedTileMap = map;
            tileMapCacheValid = true;
        }

        /// <summary>
        /// Item ids the generator produced prefabs for. The runtime declares these so a prefab
        /// that failed to register can be named instead of silently missing from the game.
        /// </summary>
        public string[] GeneratedItemIds
        {
            get { return generatedItemIds ?? new string[0]; }
        }

        /// <summary>
        /// Records which items were generated. Called by the editor generator; kept separate from
        /// <see cref="Configure"/> because generation and manifest export run independently.
        /// </summary>
        public void SetGeneratedItemIds(string[] itemIds)
        {
            generatedItemIds = itemIds ?? new string[0];
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionTemplateAsset SourceTemplate
        {
            get { return sourceTemplate; }
        }

        public bool UpdateExisting
        {
            get { return updateExisting; }
        }

        public bool RequireExistingOwnershipRecords
        {
            get { return requireExistingOwnershipRecords; }
        }

        public void Configure(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview,
            bool newUpdateExisting,
            bool newRequireExistingOwnershipRecords)
        {
            sourceTemplate = template;
            updateExisting = newUpdateExisting;
            requireExistingOwnershipRecords = newRequireExistingOwnershipRecords;

            string dimensionId = template == null ? string.Empty : template.DimensionId;
            string contentPackId = template == null ? string.Empty : template.ContentPackId;
            generatedFromDimensionId = dimensionId;
            generatedFromContentPackId = contentPackId;
            manifestId = string.IsNullOrEmpty(dimensionId)
                ? "dimension-manifest"
                : dimensionId + ".runtime-manifest";
            displayName = template == null || string.IsNullOrEmpty(template.DisplayName)
                ? "Dimension Runtime Manifest"
                : template.DisplayName + " Runtime Manifest";

            lastBuildCode = preview.Code;
            lastBuildMessage = preview.Message;
            serializedManifestSchemaVersion = 1;
            serializedManifestJson = UnityEngine.JsonUtility.ToJson(
                DimensionRuntimeManifestSnapshot.FromManifest(preview.Manifest));
            dimensionCount = preview.DimensionCount;
            biomeCount = preview.BiomeCount;
            zoneCount = preview.ZoneCount;
            portalCount = preview.Manifest.Portals == null ? 0 : preview.Manifest.Portals.Count;
            sceneCount = preview.SceneCount;
            resourceNodeCount = preview.ResourceNodeCount;
            spawnRuleCount = preview.SpawnRuleCount;
            generationPassCount = preview.GenerationPassCount;
            generationTableCount = preview.GenerationTableCount;
            ownershipBindingCount = preview.OwnershipBindingCount;
            assetReferenceCount = preview.AssetReferenceCount;
        }

        public bool TryBuildManifest(
            out DimensionContentManifest manifest,
            out DimensionCompiledGenerationPlan compiledPlan,
            out DimensionOperationResult result)
        {
            if (TryBuildSerializedManifest(out manifest, out result))
            {
                compiledPlan = default(DimensionCompiledGenerationPlan);
                return true;
            }

            if (serializedManifestSchemaVersion > 0 &&
                !string.IsNullOrEmpty(serializedManifestJson))
            {
                compiledPlan = default(DimensionCompiledGenerationPlan);
                return false;
            }

            return DimensionTemplateManifestBuilder.TryBuildManifest(
                sourceTemplate,
                out manifest,
                out compiledPlan,
                out result);
        }

        public bool TryBuildApplyRequest(
            string reason,
            out DimensionContentManifestRequest request,
            out DimensionOperationResult result)
        {
            DimensionContentManifest manifest;
            DimensionCompiledGenerationPlan ignoredPlan;
            if (!TryBuildManifest(out manifest, out ignoredPlan, out result))
            {
                request = default(DimensionContentManifestRequest);
                return false;
            }

            request = new DimensionContentManifestRequest(
                manifest,
                updateExisting,
                requireExistingOwnershipRecords,
                string.IsNullOrEmpty(reason) ? "Apply generated Dimensions API runtime manifest." : reason);
            return true;
        }

        public bool TryApplyTo(
            IDimensionContentManifestService manifestService,
            string reason,
            out DimensionContentManifestResult applyResult,
            out DimensionOperationResult buildResult)
        {
            if (manifestService == null)
            {
                buildResult = DimensionOperationResult.Failed(
                    "manifest-service-missing",
                    "A manifest service is required before a runtime manifest can be applied.");
                applyResult = default(DimensionContentManifestResult);
                return false;
            }

            DimensionContentManifestRequest request;
            if (!TryBuildApplyRequest(reason, out request, out buildResult))
            {
                applyResult = default(DimensionContentManifestResult);
                return false;
            }

            applyResult = manifestService.TryApplyContentManifest(request);
            return applyResult.Success;
        }

        private bool TryBuildSerializedManifest(
            out DimensionContentManifest manifest,
            out DimensionOperationResult result)
        {
            manifest = default(DimensionContentManifest);
            if (serializedManifestSchemaVersion <= 0 ||
                string.IsNullOrEmpty(serializedManifestJson))
            {
                result = DimensionOperationResult.Failed(
                    "runtime-manifest-snapshot-missing",
                    "The runtime manifest does not contain a serialized manifest snapshot.");
                return false;
            }

            try
            {
                DimensionRuntimeManifestSnapshot snapshot =
                    UnityEngine.JsonUtility.FromJson<DimensionRuntimeManifestSnapshot>(
                        serializedManifestJson);
                if (snapshot == null)
                {
                    result = DimensionOperationResult.Failed(
                        "runtime-manifest-snapshot-invalid",
                        "The serialized runtime manifest snapshot could not be read.");
                    return false;
                }

                manifest = snapshot.ToManifest();
                result = DimensionOperationResult.Ok();
                return true;
            }
            catch (System.Exception exception)
            {
                result = DimensionOperationResult.Failed(
                    "runtime-manifest-snapshot-invalid",
                    "The serialized runtime manifest snapshot is invalid: " +
                    exception.Message);
                return false;
            }
        }
    }
}
