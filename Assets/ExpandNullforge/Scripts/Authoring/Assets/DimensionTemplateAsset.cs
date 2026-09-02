using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Dimension Asset")]
    public sealed class DimensionTemplateAsset : ScriptableObject
    {
        [SerializeField] private string dimensionId = "mod.dimension";
        [SerializeField] private string displayName = "Custom Dimension";
        [SerializeField] private string description = string.Empty;
        [SerializeField] private string contentPackId = string.Empty;
        [SerializeField] private string contentPackDisplayName = string.Empty;
        [SerializeField] private string contentPackVersion = "1.0.0";
        [SerializeField] private string contentPackAuthor = string.Empty;
        [SerializeField] private int minimumApiVersion = 1;
        [SerializeField] private string[] dependencyContentPackIds = new string[0];
        // Clears the protected ±5000 overworld band by the dimension's half-extent; an origin at
        // exactly 5000 straddles it and dimension registration is rejected.
        [SerializeField] private Vector2Int absoluteOrigin = new Vector2Int(0, 7000);
        [SerializeField] private Vector2Int reservedLocalMin = new Vector2Int(-4096, -4096);
        [SerializeField] private Vector2Int reservedLocalMaxExclusive = new Vector2Int(4096, 4096);
        [SerializeField] private int generationVersion = 1;
        [UnityEngine.Serialization.FormerlySerializedAs("spaceKind")]
        [SerializeField] private DimensionType dimensionType = DimensionType.World;
        [SerializeField] private DimensionCapabilityFlags capabilities =
            DimensionCapabilityFlags.LocalCoordinates |
            DimensionCapabilityFlags.AbsoluteCoordinates |
            DimensionCapabilityFlags.PlayerContext |
            DimensionCapabilityFlags.PlayerTravel |
            DimensionCapabilityFlags.Portals |
            DimensionCapabilityFlags.Map |
            DimensionCapabilityFlags.Minimap |
            DimensionCapabilityFlags.Generation |
            DimensionCapabilityFlags.AreaLoading |
            DimensionCapabilityFlags.SimulationLoading |
            DimensionCapabilityFlags.Persistence |
            DimensionCapabilityFlags.Multiplayer |
            DimensionCapabilityFlags.CoordinateInterop;
        [SerializeField] private DimensionLayoutTemplateAsset layoutTemplate;
        [SerializeField] private BiomeTemplateAsset[] biomes = new BiomeTemplateAsset[0];
        [SerializeField] private SceneTemplateAsset[] globalScenes = new SceneTemplateAsset[0];
        [SerializeField] private DimensionItemAsset[] globalItems = new DimensionItemAsset[0];
        [SerializeField] private DimensionTilesetAsset[] tilesets = new DimensionTilesetAsset[0];
        [SerializeField] private DimensionRecipeAsset[] globalRecipes = new DimensionRecipeAsset[0];
        [SerializeField] private DimensionWorkbenchAsset[] globalWorkbenches = new DimensionWorkbenchAsset[0];
        [SerializeField] private DimensionLootTableAsset[] globalLootTables = new DimensionLootTableAsset[0];
        [SerializeField] private DimensionDungeonAsset[] globalDungeons = new DimensionDungeonAsset[0];
        [SerializeField] private DimensionContainerAsset[] globalContainers = new DimensionContainerAsset[0];
        [SerializeField] private DimensionPlantAsset[] globalPlants = new DimensionPlantAsset[0];
        [SerializeField] private DimensionDishAsset[] globalDishes = new DimensionDishAsset[0];
        [SerializeField] private DimensionWorldObjectAsset[] globalWorldObjects = new DimensionWorldObjectAsset[0];
        [SerializeField] private DimensionVehicleAsset[] globalVehicles = new DimensionVehicleAsset[0];
        [SerializeField] private DimensionProjectileAsset[] globalProjectiles = new DimensionProjectileAsset[0];
        [SerializeField] private DimensionExplosionAsset[] globalExplosions = new DimensionExplosionAsset[0];
        [SerializeField] private DimensionAnimalAsset[] globalAnimals = new DimensionAnimalAsset[0];
        [SerializeField] private DimensionCritterAsset[] globalCritters = new DimensionCritterAsset[0];

        [Tooltip("The pieces of the game itself this mod replaces, rather than adds to.")]
        [SerializeField] private DimensionNamedAreaAsset[] namedAreas =
            new DimensionNamedAreaAsset[0];
        [SerializeField] private DimensionGameSetupAsset[] globalGameSetups =
            new DimensionGameSetupAsset[0];

        [Tooltip("Stat effects this mod invented — buffs, curses, poisonings the game did not have.")]
        [SerializeField] private DimensionConditionAsset[] globalConditions =
            new DimensionConditionAsset[0];
        [SerializeField] private DimensionMobAsset[] globalMobs = new DimensionMobAsset[0];
        [SerializeField] private DimensionBossAsset[] globalBosses = new DimensionBossAsset[0];
        [SerializeField] private GenerationPassTemplateAsset[] globalGenerationPasses = new GenerationPassTemplateAsset[0];
        [SerializeField] private DimensionPortalAccessRuleAsset[] portalAccessRules = new DimensionPortalAccessRuleAsset[0];
        [SerializeField] private float portalActivationChargeSeconds = 30.0f;
        [SerializeField] private DimensionPortalVisualProfileAsset portalVisualProfile;
        [SerializeField] private DimensionPortalVisualProfileAsset itemPortalVisualProfile;

        // Portal sounds. Keys are either a game SfxID name or a Sound Library (Addressables)
        // clip key. The placed portal only ever plays an activation sound (it never despawns
        // and a permanent loop would wear players down); the instant portal chooses ONE mode:
        // Peak (activation + deactivation one-shots) or Loop (a bed while it stands open).
        [SerializeField] private string placedPortalActivationSound = string.Empty;
        [SerializeField] private int instantPortalSoundMode; // 0 = Peak, 1 = Loop
        [SerializeField] private string instantPortalActivationSound = "AF_portal_appear";
        [SerializeField] private string instantPortalDeactivationSound = "AF_portal_collapse";
        [SerializeField] private string instantPortalLoopSound = string.Empty;

        [Tooltip("The music that plays while a player is inside this dimension. One of the game's " +
                 "own music names — MOLD_DUNGEON, MYSTERY, HOME_BASE and the rest — or a name of " +
                 "your own with your tracks listed below. Leave it empty to let the ground decide, " +
                 "which is what the game does everywhere else.")]
        [SerializeField] private string music = string.Empty;

        [Tooltip("Your own tracks, when the music above is a name of your own rather than one of " +
                 "the game's. Clip keys, the way sounds are named.")]
        [SerializeField] private string[] musicTracks = new string[0];

        public string DimensionId
        {
            get { return dimensionId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public string ContentPackId
        {
            get { return contentPackId ?? string.Empty; }
        }

        public string ContentPackDisplayName
        {
            get { return string.IsNullOrEmpty(contentPackDisplayName) ? DisplayName : contentPackDisplayName; }
        }

        public string ContentPackVersion
        {
            get { return contentPackVersion ?? string.Empty; }
        }

        public string ContentPackAuthor
        {
            get { return contentPackAuthor ?? string.Empty; }
        }

        public int MinimumApiVersion
        {
            get { return minimumApiVersion; }
        }

        public string[] DependencyContentPackIds
        {
            get { return dependencyContentPackIds ?? new string[0]; }
        }

        public DimensionLayoutTemplateAsset LayoutTemplate
        {
            get { return layoutTemplate; }
        }

        public BiomeTemplateAsset[] Biomes
        {
            get { return biomes ?? new BiomeTemplateAsset[0]; }
        }

        public SceneTemplateAsset[] GlobalScenes
        {
            get { return globalScenes ?? new SceneTemplateAsset[0]; }
        }

        public DimensionItemAsset[] GlobalItems
        {
            get { return globalItems ?? new DimensionItemAsset[0]; }
        }

        public DimensionTilesetAsset[] Tilesets
        {
            get { return tilesets ?? new DimensionTilesetAsset[0]; }
        }

        public DimensionRecipeAsset[] GlobalRecipes
        {
            get { return globalRecipes ?? new DimensionRecipeAsset[0]; }
        }

        public DimensionWorkbenchAsset[] GlobalWorkbenches
        {
            get { return globalWorkbenches ?? new DimensionWorkbenchAsset[0]; }
        }

        public DimensionLootTableAsset[] GlobalLootTables
        {
            get { return globalLootTables ?? new DimensionLootTableAsset[0]; }
        }

        /// <summary>The dungeons this dimension's world generation may grow.</summary>
        public DimensionDungeonAsset[] GlobalDungeons
        {
            get { return globalDungeons ?? new DimensionDungeonAsset[0]; }
        }

        /// <summary>The containers this dimension defines — chests, stashes, display stands.</summary>
        public DimensionContainerAsset[] GlobalContainers
        {
            get { return globalContainers ?? new DimensionContainerAsset[0]; }
        }

        /// <summary>The crops this dimension defines. Each one emits a seed, a plant and a ripe plant.</summary>
        public DimensionPlantAsset[] GlobalPlants
        {
            get { return globalPlants ?? new DimensionPlantAsset[0]; }
        }

        /// <summary>
        /// The kinds of dish this dimension adds to the cooking pot.
        /// </summary>
        /// <remarks>
        /// One entry per family, not per pair and not per quality. The pot has no recipe list: every
        /// pair of ingredients is a recipe, and the dish that comes out is whichever family the
        /// leading ingredient names. Each family here becomes three items — ordinary, rare and epic.
        /// </remarks>
        public DimensionDishAsset[] GlobalDishes
        {
            get { return globalDishes ?? new DimensionDishAsset[0]; }
        }

        /// <summary>The placed objects that are not containers, stations, plants or creatures.</summary>
        public DimensionWorldObjectAsset[] GlobalWorldObjects
        {
            get { return globalWorldObjects ?? new DimensionWorldObjectAsset[0]; }
        }

        /// <summary>The things this dimension lets a player ride.</summary>
        public DimensionVehicleAsset[] GlobalVehicles
        {
            get { return globalVehicles ?? new DimensionVehicleAsset[0]; }
        }

        /// <summary>What this dimension's weapons and creatures fire.</summary>
        public DimensionProjectileAsset[] GlobalProjectiles
        {
            get { return globalProjectiles ?? new DimensionProjectileAsset[0]; }
        }

        /// <summary>The blasts this dimension's bombs turn into.</summary>
        public DimensionExplosionAsset[] GlobalExplosions
        {
            get { return globalExplosions ?? new DimensionExplosionAsset[0]; }
        }

        public DimensionAnimalAsset[] GlobalAnimals
        {
            get { return globalAnimals ?? new DimensionAnimalAsset[0]; }
        }

        public DimensionCritterAsset[] GlobalCritters
        {
            get { return globalCritters ?? new DimensionCritterAsset[0]; }
        }

        /// <summary>The pieces of the game itself this mod replaces, rather than adds to.</summary>
        public DimensionNamedAreaAsset[] NamedAreas
        {
            get { return namedAreas ?? new DimensionNamedAreaAsset[0]; }
        }

        public DimensionGameSetupAsset[] GlobalGameSetups
        {
            get { return globalGameSetups ?? new DimensionGameSetupAsset[0]; }
        }

        /// <summary>Stat effects this mod invented.</summary>
        public DimensionConditionAsset[] GlobalConditions
        {
            get { return globalConditions ?? new DimensionConditionAsset[0]; }
        }

        public DimensionMobAsset[] GlobalMobs
        {
            get { return globalMobs ?? new DimensionMobAsset[0]; }
        }

        public DimensionBossAsset[] GlobalBosses
        {
            get { return globalBosses ?? new DimensionBossAsset[0]; }
        }

        public GenerationPassTemplateAsset[] GlobalGenerationPasses
        {
            get { return globalGenerationPasses ?? new GenerationPassTemplateAsset[0]; }
        }

        public DimensionPortalAccessRuleAsset[] PortalAccessRules
        {
            get { return portalAccessRules ?? new DimensionPortalAccessRuleAsset[0]; }
        }

        public float PortalActivationChargeSeconds
        {
            get { return Mathf.Max(0.0f, portalActivationChargeSeconds); }
        }

        public DimensionPortalVisualProfileAsset PortalVisualProfile
        {
            get { return portalVisualProfile; }
        }

        /// <summary>
        /// Optional dedicated look for the instant item portal (V2). When unassigned, the
        /// generator falls back to <see cref="PortalVisualProfile"/> with the frameless
        /// layers forced off.
        /// </summary>
        public DimensionPortalVisualProfileAsset ItemPortalVisualProfile
        {
            get { return itemPortalVisualProfile; }
        }

        public string PlacedPortalActivationSound
        {
            get { return placedPortalActivationSound ?? string.Empty; }
        }

        /// <summary>0 = Peak (activation/deactivation one-shots), 1 = Loop (open-portal bed).</summary>
        public int InstantPortalSoundMode
        {
            get { return Mathf.Clamp(instantPortalSoundMode, 0, 1); }
        }

        public string InstantPortalActivationSound
        {
            get { return instantPortalActivationSound ?? string.Empty; }
        }

        public string InstantPortalDeactivationSound
        {
            get { return instantPortalDeactivationSound ?? string.Empty; }
        }

        public string InstantPortalLoopSound
        {
            get { return instantPortalLoopSound ?? string.Empty; }
        }

        /// <summary>
        /// What this dimension sounds like, or empty to leave the music to the ground underfoot.
        /// </summary>
        /// <remarks>
        /// Either one of the game's own <c>MusicRosterType</c> names or a name of the author's own
        /// backed by <see cref="MusicTracks"/>. Both are answered the same way at load, because the
        /// framework's own rosters are appended to the game's list and are picked by exactly the
        /// same lookup — so an author never has to know which kind they typed.
        /// </remarks>
        public string Music
        {
            get { return music ?? string.Empty; }
        }

        /// <summary>The author's own tracks, when <see cref="Music"/> names a roster of their own.</summary>
        public string[] MusicTracks
        {
            get { return musicTracks ?? new string[0]; }
        }

        /// <summary>Whether the music name is one this mod has to supply the tracks for.</summary>
        /// <remarks>
        /// The generator answers this by asking whether the name parses as one of the game's
        /// rosters, which it cannot do from here — the enum lives in the game assembly and this
        /// asset is read in contexts that have no game loaded. So this only reports that TRACKS
        /// were listed, and the emitter decides what to do with them.
        /// </remarks>
        public bool HasOwnMusicTracks
        {
            get
            {
                string[] tracks = MusicTracks;
                for (int i = 0; i < tracks.Length; i++)
                {
                    if (!string.IsNullOrEmpty(tracks[i]))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void SetPortalSoundSettings(
            string newPlacedActivationSound,
            int newInstantSoundMode,
            string newInstantActivationSound,
            string newInstantDeactivationSound,
            string newInstantLoopSound)
        {
            placedPortalActivationSound = newPlacedActivationSound ?? string.Empty;
            instantPortalSoundMode = Mathf.Clamp(newInstantSoundMode, 0, 1);
            instantPortalActivationSound = newInstantActivationSound ?? string.Empty;
            instantPortalDeactivationSound = newInstantDeactivationSound ?? string.Empty;
            instantPortalLoopSound = newInstantLoopSound ?? string.Empty;
        }

        public DimensionBounds ReservedLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(reservedLocalMin.x, reservedLocalMin.y),
                    new int2(reservedLocalMaxExclusive.x, reservedLocalMaxExclusive.y));
            }
        }

        public void ConfigureIdentity(
            string newDimensionId,
            string newDisplayName,
            string newDescription,
            string newContentPackId,
            string newContentPackDisplayName,
            string newContentPackVersion,
            string newContentPackAuthor,
            int newMinimumApiVersion,
            DimensionType newDimensionType,
            DimensionCapabilityFlags newCapabilities)
        {
            dimensionId = newDimensionId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            description = newDescription ?? string.Empty;
            contentPackId = newContentPackId ?? string.Empty;
            contentPackDisplayName = newContentPackDisplayName ?? string.Empty;
            contentPackVersion = newContentPackVersion ?? string.Empty;
            contentPackAuthor = newContentPackAuthor ?? string.Empty;
            minimumApiVersion = Mathf.Max(1, newMinimumApiVersion);
            dimensionType = newDimensionType;
            capabilities = newCapabilities;
        }

        public void ApplyCustomizerMetadata(
            string newDimensionId,
            string newDisplayName,
            string newDescription,
            string newContentPackId,
            string newContentPackDisplayName,
            string newContentPackVersion,
            string newContentPackAuthor,
            int newMinimumApiVersion)
        {
            dimensionId = newDimensionId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            description = newDescription ?? string.Empty;
            contentPackId = newContentPackId ?? string.Empty;
            contentPackDisplayName = newContentPackDisplayName ?? string.Empty;
            contentPackVersion = newContentPackVersion ?? string.Empty;
            contentPackAuthor = newContentPackAuthor ?? string.Empty;
            minimumApiVersion = Mathf.Max(1, newMinimumApiVersion);
        }

        public void SetDependencyContentPackIds(IReadOnlyList<string> dependencies)
        {
            dependencyContentPackIds = CopyUniqueStrings(dependencies);
        }

        public void ConfigurePlacement(Vector2Int newAbsoluteOrigin, int newGenerationVersion)
        {
            absoluteOrigin = newAbsoluteOrigin;
            generationVersion = Mathf.Max(1, newGenerationVersion);
        }

        public void ApplyReservedLocalBounds(DimensionBounds bounds)
        {
            reservedLocalMin = new Vector2Int(bounds.Min.x, bounds.Min.y);
            reservedLocalMaxExclusive = EnsureExclusiveMax(
                reservedLocalMin,
                new Vector2Int(bounds.MaxExclusive.x, bounds.MaxExclusive.y));
        }

        public void SetLayoutTemplate(DimensionLayoutTemplateAsset template)
        {
            layoutTemplate = template;
        }

        public void SetBiomes(IReadOnlyList<BiomeTemplateAsset> assets)
        {
            biomes = CopyObjects(assets);
        }

        public void SetGlobalScenes(IReadOnlyList<SceneTemplateAsset> assets)
        {
            globalScenes = CopyObjects(assets);
        }

        public void SetGlobalItems(IReadOnlyList<DimensionItemAsset> assets)
        {
            globalItems = CopyObjects(assets);
        }

        public void SetTilesets(IReadOnlyList<DimensionTilesetAsset> assets)
        {
            tilesets = CopyObjects(assets);
        }

        public void SetGlobalRecipes(IReadOnlyList<DimensionRecipeAsset> assets)
        {
            globalRecipes = CopyObjects(assets);
        }

        public void SetGlobalWorkbenches(IReadOnlyList<DimensionWorkbenchAsset> assets)
        {
            globalWorkbenches = CopyObjects(assets);
        }

        public void SetGlobalLootTables(IReadOnlyList<DimensionLootTableAsset> assets)
        {
            globalLootTables = CopyObjects(assets);
        }

        public void SetGlobalAnimals(IReadOnlyList<DimensionAnimalAsset> assets)
        {
            globalAnimals = CopyObjects(assets);
        }

        public void SetGlobalCritters(IReadOnlyList<DimensionCritterAsset> assets)
        {
            globalCritters = CopyObjects(assets);
        }

        public void SetGlobalGameSetups(IReadOnlyList<DimensionGameSetupAsset> assets)
        {
            globalGameSetups = CopyObjects(assets);
        }

        public void SetGlobalConditions(IReadOnlyList<DimensionConditionAsset> assets)
        {
            globalConditions = CopyObjects(assets);
        }

        public void SetGlobalMobs(IReadOnlyList<DimensionMobAsset> assets)
        {
            globalMobs = CopyObjects(assets);
        }

        public void SetGlobalBosses(IReadOnlyList<DimensionBossAsset> assets)
        {
            globalBosses = CopyObjects(assets);
        }

        public void SetGlobalGenerationPasses(IReadOnlyList<GenerationPassTemplateAsset> assets)
        {
            globalGenerationPasses = CopyObjects(assets);
        }

        public void SetPortalAccessRules(IReadOnlyList<DimensionPortalAccessRuleAsset> assets)
        {
            portalAccessRules = CopyObjects(assets);
        }

        public void ConfigurePortalAppearance(
            float activationChargeSeconds,
            DimensionPortalVisualProfileAsset visualProfile)
        {
            portalActivationChargeSeconds = Mathf.Max(0.0f, activationChargeSeconds);
            portalVisualProfile = visualProfile;
        }

        public void SetPortalVisualProfile(DimensionPortalVisualProfileAsset visualProfile)
        {
            portalVisualProfile = visualProfile;
        }

        public void SetItemPortalVisualProfile(DimensionPortalVisualProfileAsset visualProfile)
        {
            itemPortalVisualProfile = visualProfile;
        }

        public DimensionDefinition ToDimensionDefinition()
        {
            return ToDimensionDefinition(ReservedLocalBounds);
        }

        public DimensionDefinition ToDimensionDefinition(DimensionBounds resolvedReservedLocalBounds)
        {
            return new DimensionDefinition(
                dimensionId,
                displayName,
                new int2(absoluteOrigin.x, absoluteOrigin.y),
                resolvedReservedLocalBounds,
                generationVersion,
                // Normalize guards the one YAML case FormerlySerializedAs cannot: a raw 0.
                DimensionTypeMigration.Normalize((int)dimensionType),
                NormalizeRuntimeCapabilities(capabilities),
                DimensionLifecycleState.Registered);
        }

        private static DimensionCapabilityFlags NormalizeRuntimeCapabilities(
            DimensionCapabilityFlags source)
        {
            if ((source & DimensionCapabilityFlags.PlayerTravel) == DimensionCapabilityFlags.PlayerTravel)
            {
                source |= DimensionCapabilityFlags.AreaLoading;
                source |= DimensionCapabilityFlags.SimulationLoading;
            }

            return source;
        }

        private static Vector2Int EnsureExclusiveMax(Vector2Int localMin, Vector2Int localMaxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(localMin.x + 1, localMaxExclusive.x),
                Mathf.Max(localMin.y + 1, localMaxExclusive.y));
        }

        private static string[] CopyUniqueStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return new string[0];
            }

            List<string> destination = new List<string>();
            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i];
                if (!string.IsNullOrEmpty(value) && !ContainsString(destination, value))
                {
                    destination.Add(value);
                }
            }

            return destination.ToArray();
        }

        private static T[] CopyObjects<T>(IReadOnlyList<T> source)
            where T : Object
        {
            if (source == null || source.Count == 0)
            {
                return new T[0];
            }

            List<T> destination = new List<T>();
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item != null)
                {
                    destination.Add(item);
                }
            }

            return destination.ToArray();
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
