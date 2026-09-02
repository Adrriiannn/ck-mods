using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How a critter decides where it belongs.</summary>
    /// <remarks>
    /// Core Keeper offers exactly two, and they are exclusive: a critter reads either the biome it
    /// is in or the ground it is standing on, never both. The converter proves it — it writes the
    /// biome list only when the type is Biome and the tileset list only when it is Tileset.
    /// </remarks>
    public enum DimensionCritterHome
    {
        /// <summary>It belongs to biomes. What most critters do.</summary>
        ByBiome = 0,

        /// <summary>It belongs to particular ground, whatever biome that ground is in.</summary>
        ByGround = 1
    }

    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Critter")]
    public sealed class DimensionCritterAsset : ScriptableObject, IDimensionCreatureAsset
    {
        [SerializeField] private string critterId = "mod:critter";

        [Tooltip("What this critter is called once it is in a slot. A critter catcher puts the critter itself into a chest, so this is the name a player reads there.")]
        [SerializeField] private string displayName = "Critter";

        [Tooltip("The line under the name in that tooltip. The game's own critters do write one.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();

        [Tooltip("The small things it simply is, or simply does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();


        [Header("How it lives")]
        [Tooltip("It flies rather than walks, so terrain does not stop it.")]
        [SerializeField] private bool isFlying;

        [Tooltip("New ones keep appearing rather than the world holding a fixed set.")]
        [SerializeField] private bool spawnContinuously = true;

        [Tooltip("It survives being left behind rather than being cleaned up when nobody is near.")]
        [SerializeField] private bool isPersistent;

        [Tooltip("More of them may gather in one place than usual.")]
        [SerializeField] private bool allowLargerAmount;

        [Tooltip("It can be caught with a critter catcher.")]
        [SerializeField] private bool canBeCaught;

        [Header("Where it lives")]
        [Tooltip("Whether it picks its home by biome or by the ground it stands on.")]
        [SerializeField] private DimensionCritterHome home = DimensionCritterHome.ByBiome;

        [Tooltip("The tilesets it appears on. Only used when it picks its home by ground.")]
        [SerializeField] private string[] tilesetIds = new string[0];
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string CritterId { get { return critterId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string Description { get { return description ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string[] AllowedBiomeIds { get { return allowedBiomeIds ?? new string[0]; } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }


        public bool IsFlying { get { return isFlying; } }
        public bool SpawnContinuously { get { return spawnContinuously; } }
        public bool IsPersistent { get { return isPersistent; } }
        public bool AllowLargerAmount { get { return allowLargerAmount; } }
        public bool CanBeCaught { get { return canBeCaught; } }
        public DimensionCritterHome Home { get { return home; } }

        /// <summary>The tilesets it lives on, or none when it picks its home by biome.</summary>
        public string[] TilesetIds
        {
            get
            {
                return home == DimensionCritterHome.ByGround
                    ? (tilesetIds ?? new string[0])
                    : new string[0];
            }
        }

        /// <summary>Whether it lives by ground and names no ground to live on.</summary>
        /// <remarks>
        /// The converter falls back to an empty list when this happens, which the game reads as
        /// "nowhere" — so the critter is authored, generated, and never appears anywhere.
        /// </remarks>
        public bool LivesNowhere
        {
            get
            {
                if (home == DimensionCritterHome.ByGround)
                {
                    return TilesetIds.Length == 0;
                }

                return AllowedBiomeIds.Length == 0;
            }
        }

        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionSpawnableAssetReferenceUtility.AddSpawnableAssetReferences(
                contentPackId,
                dimensionId,
                zoneId,
                CritterId,
                DisplayName,
                ObjectId,
                Enabled,
                Audio,
                references);
        }
    }
}
