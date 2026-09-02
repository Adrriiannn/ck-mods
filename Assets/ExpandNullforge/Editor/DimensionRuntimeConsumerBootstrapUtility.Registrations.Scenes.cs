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
    /// What the emitted script registers about scenes and where they are placed.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Emits a scene registration for every authored scene that stamps terrain.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Emitted into the mod's own bootstrap rather than discovered at runtime because the scene
        /// table can only be rebuilt in one narrow window during world start — by then, nothing is
        /// going to go looking through asset files. Registration has to have already happened.
        /// </para>
        /// <para>
        /// Only scenes with tiles are emitted. A scene of pure props and spawns is placed by this
        /// framework's own systems and has no business occupying one of the world's scene slots.
        /// </para>
        /// <para>
        /// The name is mod-qualified and then checked here, at generation time, against the limit a
        /// spawn request can carry. Left to runtime it would register, never spawn, and log nothing.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Grows the placed portal in the game's own world, for a rule that asks to be found rather
        /// than crafted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is no separate "put this object in the Overworld" road in Core Keeper: the only
        /// thing world generation grows on its own is a custom scene. So this builds the smallest
        /// honest one — a single cleared tile with the portal standing on it — and hands it the
        /// same natural-growth arguments an authored scene uses. Everything after that is the
        /// game's: it picks a spot with enough clearance, clears the cell, lays the ground, and
        /// instantiates the prefab.
        /// </para>
        /// <para>
        /// The one tile is not decoration. Placement clears a scene's own cells before writing
        /// them, so the tile is what guarantees the portal is never found buried inside a wall or
        /// standing in water. It is laid as plain dirt, which reads as a small pad in biomes that
        /// are not dirt already.
        /// </para>
        /// <para>
        /// A rule that names no biome the Overworld actually samples registers nothing at all —
        /// <see cref="BuildOverworldSpawnArguments"/> says so out loud — because a scene with no
        /// biomes is invisible to the placer and would be dead weight in the table.
        /// </para>
        /// </remarks>
        private static void AppendPortalWorldSceneRegistration(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionRuntimePortalOutput portalOutput,
            string modName)
        {
            DimensionPortalAccessRuleAsset rule = FindPortalRule(
                template,
                DimensionPortalAccessKind.PlacedPortal,
                portalOutput.DimensionId,
                false);
            if (rule == null || !rule.GeneratedInWorld)
            {
                return;
            }

            string sceneName = DimensionObjectNamespace.Qualify(
                modName,
                portalOutput.DimensionId + ".portal");
            string nameError;
            if (!DimensionCustomSceneNames.IsValid(sceneName, out nameError))
            {
                Debug.LogWarning(
                    "[ExpandNullforge] The portal for '" + portalOutput.DimensionId + "' cannot be " +
                    "found in the world. " + nameError + " Shorten the dimension's id, or turn off " +
                    "\"Found in the world\" and let players craft the portal instead.");
                return;
            }

            string spawnArguments = BuildOverworldSpawnArguments(
                "The portal for '" + portalOutput.DimensionId + "'",
                rule.WorldBiomeNames,
                rule.WorldMaxOccurrences,
                rule.WorldMinDistanceFromCore);
            if (spawnArguments.Length == 0)
            {
                return;
            }

            builder.AppendLine("    {");
            builder.AppendLine("      var portalSpotTiles = new System.Collections.Generic.List<DimensionSceneTileRequest>();");
            builder.AppendLine("      portalSpotTiles.Add(new DimensionSceneTileRequest(new int2(0, 0), \"0\", DimensionTileRole.Ground));");
            builder.AppendLine("      var portalSpotObjects = new System.Collections.Generic.List<DimensionSceneObject>();");
            builder.AppendLine(
                "      portalSpotObjects.Add(new DimensionSceneObject(new int2(0, 0), PortalObjectName, " +
                "DimensionSceneFacing.Down, DimensionScenePaintChoice.Unpainted, \"\", null));");
            builder.Append("      var portalSpotResult = DimensionSceneTileCompiler.Compile(")
                .Append(ToCSharpString(sceneName))
                .AppendLine(", portalSpotTiles);");
            builder.AppendLine("      if (portalSpotResult.Tiles.Count > 0)");
            builder.AppendLine("      {");
            builder.Append("        DimensionCustomSceneRegistry.Register(new DimensionCustomSceneDefinition(")
                .Append(ToCSharpString(sceneName))
                .Append(", portalSpotResult.Tiles, centerPosition: new int2(0, 0)")
                .Append(", objects: portalSpotObjects")
                .Append(spawnArguments)
                .AppendLine("));");
            builder.AppendLine("      }");
            builder.AppendLine("    }");
        }

        private static void AppendSceneRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            List<SceneTemplateAsset> scenes = CollectSceneTemplates(template);
            Dictionary<SceneTemplateAsset, List<string>> owningBiomes = CollectSceneOwners(template);
            for (int i = 0; i < scenes.Count; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null || !scene.Enabled || !scene.HasTiles)
                {
                    continue;
                }

                string sceneName = DimensionObjectNamespace.Qualify(modName, scene.SceneId);
                string nameError;
                if (!DimensionCustomSceneNames.IsValid(sceneName, out nameError))
                {
                    Debug.LogWarning("[ExpandNullforge] Scene '" + scene.SceneId + "' cannot be registered. " + nameError);
                    continue;
                }

                // Build the tile lines first and only open the block if there are any: a scene whose
                // tiles are all disabled or blank would otherwise emit a block that registers nothing.
                List<string> tileLines = new List<string>();
                DimensionSceneTileTemplate[] tiles = scene.Tiles;
                for (int t = 0; t < tiles.Length; t++)
                {
                    DimensionSceneTileTemplate tile = tiles[t];
                    if (tile == null || !tile.Enabled || string.IsNullOrEmpty(tile.BlockId))
                    {
                        continue;
                    }

                    WarnIfTilesetHasNoBlockObject(template, scene, tile);

                    tileLines.Add(
                        "      sceneTiles.Add(new DimensionSceneTileRequest(new int2(" +
                        tile.LocalPosition.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        tile.LocalPosition.y.ToString(CultureInfo.InvariantCulture) + "), " +
                        ToCSharpString(DimensionObjectNamespace.Qualify(modName, tile.BlockId)) +
                        ", DimensionTileRole." + tile.Role + "));");
                }

                if (tileLines.Count == 0)
                {
                    continue;
                }

                // Objects are gathered the same way and for the same reason: a scene whose objects
                // are all disabled should emit no object list rather than an empty one.
                List<string> objectLines = new List<string>();
                DimensionSceneObjectTemplate[] sceneObjects = scene.SceneObjects;
                for (int o = 0; o < sceneObjects.Length; o++)
                {
                    DimensionSceneObjectTemplate placed = sceneObjects[o];
                    if (placed == null || !placed.Enabled || string.IsNullOrEmpty(placed.ObjectId))
                    {
                        continue;
                    }

                    // One of the mod's own objects is namespaced like everything else it generates;
                    // a vanilla one keeps the name the game already knows it by.
                    string objectName = IsModOwnedObjectName(template, placed.ObjectId)
                        ? DimensionObjectNamespace.Qualify(modName, placed.ObjectId)
                        : placed.ObjectId;

                    // Contents are built inline so each container carries its own list; a shared one
                    // would let two chests in a scene end up holding the same objects.
                    string contentsExpression = "null";
                    DimensionSceneContainerItem[] contents = placed.Contents;
                    if (contents.Length > 0)
                    {
                        StringBuilder contentsBuilder = new StringBuilder();
                        contentsBuilder.Append("new DimensionSceneContent[] { ");
                        bool wroteAny = false;

                        for (int c = 0; c < contents.Length; c++)
                        {
                            DimensionSceneContainerItem item = contents[c];
                            if (item == null || string.IsNullOrEmpty(item.ItemId))
                            {
                                continue;
                            }

                            if (wroteAny)
                            {
                                contentsBuilder.Append(", ");
                            }

                            string itemName = IsModOwnedObjectName(template, item.ItemId)
                                ? DimensionObjectNamespace.Qualify(modName, item.ItemId)
                                : item.ItemId;

                            contentsBuilder
                                .Append("new DimensionSceneContent(")
                                .Append(ToCSharpString(itemName)).Append(", ")
                                .Append(item.Amount.ToString(CultureInfo.InvariantCulture)).Append(")");
                            wroteAny = true;
                        }

                        contentsBuilder.Append(" }");
                        if (wroteAny)
                        {
                            contentsExpression = contentsBuilder.ToString();
                        }
                    }

                    objectLines.Add(
                        "      sceneObjects.Add(new DimensionSceneObject(new int2(" +
                        placed.LocalPosition.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        placed.LocalPosition.y.ToString(CultureInfo.InvariantCulture) + "), " +
                        ToCSharpString(objectName) +
                        ", DimensionSceneFacing." + placed.Facing +
                        ", DimensionScenePaintChoice." + placed.Paint +
                        ", " + ToCSharpString(placed.LootTableId) +
                        ", " + contentsExpression + "));");
                }

                AppendArenaBosses(objectLines, template, scene, modName);

                // Triggers travel with the scene the same way objects do: scene-local here, turned
                // into world-anchored registrations by the placement pass at stamp time — the first
                // moment anything knows where the scene landed.
                List<string> triggerLines = new List<string>();
                DimensionSceneTriggerTemplate[] triggers = scene.Triggers;
                for (int g = 0; g < triggers.Length; g++)
                {
                    DimensionSceneTriggerTemplate trigger = triggers[g];
                    if (trigger == null || !trigger.Enabled || string.IsNullOrEmpty(trigger.TriggerId))
                    {
                        continue;
                    }

                    string carriedItemName = IsModOwnedObjectName(template, trigger.CarriedItemId)
                        ? DimensionObjectNamespace.Qualify(modName, trigger.CarriedItemId)
                        : trigger.CarriedItemId;

                    // Only a summoned creature is one of the mod's own objects; a condition target
                    // is a name in the game's own vocabulary and must reach the runtime untouched.
                    string actionTarget = trigger.Action == ExpandNullforge.Zones.DimensionTileAction.SummonCreatures &&
                        IsModOwnedObjectName(template, trigger.Target)
                        ? DimensionObjectNamespace.Qualify(modName, trigger.Target)
                        : trigger.Target;

                    triggerLines.Add(
                        "      sceneTriggers.Add(new DimensionSceneTrigger(" +
                        ToCSharpString(trigger.TriggerId) +
                        ", new int2(" +
                        trigger.LocalMin.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        trigger.LocalMin.y.ToString(CultureInfo.InvariantCulture) + ")" +
                        ", new int2(" +
                        trigger.LocalMaxExclusive.x.ToString(CultureInfo.InvariantCulture) + ", " +
                        trigger.LocalMaxExclusive.y.ToString(CultureInfo.InvariantCulture) + ")" +
                        ", DimensionTileTrigger." + trigger.Kind +
                        ", " + ToCSharpString(carriedItemName) +
                        ", DimensionTileAction." + trigger.Action +
                        ", " + ToCSharpString(actionTarget) +
                        ", " + trigger.Amount.ToString(CultureInfo.InvariantCulture) +
                        ", " + trigger.ConditionSeconds.ToString(CultureInfo.InvariantCulture) + "f" +
                        ", " + trigger.CooldownSeconds.ToString(CultureInfo.InvariantCulture) + "f" +
                        ", " + (trigger.OnceOnly ? "true" : "false") + "));");
                }

                builder.AppendLine("    {");
                builder.AppendLine("      var sceneTiles = new System.Collections.Generic.List<DimensionSceneTileRequest>();");
                for (int t = 0; t < tileLines.Count; t++)
                {
                    builder.AppendLine(tileLines[t]);
                }

                builder.AppendLine("      var sceneObjects = new System.Collections.Generic.List<DimensionSceneObject>();");
                for (int o = 0; o < objectLines.Count; o++)
                {
                    builder.AppendLine(objectLines[o]);
                }

                builder.AppendLine("      var sceneTriggers = new System.Collections.Generic.List<DimensionSceneTrigger>();");
                for (int g = 0; g < triggerLines.Count; g++)
                {
                    builder.AppendLine(triggerLines[g]);
                }

                builder.Append("      var sceneResult = DimensionSceneTileCompiler.Compile(")
                    .Append(ToCSharpString(sceneName))
                    .AppendLine(", sceneTiles);");
                builder.AppendLine("      for (int i = 0; i < sceneResult.Skipped.Count; i++)");
                builder.AppendLine("      {");
                builder.AppendLine("        DimensionConsumerLog.Problem(DimensionId, sceneResult.Skipped[i]);");
                builder.AppendLine("      }");
                builder.AppendLine("      if (sceneResult.Tiles.Count > 0)");
                builder.AppendLine("      {");
                // The explicit pivot is the whole alignment story: the game stamps a scene as
                // anchor + (tile − centre), corridors aim at room CENTRES, and their band is
                // always odd and centred on that line. Without this argument the pivot defaults
                // to (0,0) — the scene's corner — and every dungeon room built from the scene
                // reads as shoved half its size off its corridors.
                builder.Append("        DimensionCustomSceneRegistry.Register(new DimensionCustomSceneDefinition(")
                    .Append(ToCSharpString(sceneName))
                    .Append(", sceneResult.Tiles, centerPosition: DimensionSceneGeometry.CentreOf(sceneResult.Tiles)")
                    .Append(", objects: sceneObjects")
                    .Append(", triggers: sceneTriggers")
                    .Append(BuildOverworldSpawnArguments(scene))
                    .AppendLine("));");

                // The placement policy rides beside the tile data. Without this line the Studio's
                // placement page — mode, radial band, weight, unique, required — was authored and
                // then thrown away: nothing at runtime ever read it.
                AppendScenePoolRegistration(builder, template, scene, sceneName, owningBiomes);

                builder.AppendLine("      }");
                builder.AppendLine("    }");
            }
        }

        private static bool SceneAlreadyPlaces(SceneTemplateAsset scene, string objectId)
        {
            DimensionSceneObjectTemplate[] placed = scene.SceneObjects;
            for (int i = 0; i < placed.Length; i++)
            {
                if (placed[i] != null && placed[i].Enabled &&
                    string.Equals(placed[i].ObjectId, objectId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The middle of a scene's painted ground — the same floor((min+max)/2) the scene's own
        /// pivot uses, so a boss lands exactly where the room centres itself.
        /// </summary>
        private static Vector2Int ResolveSceneCentre(SceneTemplateAsset scene)
        {
            DimensionSceneTileTemplate[] tiles = scene.Tiles;
            bool any = false;
            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null || !tiles[i].Enabled)
                {
                    continue;
                }

                Vector2Int p = tiles[i].LocalPosition;
                if (!any)
                {
                    minX = maxX = p.x;
                    minY = maxY = p.y;
                    any = true;
                    continue;
                }

                if (p.x < minX) { minX = p.x; }
                if (p.x > maxX) { maxX = p.x; }
                if (p.y < minY) { minY = p.y; }
                if (p.y > maxY) { maxY = p.y; }
            }

            if (!any)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int((minX + maxX) >> 1, (minY + maxY) >> 1);
        }

        /// <summary>
        /// The extra ctor arguments that let vanilla Overworld generation grow this scene, or an
        /// empty string when the scene has not opted in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Biome names resolve against the game's own <c>Biome</c> enum here, at build time,
        /// because the game's availability filter is an exact-match scan with no wildcard: a name
        /// that is not a vanilla biome would compile fine and then never match anything. That is
        /// also why custom biome ids are refused — the Overworld's sampler only ever produces
        /// vanilla values, so a custom id here would be dead weight dressed as configuration.
        /// </para>
        /// <para>
        /// A scene that opts in but resolves zero biomes keeps <c>maxOccurrences</c> 0, which is
        /// the game's own "invisible to natural spawn" — the safe state, loudly explained.
        /// </para>
        /// </remarks>
        private static string BuildOverworldSpawnArguments(SceneTemplateAsset scene)
        {
            if (!scene.SpawnInOverworld)
            {
                return string.Empty;
            }

            return BuildOverworldSpawnArguments(
                "Scene '" + scene.SceneId + "'",
                scene.OverworldBiomeNames,
                scene.OverworldMaxOccurrences,
                scene.MinDistanceFromCore);
        }

        /// <summary>
        /// The shared body of the above, so a portal that grows in the world takes exactly the same
        /// road a scene does — including the same refusal of biome names the Overworld never samples.
        /// </summary>
        private static string BuildOverworldSpawnArguments(
            string subject,
            string[] names,
            int maxOccurrences,
            int minDistanceFromCore)
        {
            List<string> resolved = new List<string>();
            for (int i = 0; names != null && i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                Biome biome;
                if (!System.Enum.TryParse(names[i], false, out biome) || biome == Biome.None)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] " + subject + " wants to grow in " +
                        "Overworld biome '" + names[i] + "', which is not a vanilla biome name. " +
                        "The entry is dropped — the Overworld only ever samples vanilla biomes, " +
                        "so it could never match.");
                    continue;
                }

                string literal = "Biome." + biome;
                if (!resolved.Contains(literal))
                {
                    resolved.Add(literal);
                }
            }

            if (resolved.Count == 0)
            {
                Debug.LogWarning(
                    "[ExpandNullforge] " + subject + " opted into Overworld " +
                    "spawning but names no valid vanilla biome, so it will not grow naturally.");
                return string.Empty;
            }

            StringBuilder args = new StringBuilder();
            args.Append(", maxOccurrences: ")
                .Append((maxOccurrences < 1 ? 1 : maxOccurrences).ToString(CultureInfo.InvariantCulture));
            args.Append(", overworldBiomes: new Biome[] { ");
            for (int i = 0; i < resolved.Count; i++)
            {
                if (i > 0)
                {
                    args.Append(", ");
                }

                args.Append(resolved[i]);
            }

            args.Append(" }");
            args.Append(", minDistanceFromCoreInClassicWorlds: ")
                .Append((minDistanceFromCore < 0 ? 0 : minDistanceFromCore)
                    .ToString(CultureInfo.InvariantCulture));
            return args.ToString();
        }

        /// <summary>
        /// Emits the scene's placement policy into the runtime pool, so the placement pass can
        /// honor what the Studio authored: mode, radial band, biome filters, weight, unique,
        /// required. Uses the raw scene id so the entry lines up with the compiled scene record.
        /// </summary>
        private static void AppendScenePoolRegistration(
            StringBuilder builder,
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            string sceneName,
            Dictionary<SceneTemplateAsset, List<string>> owningBiomes)
        {
            List<string> owners;
            owningBiomes.TryGetValue(scene, out owners);
            string owningBiome = owners != null && owners.Count > 0 ? owners[0] : string.Empty;

            // Extra owners plus the authored filter merge into one allow list; the pass treats
            // any match as permission.
            List<string> allowed = new List<string>();
            if (owners != null)
            {
                for (int i = 1; i < owners.Count; i++)
                {
                    if (!allowed.Contains(owners[i]))
                    {
                        allowed.Add(owners[i]);
                    }
                }
            }

            string[] authoredAllowed = scene.AllowedBiomeIds;
            for (int i = 0; i < authoredAllowed.Length; i++)
            {
                if (!string.IsNullOrEmpty(authoredAllowed[i]) && !allowed.Contains(authoredAllowed[i]))
                {
                    allowed.Add(authoredAllowed[i]);
                }
            }

            StringBuilder allowedList = new StringBuilder("new string[] { ");
            for (int i = 0; i < allowed.Count; i++)
            {
                if (i > 0)
                {
                    allowedList.Append(", ");
                }

                allowedList.Append(ToCSharpString(allowed[i]));
            }

            allowedList.Append(" }");

            DimensionBounds preferred = scene.PreferredLocalBounds;
            bool hasPreferred = scene.PlacementMode == DimensionScenePlacementMode.PreferredBounds;

            builder.Append("        DimensionScenePoolRegistry.Register(")
                .Append(ToCSharpString(template.DimensionId))
                .AppendLine(", new DimensionScenePoolEntry(");
            builder.Append("            ").Append(ToCSharpString(sceneName)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(scene.SceneId)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(owningBiome)).AppendLine(",");
            builder.Append("            ").Append(allowedList.ToString()).AppendLine(",");
            builder.Append("            DimensionScenePlacementMode.")
                .Append(scene.PlacementMode.ToString()).AppendLine(",");
            builder.Append("            new int2(")
                .Append(scene.ExactLocalPosition.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(scene.ExactLocalPosition.y.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
            builder.Append("            new DimensionBounds(new int2(")
                .Append(preferred.Min.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(preferred.Min.y.ToString(CultureInfo.InvariantCulture)).Append("), new int2(")
                .Append(preferred.MaxExclusive.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(preferred.MaxExclusive.y.ToString(CultureInfo.InvariantCulture)).AppendLine(")),");
            builder.Append("            ").Append(hasPreferred ? "true" : "false").AppendLine(",");
            builder.Append("            ")
                .Append(scene.MinRadiusTiles.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ")
                .Append(scene.MaxRadiusTiles.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ").Append(ToCSharpString(scene.RadialBiomeId)).AppendLine(",");
            builder.Append("            new int2(")
                .Append(scene.FootprintSize.x.ToString(CultureInfo.InvariantCulture)).Append(", ")
                .Append(scene.FootprintSize.y.ToString(CultureInfo.InvariantCulture)).AppendLine("),");
            builder.Append("            ")
                .Append(scene.Weight.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("            ").Append(scene.Unique ? "true" : "false").AppendLine(",");
            builder.Append("            ").Append(scene.Required ? "true" : "false").AppendLine(",");
            builder.Append("            ")
                .Append(scene.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine("));");
        }

        /// <summary>
        /// Which biomes carry each scene in their pool. The flattening in
        /// <see cref="CollectSceneTemplates"/> deliberately loses this — registration wants each
        /// scene once — but the placement policy needs to remember whose pool it came from.
        /// </summary>
        private static Dictionary<SceneTemplateAsset, List<string>> CollectSceneOwners(
            DimensionTemplateAsset template)
        {
            Dictionary<SceneTemplateAsset, List<string>> owners =
                new Dictionary<SceneTemplateAsset, List<string>>();
            if (template == null)
            {
                return owners;
            }

            BiomeTemplateAsset[] biomes = template.Biomes;
            if (biomes == null)
            {
                return owners;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                SceneTemplateAsset[] pool = biome.ScenePool;
                if (pool == null)
                {
                    continue;
                }

                for (int s = 0; s < pool.Length; s++)
                {
                    if (pool[s] == null)
                    {
                        continue;
                    }

                    List<string> list;
                    if (!owners.TryGetValue(pool[s], out list))
                    {
                        list = new List<string>();
                        owners[pool[s]] = list;
                    }

                    if (!list.Contains(biome.BiomeId))
                    {
                        list.Add(biome.BiomeId);
                    }
                }
            }

            return owners;
        }

        private static List<SceneTemplateAsset> CollectSceneTemplates(DimensionTemplateAsset template)
        {
            List<SceneTemplateAsset> scenes = new List<SceneTemplateAsset>();
            if (template == null)
            {
                return scenes;
            }

            AddScenes(template.GlobalScenes, scenes);

            BiomeTemplateAsset[] biomes = template.Biomes;
            if (biomes != null)
            {
                for (int i = 0; i < biomes.Length; i++)
                {
                    if (biomes[i] != null)
                    {
                        AddScenes(biomes[i].ScenePool, scenes);
                    }
                }
            }

            return scenes;
        }

        private static void AddScenes(SceneTemplateAsset[] source, List<SceneTemplateAsset> destination)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                // A scene shared between biomes must still be registered exactly once: the registry
                // refuses a duplicate name, so emitting it twice would log a warning about the mod's
                // own content.
                if (source[i] != null && !destination.Contains(source[i]))
                {
                    destination.Add(source[i]);
                }
            }
        }
    }
}
