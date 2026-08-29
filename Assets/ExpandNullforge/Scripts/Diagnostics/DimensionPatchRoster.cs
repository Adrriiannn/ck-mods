using System;

namespace ExpandNullforge.Diagnostics
{
    /// <summary>
    /// Every Harmony patch this framework declares, what it is for, and how many times it has run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS CAN AND CANNOT PROVE. It cannot prove a patch BOUND. Core Keeper's mod sandbox
    /// denies the type <c>HarmonyLib.Harmony</c> outright (<c>Docs/SandboxDenyList.txt</c>), so
    /// <c>Harmony.GetPatchInfo</c> is not callable from shipped code, and resolving a patch target
    /// to a <c>MethodBase</c> would need <c>System.Reflection</c>, which is denied as well.
    /// Declaring a patch is the sanctioned route; asking the patcher what it did is not.
    /// </para>
    /// <para>
    /// SO THE COUNTER IS THE WHOLE OF THE EVIDENCE. Each patch class carries
    /// <c>internal static int Fired</c> and increments it as the first statement of every prefix
    /// and postfix. A count of zero means one of two things — the patch never bound, or it bound
    /// and the game never reached the method — and the message says both, because from inside the
    /// process they are indistinguishable and the reader can tell them apart from the loader's own
    /// "failed to patch mod ExpandNullforge" line, which appears above ours when the first is true.
    /// </para>
    /// <para>
    /// A ZERO IS ONLY REPORTED WHEN SOMETHING WAS WAITING ON IT. <see cref="Row.CountWork"/> is the
    /// registry that gives the patch a reason to exist; when it is empty, a patch that never ran is
    /// not a failure and saying so would be noise. Patches that only run when a player does
    /// something carry <see cref="Row.OnlyOnPlayerAction"/> and are never reported as failures at
    /// all — nothing proves they work until somebody opens the map or places a block.
    /// </para>
    /// <para>
    /// PATCHING IS PROCESS-WIDE, NOT PER WORLD, so these are reported once, tagged <c>MOD</c>. A
    /// count is not attributable to the server world or the client world; several of these targets
    /// run in neither.
    /// </para>
    /// </remarks>
    internal static class DimensionPatchRoster
    {
        internal sealed class Row
        {
            public Row(
                string patchClass,
                string target,
                Func<int> fired,
                bool onlyOnPlayerAction,
                string workName,
                Func<int> countWork,
                string whatBreaks)
            {
                PatchClass = patchClass;
                Target = target;
                Fired = fired;
                OnlyOnPlayerAction = onlyOnPlayerAction;
                WorkName = workName;
                CountWork = countWork;
                WhatBreaks = whatBreaks;
            }

            /// <summary>The patch class name, as it appears in the source.</summary>
            public string PatchClass { get; private set; }

            /// <summary>The game method it is declared against.</summary>
            public string Target { get; private set; }

            /// <summary>How many times it has run this session.</summary>
            public Func<int> Fired { get; private set; }

            /// <summary>Whether nothing but a player doing something can make it run.</summary>
            public bool OnlyOnPlayerAction { get; private set; }

            /// <summary>What holds the rows this patch exists to deliver.</summary>
            public string WorkName { get; private set; }

            /// <summary>How many such rows there are, or null when nothing can count them.</summary>
            public Func<int> CountWork { get; private set; }

            /// <summary>What a player would notice if it never runs.</summary>
            public string WhatBreaks { get; private set; }
        }

        private static int CountOf(System.Collections.IEnumerable rows)
        {
            return DimensionSystemRoster.CountOf(rows);
        }

        private static int CountConditions()
        {
            return ExpandNullforge.Conditions.DimensionConditionRegistry.Count;
        }

        private static int CountTilesets()
        {
            return CountOf(
                ExpandNullforge.Tilesets.DimensionTilesetRegistry.All);
        }

        private static int GeneratedItems()
        {
            return ExpandNullforge.Foundation.DimensionItemObjectRegistry.DeclaredCount;
        }

        private static readonly Row[] RowsValue =
        {
            // ---- conditions ---------------------------------------------------------------
            new Row(
                "DimensionConditionsTablePatch",
                "ConditionsTableConverter.Convert",
                () => Conditions.DimensionConditionsTablePatch.Fired,
                false,
                "DimensionConditionRegistry",
                CountConditions,
                "the table the game built does not contain this mod's stat effects, so they show "
                    + "as blank buffs with no name and no picture"),
            new Row(
                "DimensionConditionInfoPatch",
                "ConditionsTable.GetConditionInfo",
                () => Conditions.DimensionConditionInfoPatch.Fired,
                false,
                "DimensionConditionRegistry",
                CountConditions,
                "a custom stat effect on the buff bar has no name and no picture"),

            // ---- loot ---------------------------------------------------------------------
            new Row(
                "DimensionLootTableConverterPatch",
                "LootTableConverter.Convert",
                () => Loot.DimensionLootTableConverterPatch.Fired,
                false,
                "DimensionLootTableRegistry",
                () => Loot.DimensionLootTableRegistry.QueuedTableCount,
                "no custom loot table reaches the game, so nothing drops from any of them"),
            new Row(
                "DimensionPortalLootTablePatch",
                "LootTableConverter.Convert",
                () => Portals.DimensionPortalLootTablePatch.Fired,
                false,
                "DimensionPortalDropRegistry",
                () => Portals.DimensionPortalDropRegistry.QueuedDropCount,
                "the drops this mod adds onto other things never appear"),

            // ---- object links -------------------------------------------------------------
            new Row(
                "DimensionPlacedOnLinkPatch",
                "ECSManager.ConfigurePostConverters",
                () => Objects.DimensionPlacedOnLinkPatch.Fired,
                false,
                "the items this mod declared",
                GeneratedItems,
                "an object that is meant to stand on one of this mod's own blocks can be put down "
                    + "nowhere"),
            new Row(
                "DimensionSeedLinkPatch",
                "ECSManager.ConfigurePostConverters",
                () => Plants.DimensionSeedLinkPatch.Fired,
                false,
                "the items this mod declared",
                GeneratedItems,
                "a seed whose plant was converted after it grows nothing"),

            // ---- creatures, respawn, spawning ---------------------------------------------
            new Row(
                "DimensionCreatureSpawnHook",
                "PugMods.SpawnTable.Init",
                () => Zones.DimensionCreatureSpawnHook.Fired,
                false,
                "DimensionCreatureSpawnRegistry",
                () => CountOf(Zones.DimensionCreatureSpawnRegistry.All),
                "no custom creature spawns naturally anywhere"),
            new Row(
                "DimensionRespawnTableConverterPatch",
                "EnvironmentSpawnObjectsTableConverter.Convert",
                () => Creatures.DimensionRespawnTableConverterPatch.Fired,
                false,
                "DimensionRespawnRegistry",
                () => Creatures.DimensionRespawnRegistry.PendingCount,
                "nothing this mod asked to come back comes back"),
            new Row(
                "DimensionAmbientSpawnOrderHook",
                "SpawnEnvironmentObjectsPeriodicallySystem.OnUpdate",
                () => Zones.DimensionAmbientSpawnOrderHook.Fired,
                false,
                "DimensionCreatureSpawnRegistry",
                () => CountOf(Zones.DimensionCreatureSpawnRegistry.All),
                "the game's own ambient spawning runs everywhere, including in areas set to block it"),

            // ---- world rules --------------------------------------------------------------
            new Row(
                "DimensionUpgradeCostsTableConverterPatch",
                "UpgradeCostsTableConverter.Convert",
                () => WorldRules.DimensionUpgradeCostsTableConverterPatch.Fired,
                false,
                "DimensionUpgradeCostRegistry",
                () => WorldRules.DimensionUpgradeCostRegistry.PendingCount,
                "the game's own upgrade costs apply and this mod's overrides do nothing"),
            new Row(
                "DimensionFishingTableConverterPatch",
                "FishingTableConverter.Convert",
                () => WorldRules.DimensionFishingTableConverterPatch.Fired,
                false,
                "DimensionFishFightRegistry",
                () => WorldRules.DimensionFishFightRegistry.PendingCount,
                "custom fish fights do nothing and only the game's own fish can be caught"),
            new Row(
                "DimensionPlayerAuthoringConverterPatch",
                "PlayerAuthoringConverter.Convert",
                () => WorldRules.DimensionPlayerAuthoringConverterPatch.Fired,
                false,
                "DimensionPlayerOverrideRegistry",
                () => WorldRules.DimensionPlayerOverrideRegistry.OverridesMovement ? 1 : 0,
                "the player's turning delay and vehicle drift stay the game's own"),
            new Row(
                "DimensionPlayerAimPositionConverterPatch",
                "PlayerAimPositionConverter.Convert",
                () => WorldRules.DimensionPlayerAimPositionConverterPatch.Fired,
                false,
                "DimensionPlayerOverrideRegistry",
                () => WorldRules.DimensionPlayerOverrideRegistry.OverridesAim ? 1 : 0,
                "the aim offset override does nothing"),

            // ---- scenes and dungeons ------------------------------------------------------
            new Row(
                "DimensionSpawnDungeonAndScenePatch",
                "SpawnDungeonAndSceneSystem.OnStartRunning",
                () => Scenes.DimensionSpawnDungeonAndScenePatch.Fired,
                false,
                "DimensionCustomSceneRegistry",
                () => Scenes.DimensionCustomSceneRegistry.Count,
                "the custom scene table is not in place when the dungeon generator caches it, so "
                    + "every custom room resolves no scene and dungeons generate as empty caves"),
            new Row(
                "DimensionUniqueDungeonInjector",
                "SpawnUniqueDungeonInitSystem.OnStartRunning",
                () => Scenes.DimensionUniqueDungeonInjector.Fired,
                false,
                "DimensionUniqueDungeonRegistry",
                () => CountOf(Scenes.DimensionUniqueDungeonRegistry.All),
                "no one-off dungeon this mod added is placed in the world"),

            // ---- tilesets -----------------------------------------------------------------
            new Row(
                "DimensionTilesetTypeUtilityPatch",
                "TilesetTypeUtility (8 lookups: GetTileset, GetTilesetTextures, GetTexture, "
                    + "GetAdaptiveTexture, GetOverrideMaterial, GetEditorOverrideMaterial, "
                    + "GetOverrideParticles, GetFriendlyName)",
                () => Tilesets.DimensionTilesetTypeUtilityPatch.Fired,
                false,
                "DimensionTilesetRegistry",
                CountTilesets,
                "a custom tileset renders as whatever the game's own table holds at that index, "
                    + "which is a vanilla tileset or nothing"),
            new Row(
                "DimensionTileColorPatch",
                "TileTypeColorLookupSystem.OnCreate",
                () => Tilesets.DimensionTileColorPatch.Fired,
                false,
                "DimensionTilesetRegistry",
                CountTilesets,
                "custom tiles have no colour on the map"),
            new Row(
                "DimensionQuadGeneratorTilesetPatch",
                "QuadGenerator.HasTileset",
                () => Tilesets.DimensionQuadGeneratorTilesetPatch.Fired,
                true,
                "DimensionTilesetRegistry",
                CountTilesets,
                "a custom tileset draws nothing on the layer the quad generator was asked about"),
            // NOT PLAYER-DRIVEN, AND IT WAS MARKED THAT WAY. Its counter is incremented as the
            // first statement of the prefix, before the id test, so it counts every tile the game
            // writes anywhere — world generation included. A zero here is therefore an unambiguous
            // "this patch never bound", available on every load without a player touching
            // anything, on the patch whose failure is the tileset bug this framework exists around.
            // Marked as player-driven it was the one thing the audit could never say a word about.
            new Row(
                "DimensionAddTilePatch",
                "EntityUtility.AddTile",
                () => Tilesets.DimensionAddTilePatch.Fired,
                false,
                "DimensionTilesetRegistry",
                CountTilesets,
                "placing a custom block is rejected by the game (\"Trying to add invalid tileset\") "
                    + "and the block does not exist on the server"),
            new Row(
                "DimensionGroundBehaviourPatch",
                "PlayerController.UpdateOnTileEffects",
                () => Tilesets.DimensionGroundBehaviourPatch.Fired,
                true,
                "DimensionTilesetRegistry",
                CountTilesets,
                "walking on a custom ground feels exactly like walking on dirt"),

            // ---- portals ------------------------------------------------------------------
            new Row(
                "DimensionItemPortalUseHook",
                "EquipmentSlot.UpdateEquipment",
                () => Portals.DimensionItemPortalUseHook.Fired,
                true,
                "DimensionItemPortalRegistry",
                () => CountOf(
                    Portals.DimensionItemPortalRegistry.RegisteredItemNames
                       ),
                "using a portal item does nothing at all"),
            // ALSO NOT PLAYER-DRIVEN. It is a postfix on a system's OnUpdate, so it runs on every
            // frame that system updates whether or not anybody is holding a portal item. Its zero
            // is the cleanest proof of an unbound patch the framework has, and suppressing it threw
            // that away.
            new Row(
                "DimensionEquipmentUpdateForceJobCompletePatch",
                "EquipmentUpdateSystem.OnUpdate",
                () => Portals.DimensionEquipmentUpdateForceJobCompletePatch.Fired,
                false,
                "DimensionItemPortalRegistry",
                () => CountOf(
                    Portals.DimensionItemPortalRegistry.RegisteredItemNames
                       ),
                "a portal item's use is read a frame late or not at all"),
            new Row(
                "DimensionItemPortalCooldownUiHook",
                "EquipmentSlot.GetNormalizedCooldownRemainingForItem",
                () => Portals.DimensionItemPortalCooldownUiHook.Fired,
                true,
                "DimensionItemPortalRegistry",
                () => CountOf(
                    Portals.DimensionItemPortalRegistry.RegisteredItemNames
                       ),
                "a portal item's cooldown sweep does not show on its slot"),
            new Row(
                "DimensionPortalOfferingHintPatch",
                "InventorySlotUI.ShowHint",
                () => Portals.DimensionPortalOfferingHintPatch.Fired,
                true,
                "the portals registered with the dimension service",
                null,
                "a portal's offering window shows no picture of what it wants"),

            // ---- interface ----------------------------------------------------------------
            new Row(
                "DimensionBossPinHook",
                "MapUI.Awake",
                () => Creatures.DimensionBossPinHook.Fired,
                true,
                "the boss map markers registered by generated content",
                null,
                "a boss pin on the map has no picture"),
            new Row(
                "DimensionTalentIconPatch",
                "SkillTalentUIElement.UpdateTalent",
                () => Skills.DimensionTalentIconPatch.Fired,
                true,
                "DimensionTalentIconRegistry",
                () => Skills.DimensionTalentIconRegistry.Count,
                "a talent this mod changed keeps the game's own picture"),
            new Row(
                "DimensionRegionTitleHook",
                "RegionTitleHandler.Awake",
                () => Zones.DimensionRegionTitleHook.Fired,
                false,
                "DimensionRegionTitleRegistry",
                () => CountOf(Zones.DimensionRegionTitleRegistry.All),
                "the title card shows the game's own biome name instead of the named area"),

            // ---- sound and music ----------------------------------------------------------
            new Row(
                "DimensionAmbienceInstallHook",
                "AmbientSoundsHandler.Awake",
                () => Zones.DimensionAmbienceInstallHook.Fired,
                false,
                "DimensionBiomeAtmosphereRegistry",
                () => CountOf(
                    Zones.DimensionBiomeAtmosphereRegistry.All),
                "a custom biome is silent instead of carrying its ambience"),
            new Row(
                "DimensionBiomeMusicInstallHook",
                "GameMusicHandler.Start",
                () => Zones.DimensionBiomeMusicInstallHook.Fired,
                false,
                "DimensionBiomeAtmosphereRegistry",
                () => CountOf(
                    Zones.DimensionBiomeAtmosphereRegistry.All),
                "a custom biome plays the game's own music for whatever it was mapped onto"),
            new Row(
                "DimensionAmbienceAssetHook",
                "AmbientSoundsHandler.AudioInfo.LoadAudioAsset",
                () => Zones.DimensionAmbienceAssetHook.Fired,
                false,
                "DimensionBiomeAtmosphereRegistry",
                () => CountOf(
                    Zones.DimensionBiomeAtmosphereRegistry.All),
                "the ambience a custom biome named never loads"),
            new Row(
                "DimensionMusicRosterInstallHook",
                "GameMusicHandler.Start",
                () => Zones.DimensionMusicRosterInstallHook.Fired,
                false,
                "DimensionMusicRosterRegistry",
                // This was the one reportable row with nothing to count, which is exactly what
                // DimensionSelfAuditTests forbids: with no count the audit could have named it on
                // a session where no cue had been registered and nothing was wrong.
                () => Zones.DimensionMusicRosterRegistry.Count,
                "a custom music roster is never installed, so its tracks never play"),
            new Row(
                "DimensionMusicRosterPlayHook",
                "MusicManager.PlayMusic",
                () => Zones.DimensionMusicRosterPlayHook.Fired,
                true,
                "DimensionMusicRosterRegistry",
                null,
                "a custom roster's track is asked for and the game plays its own instead"),
            new Row(
                "DimensionMusicOverrideHook",
                "GameMusicHandler.GetActiveMusicArea",
                () => Zones.DimensionMusicOverrideHook.Fired,
                true,
                "DimensionMusicOverrideRegistry",
                null,
                "a dimension or a boss phase does not change the music"),
        };

        /// <summary>Every patch class, one row each.</summary>
        public static Row[] All
        {
            get { return RowsValue; }
        }
    }
}
