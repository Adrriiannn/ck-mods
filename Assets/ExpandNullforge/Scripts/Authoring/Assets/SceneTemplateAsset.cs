using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Scene Template")]
    public sealed class SceneTemplateAsset : ScriptableObject
    {
        [SerializeField] private string sceneId = "scene";
        [SerializeField] private string templateId = "scene-template";
        [SerializeField] private string displayName = "Scene";
        [SerializeField] private string kind = "scene";
        [SerializeField] private string providerId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionScenePlacementMode placementMode = DimensionScenePlacementMode.Automatic;
        [SerializeField] private Vector2Int footprintSize = new Vector2Int(16, 16);
        [SerializeField] private Vector2Int exactLocalPosition;
        [SerializeField] private Vector2Int preferredLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int preferredLocalMaxExclusive = new Vector2Int(64, 64);

        [Tooltip("Nearest this may appear to the dimension's centre, in tiles.")]
        [Min(0)]
        [SerializeField] private int minRadiusTiles;

        [Tooltip("Furthest out it may appear, in tiles.")]
        [Min(1)]
        [SerializeField] private int maxRadiusTiles = 256;

        [Tooltip("Only place it where this biome reaches. Empty means anywhere in the band.")]
        [SerializeField] private string radialBiomeId = string.Empty;
        [SerializeField] private int weight = 1;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool required;
        [SerializeField] private bool unique = true;

        [Tooltip("Let Core Keeper's own Overworld generation grow this scene naturally, the way " +
                 "vanilla ruins appear. Off means the scene only exists where this dimension " +
                 "places it.")]
        [SerializeField] private bool spawnInOverworld;

        [Tooltip("Vanilla biome names this may grow in (Forest, Stone, Nature, Sea, Desert, " +
                 "Crystal). Custom biomes cannot go here: the Overworld only ever samples " +
                 "vanilla biomes, so a custom name would be configuration that does nothing.")]
        [SerializeField] private string[] overworldBiomeNames = new string[0];

        [Tooltip("How many the Overworld may grow per world.")]
        [Min(1)]
        [SerializeField] private int overworldMaxOccurrences = 1;

        [Tooltip("Nearest to the Core it may grow, in tiles. Classic worlds only.")]
        [Min(0)]
        [SerializeField] private int minDistanceFromCore;
        [SerializeField] private DimensionSceneTriggerTemplate[] triggers =
            new DimensionSceneTriggerTemplate[0];
        [SerializeField] private DimensionSceneTileTemplate[] tiles =
            new DimensionSceneTileTemplate[0];
        [SerializeField] private DimensionSceneObjectTemplate[] sceneObjects =
            new DimensionSceneObjectTemplate[0];

        public string SceneId
        {
            get { return sceneId ?? string.Empty; }
        }

        public string TemplateId
        {
            get { return templateId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Kind
        {
            get { return kind ?? string.Empty; }
        }

        public string[] AllowedBiomeIds
        {
            get { return allowedBiomeIds ?? new string[0]; }
        }

        public DimensionScenePlacementMode PlacementMode
        {
            get { return placementMode; }
        }

        /// <summary>Nearest this scene may appear to the dimension's centre.</summary>
        public int MinRadiusTiles
        {
            get { return minRadiusTiles < 0 ? 0 : minRadiusTiles; }
        }

        /// <summary>
        /// Furthest out it may appear, never inside the minimum.
        /// </summary>
        /// <remarks>
        /// Clamped rather than validated because an inverted band produces no placement at all, and a
        /// landmark that silently never appears is the hardest kind of authoring mistake to notice.
        /// </remarks>
        public int MaxRadiusTiles
        {
            get { return maxRadiusTiles <= MinRadiusTiles ? MinRadiusTiles + 1 : maxRadiusTiles; }
        }

        /// <summary>The biome the band is restricted to, or empty for the whole ring.</summary>
        public string RadialBiomeId
        {
            get { return radialBiomeId ?? string.Empty; }
        }

        /// <summary>Whether this scene is placed by distance from the centre.</summary>
        public bool UsesRadialPlacement
        {
            get { return placementMode == DimensionScenePlacementMode.RadialBand; }
        }

        public Vector2Int FootprintSize
        {
            get { return footprintSize; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool Required
        {
            get { return required; }
        }

        public bool Unique
        {
            get { return unique; }
        }

        /// <summary>Relative frequency among repeatable scenes competing for the same ground.</summary>
        public int Weight
        {
            get { return weight < 1 ? 1 : weight; }
        }

        public Vector2Int ExactLocalPosition
        {
            get { return exactLocalPosition; }
        }

        /// <summary>Whether vanilla Overworld generation may grow this scene naturally.</summary>
        public bool SpawnInOverworld
        {
            get { return spawnInOverworld; }
        }

        /// <summary>Vanilla biome NAMES only — resolved against the game's Biome enum at build.</summary>
        public string[] OverworldBiomeNames
        {
            get { return overworldBiomeNames ?? new string[0]; }
        }

        public int OverworldMaxOccurrences
        {
            get { return overworldMaxOccurrences < 1 ? 1 : overworldMaxOccurrences; }
        }

        public int MinDistanceFromCore
        {
            get { return minDistanceFromCore < 0 ? 0 : minDistanceFromCore; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public DimensionSceneTriggerTemplate[] Triggers
        {
            get { return triggers ?? new DimensionSceneTriggerTemplate[0]; }
        }

        /// <summary>
        /// The terrain this scene stamps: the walls, floors and water that make it a place rather than
        /// a scattering of props.
        /// </summary>
        /// <remarks>
        /// Kept as its own list rather than folded into <see cref="SceneObjects"/> because the two
        /// travel together but mean different things: tiles are the terrain, objects the furniture.
        /// Both are handed to Core Keeper's scene table so the engine stamps them itself — which is
        /// what lets a scene be embedded in a generated dungeon room rather than only dropped at a
        /// coordinate we choose.
        /// </remarks>
        public DimensionSceneTileTemplate[] Tiles
        {
            get { return tiles ?? new DimensionSceneTileTemplate[0]; }
        }

        /// <summary>Whether this scene stamps terrain of its own.</summary>
        public bool HasTiles
        {
            get { return tiles != null && tiles.Length > 0; }
        }

        /// <summary>
        /// The chests, statues, torches and other objects this scene brings with it.
        /// </summary>
        /// <remarks>
        /// Travels with the tiles into Core Keeper's own scene table, rather than being placed by the
        /// framework afterwards — so a dungeon room built from this scene arrives complete, in one
        /// stamp, the way a hand-authored vanilla room does.
        /// </remarks>
        public DimensionSceneObjectTemplate[] SceneObjects
        {
            get { return sceneObjects ?? new DimensionSceneObjectTemplate[0]; }
        }

        /// <summary>Whether this scene places anything beyond terrain.</summary>
        public bool HasSceneObjects
        {
            get { return sceneObjects != null && sceneObjects.Length > 0; }
        }

        public void ConfigureIdentity(
            string newSceneId,
            string newTemplateId,
            string newDisplayName,
            string newKind,
            string newProviderId,
            int newPriority,
            bool newEnabled)
        {
            sceneId = newSceneId ?? string.Empty;
            templateId = newTemplateId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            kind = string.IsNullOrEmpty(newKind) ? "scene" : newKind;
            providerId = newProviderId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ConfigureSpawnPolicy(int newWeight, bool newRequired, bool newUnique)
        {
            weight = Mathf.Max(1, newWeight);
            required = newRequired;
            unique = newUnique;
        }

        public void SetAllowedBiomeIds(IReadOnlyList<string> biomeIds)
        {
            if (biomeIds == null || biomeIds.Count == 0)
            {
                allowedBiomeIds = new string[0];
                return;
            }

            List<string> values = new List<string>();
            for (int i = 0; i < biomeIds.Count; i++)
            {
                string biomeId = biomeIds[i];
                if (!string.IsNullOrEmpty(biomeId) && !ContainsString(values, biomeId))
                {
                    values.Add(biomeId);
                }
            }

            allowedBiomeIds = values.ToArray();
        }

        public void ApplyAutomaticPlacement(Vector2Int newFootprintSize)
        {
            placementMode = DimensionScenePlacementMode.Automatic;
            footprintSize = EnsurePositiveSize(newFootprintSize);
        }

        public DimensionBounds ExactLocalBounds
        {
            get
            {
                return BoundsFromPositionAndSize(exactLocalPosition, footprintSize);
            }
        }

        public DimensionBounds PreferredLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(preferredLocalMin.x, preferredLocalMin.y),
                    new int2(preferredLocalMaxExclusive.x, preferredLocalMaxExclusive.y));
            }
        }

        public DimensionSceneTemplateDefinition ToTemplateDefinition(string dimensionId, string zoneId)
        {
            return new DimensionSceneTemplateDefinition(
                templateId,
                displayName,
                dimensionId,
                zoneId,
                kind,
                providerId,
                new int2(footprintSize.x, footprintSize.y),
                weight,
                priority,
                enabled);
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                sceneId,
                "scene",
                displayName,
                DimensionAssetReferenceKind.Scene,
                string.IsNullOrEmpty(providerId) ? templateId : providerId,
                kind,
                priority,
                enabled,
                "Scene template source.");
        }

        private static DimensionBounds BoundsFromPositionAndSize(Vector2Int position, Vector2Int size)
        {
            return new DimensionBounds(
                new int2(position.x, position.y),
                new int2(position.x + size.x, position.y + size.y));
        }

        private static Vector2Int EnsurePositiveSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        private static bool ContainsString(List<string> values, string value)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
