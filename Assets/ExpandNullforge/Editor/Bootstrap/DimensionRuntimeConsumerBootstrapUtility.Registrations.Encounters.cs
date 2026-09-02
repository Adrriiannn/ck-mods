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
    /// What the emitted script registers about bosses, spawns and respawning.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Emits each boss's phases: the health thresholds and what happens at them.
        /// </summary>
        /// <remarks>
        /// A phase whose action needs a target it does not have is dropped here rather than shipped —
        /// a summon with nothing to summon, or a condition with no condition named, would register
        /// fine and then do nothing at the most visible moment of a fight.
        /// </remarks>
        private static void AppendBossPhaseRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled)
                {
                    continue;
                }

                DimensionBossPhaseTemplate[] phases = boss.Phases;
                if (phases == null || phases.Length == 0)
                {
                    continue;
                }

                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);

                for (int p = 0; p < phases.Length; p++)
                {
                    DimensionBossPhaseTemplate phase = phases[p];
                    if (phase == null || !phase.Enabled)
                    {
                        continue;
                    }

                    bool needsTarget =
                        phase.Action == DimensionBossPhaseActionKind.SummonAdds ||
                        phase.Action == DimensionBossPhaseActionKind.ApplyConditionToSelf ||
                        phase.Action == DimensionBossPhaseActionKind.ApplyConditionToPlayers;

                    if (needsTarget && string.IsNullOrEmpty(phase.ActionTarget))
                    {
                        Debug.LogWarning(
                            "[ExpandNullforge] Boss '" + boss.BossId + "' phase '" + phase.PhaseId +
                            "' is set to " + phase.Action + " but names nothing to act on, so it was " +
                            "left out. The fight will reach that health and do nothing.");
                        continue;
                    }

                    // A summoned creature is one of the mod's own; a condition is one of the game's.
                    // Qualifying a condition name would point it at an object that does not exist.
                    string target = phase.Action == DimensionBossPhaseActionKind.SummonAdds
                        ? DimensionObjectNamespace.Qualify(modName, phase.ActionTarget)
                        : phase.ActionTarget;

                    builder.AppendLine("    DimensionBossPhaseRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(phase.PhaseId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(bossName)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.HealthThreshold.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        DimensionBossPhaseAction.")
                        .Append(phase.Action.ToString()).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(target)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.ActionAmount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                    builder.Append("        ")
                        .Append(phase.ActionDuration.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        ")
                        .Append(phase.Radius.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                    // The tenth field, and the easy one to drop here: the phase's own music.
                    builder.Append("        ").Append(ToCSharpString(phase.MusicCueId)).AppendLine(");");
                }
            }
        }

        /// <summary>
        /// Emits each mob's keeps-coming-back rule into the game's own periodic respawn table.
        /// </summary>
        /// <remarks>
        /// The tileset resolves at emission because only the editor knows whether a name is one
        /// of this template's tilesets (minted id, computed at runtime from the same name) or a
        /// vanilla tileset (enum literal). A name that is neither is refused here, loudly,
        /// rather than emitted as a rule that can never match a tile.
        /// </remarks>
        private static void AppendRespawnRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionMobAsset[] mobs = template == null ? null : template.GlobalMobs;
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || mob.Respawn == null ||
                    !mob.Respawn.KeepsComingBack || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                DimensionRespawnTemplate respawn = mob.Respawn;
                string creatureName = DimensionObjectNamespace.Qualify(modName, mob.MobId);

                string tilesetExpression = null;
                string tilesetName = respawn.OnTileset;
                if (string.IsNullOrEmpty(tilesetName))
                {
                    tilesetExpression = string.Empty;
                }
                else if (FindTileset(template, tilesetName) != null)
                {
                    tilesetExpression =
                        "DimensionTilesetRegistry.ComputeTilesetId(" +
                        ToCSharpString(tilesetName) + ")";
                }
                else if (System.Enum.TryParse(tilesetName, false, out PugTilemap.Tileset vanillaTileset))
                {
                    tilesetExpression = "(int)PugTilemap.Tileset." + vanillaTileset;
                }
                else
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + mob.DisplayName + "' keeps coming back on " +
                        "tileset '" + tilesetName + "', which is neither one of this mod's " +
                        "tilesets nor a vanilla one. The rule was left out — fix the name.");
                    continue;
                }

                // The four surfaces vanilla's own respawn files key on; the value map lives
                // here, the one place allowed to know the game's numbers.
                string tileType;
                switch (respawn.Surface)
                {
                    case DimensionRespawnSurface.Nest:
                        tileType = "PugTilemap.TileType.chrysalis";
                        break;
                    case DimensionRespawnSurface.SlimeCoat:
                        tileType = "PugTilemap.TileType.groundSlime";
                        break;
                    case DimensionRespawnSurface.Water:
                        tileType = "PugTilemap.TileType.water";
                        break;
                    default:
                        tileType = "PugTilemap.TileType.ground";
                        break;
                }

                builder.AppendLine("    DimensionRespawnRegistry.Register(new DimensionRespawnRegistry.RespawnRule");
                builder.AppendLine("    {");
                builder.Append("        RuleName = ")
                    .Append(ToCSharpString(creatureName + ":respawn")).AppendLine(",");
                builder.Append("        CreatureObjectName = ")
                    .Append(ToCSharpString(creatureName)).AppendLine(",");
                builder.Append("        TileType = ").Append(tileType).AppendLine(",");
                builder.Append("        Tilesets = ")
                    .Append(string.IsNullOrEmpty(tilesetExpression)
                        ? "new int[0]"
                        : "new int[] { " + tilesetExpression + " }")
                    .AppendLine(",");
                builder.Append("        Chance = ")
                    .Append(respawn.Chance.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        ChanceDecayPerExisting = ")
                    .Append(respawn.CrowdSlowdown.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        MaxPerTile = ")
                    .Append(respawn.MostPerTile.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("        MaxPerSweep = ")
                    .Append(respawn.MostPerSweep.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.Append("        MinTilesRequired = ")
                    .Append(respawn.FewestTilesNeeded.ToString("R", CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.AppendLine("        OnlyInBiome = \"\"");
                builder.AppendLine("    });");
            }
        }

        /// <summary>
        /// Emits each boss's presentation row (pin, floating name) and respawn cooldown.
        /// </summary>
        /// <remarks>
        /// The literals half of the two-half registry: names and terms bake here; the sprites
        /// attach at runtime when the manifest loads, because a Sprite only exists once the
        /// bundle does. The respawn row only exists for a positive cooldown — zero means
        /// vanilla's own "gone means summonable", which needs no machinery.
        /// </remarks>
        private static void AppendBossPresentationRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);
                string nameTerm = "Names/" + DimensionLocalizationCsv.ToLookupKeyName(bossName);
                string hoverTerm = string.IsNullOrEmpty(boss.MapPin.HoverName)
                    ? nameTerm
                    : "Names/" + DimensionLocalizationCsv.ToLookupKeyName(bossName + "-pin");

                builder.AppendLine("    DimensionBossPresentationRegistry.Register(");
                builder.AppendLine("        new DimensionBossPresentationDefinition(");
                builder.Append("            ").Append(ToCSharpString(bossName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(boss.BossId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(nameTerm)).AppendLine(",");
                builder.Append("            ")
                    .Append(boss.MapPin.ShowsOnTheMap ? "true" : "false").AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(hoverTerm)).AppendLine("));");

                if (boss.RespawnCooldownMinutes > 0f)
                {
                    builder.Append("    DimensionBossRespawnRegistry.Register(")
                        .Append(ToCSharpString(bossName))
                        .Append(", ")
                        .Append(boss.RespawnCooldownMinutes.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f);");
                }

                // A fight-music name that is not a vanilla roster and carries its own tracks is
                // the mod's own cue; the runtime registry gives it a roster the game can pick.
                MusicRosterType vanillaRoster;
                string[] trackKeys = boss.FightMusic.CustomTrackKeys;
                if (trackKeys.Length > 0 &&
                    !string.IsNullOrEmpty(boss.FightMusic.MusicId) &&
                    !System.Enum.TryParse(boss.FightMusic.MusicId, false, out vanillaRoster))
                {
                    builder.Append("    DimensionMusicRosterRegistry.RegisterCue(")
                        .Append(ToCSharpString(boss.FightMusic.MusicId))
                        .Append(", new string[] { ");
                    bool wroteTrack = false;
                    for (int t = 0; t < trackKeys.Length; t++)
                    {
                        if (string.IsNullOrEmpty(trackKeys[t]))
                        {
                            continue;
                        }

                        if (wroteTrack)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(ToCSharpString(trackKeys[t]));
                        wroteTrack = true;
                    }

                    builder.AppendLine(" });");
                }
            }
        }

        /// <summary>
        /// Emits each creature's claim on where in the world it appears.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creature with no biome listed is emitted once, unrestricted — Core Keeper reads an empty
        /// biome as "anywhere", which is what an author means by leaving it blank. A creature listing
        /// several biomes is emitted once per biome, because that is how the game's table is shaped:
        /// each row answers one "where", and a bat common in caves and rare outside is genuinely two
        /// rows rather than one with a condition.
        /// </para>
        /// <para>
        /// The tilesets come from the biome's own blocks, the same derivation the title cards use, so
        /// "in this biome" means the same thing to spawning as it does to everything else.
        /// </para>
        /// </remarks>
        private static void AppendCreatureSpawnRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionMobAsset[] mobs = template == null ? null : template.GlobalMobs;
            DimensionAnimalAsset[] animals = template == null ? null : template.GlobalAnimals;
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            if (mobs != null)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    DimensionMobAsset mob = mobs[i];
                    if (mob == null || !mob.Enabled || !mob.SpawnsInWorld)
                    {
                        continue;
                    }

                    AppendSpawnRowsFor(
                        builder,
                        template,
                        tilesets,
                        DimensionObjectNamespace.Qualify(modName, mob.MobId),
                        mob.AllowedBiomeIds,
                        mob.SpawnChance,
                        mob.SpawnAmount,
                        mob.SpawnsInGroups,
                        mob.CanSpawnInBlockedArea);

                    // The elite spawns wherever its parent does, just far less often — which is the
                    // whole of what makes it feel like a rare encounter rather than a second enemy.
                    DimensionEliteVariantTemplate elite = mob.EliteVariant;
                    if (elite.Enabled)
                    {
                        AppendSpawnRowsFor(
                            builder,
                            template,
                            tilesets,
                            DimensionObjectNamespace.Qualify(
                                modName, DimensionEliteVariantTemplate.IdFor(mob.MobId)),
                            mob.AllowedBiomeIds,
                            mob.SpawnChance / elite.RarityFactor,
                            1,
                            false,
                            mob.CanSpawnInBlockedArea);
                    }
                }
            }

            if (animals != null)
            {
                for (int i = 0; i < animals.Length; i++)
                {
                    DimensionAnimalAsset animal = animals[i];
                    if (animal == null || !animal.Enabled || !animal.SpawnsInWorld)
                    {
                        continue;
                    }

                    AppendSpawnRowsFor(
                        builder,
                        template,
                        tilesets,
                        DimensionObjectNamespace.Qualify(modName, animal.AnimalId),
                        animal.AllowedBiomeIds,
                        animal.SpawnChance,
                        animal.SpawnAmount,
                        animal.SpawnsInGroups,
                        animal.CanSpawnInBlockedArea);
                }
            }
        }

        private static void AppendSpawnRowsFor(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionTilesetAsset[] tilesets,
            string objectName,
            string[] biomeIds,
            float spawnChance,
            int amount,
            bool clustered,
            bool canSpawnInBlockedArea)
        {
            if (biomeIds == null || biomeIds.Length == 0)
            {
                AppendSpawnRow(
                    builder, objectName, string.Empty, new List<int>(),
                    spawnChance, amount, clustered, canSpawnInBlockedArea);
                return;
            }

            for (int i = 0; i < biomeIds.Length; i++)
            {
                BiomeTemplateAsset biome = FindBiome(template, biomeIds[i]);
                List<int> tilesetIds = biome == null
                    ? new List<int>()
                    : CollectBiomeTilesetIds(biome, tilesets);

                AppendSpawnRow(
                    builder, objectName, biomeIds[i], tilesetIds,
                    spawnChance, amount, clustered, canSpawnInBlockedArea);
            }
        }

        private static void AppendSpawnRow(
            StringBuilder builder,
            string objectName,
            string biomeId,
            List<int> tilesetIds,
            float spawnChance,
            int amount,
            bool clustered,
            bool canSpawnInBlockedArea)
        {
            builder.AppendLine("    DimensionCreatureSpawnRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(biomeId)).AppendLine(",");

            builder.Append("        new int[] { ");
            for (int t = 0; t < tilesetIds.Count; t++)
            {
                if (t > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tilesetIds[t].ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine(" },");

            // Ground: the surface almost everything walks on. A creature that belongs in water or on
            // a wall is a different shape of authoring and is not offered yet rather than guessed at.
            builder.AppendLine("        PugTilemap.TileType.ground,");
            builder.Append("        ")
                .Append(spawnChance.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.AppendLine("        1,");
            builder.Append("        ").Append(amount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.AppendLine("        " + (clustered ? "true" : "false") + ",");
            builder.AppendLine("        " + (canSpawnInBlockedArea ? "true" : "false") + ");");
        }

        /// <summary>
        /// Puts a boss into the handmade place it waits in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A boss names its arena scene, and until now that name was only checked — the author
        /// still had to place the boss into the scene by hand, or walk into an empty arena. This
        /// closes it: the boss becomes an ordinary placed object at the middle of its scene,
        /// riding the same path every other scene object rides.
        /// </para>
        /// <para>
        /// An author who placed the boss themselves keeps their placement; only a scene that
        /// does not already contain it gets one, so this can never double a boss.
        /// </para>
        /// </remarks>
        private static void AppendArenaBosses(
            List<string> objectLines,
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            string modName)
        {
            DimensionBossAsset[] bosses = template == null ? null : template.GlobalBosses;
            if (bosses == null || scene == null || string.IsNullOrEmpty(scene.SceneId))
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId) ||
                    !string.Equals(boss.ArenaSceneId, scene.SceneId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (SceneAlreadyPlaces(scene, boss.BossId))
                {
                    continue;
                }

                Vector2Int centre = ResolveSceneCentre(scene);
                string bossName = DimensionObjectNamespace.Qualify(modName, boss.BossId);
                objectLines.Add(
                    "      sceneObjects.Add(new DimensionSceneObject(new int2(" +
                    centre.x.ToString(CultureInfo.InvariantCulture) + ", " +
                    centre.y.ToString(CultureInfo.InvariantCulture) + "), " +
                    ToCSharpString(bossName) +
                    ", DimensionSceneFacing.Down, DimensionScenePaintChoice.Unpainted, \"\", null));");
            }
        }
    }
}
