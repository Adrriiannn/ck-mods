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
    /// What the emitted script registers about the world: layouts, zones, biomes, terrain and titles.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Emits every layout version the author published, so an existing save can be generated from
        /// the one that made it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each archived version is written out as its own set of zone registrations. That is more
        /// generated code than emitting only the current layout, but it is the only way pinning can be
        /// real: a world that says "I was made by v2" needs v2 to still exist inside the shipped mod,
        /// long after the author has rebuilt the layout out of different rings.
        /// </para>
        /// <para>
        /// The drift policy is emitted alongside, because it is the author's current intent and has to
        /// be able to change without invalidating anything already published.
        /// </para>
        /// </remarks>
        private static void AppendLayoutVersionsMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionTemplateAsset template)
        {
            builder.AppendLine("  private void RegisterLayoutVersions()");
            builder.AppendLine("  {");

            DimensionLayoutTemplateAsset layout = template == null ? null : template.LayoutTemplate;
            DimensionLayoutArchiveEntry[] published =
                layout == null ? new DimensionLayoutArchiveEntry[0] : layout.PublishedVersions;

            if (layout == null || published.Length == 0)
            {
                // Nothing published means nothing to pin to. Saves still work — they simply generate
                // from whatever layout is installed, which is the behaviour before this existed.
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            string dimensionId = ToCSharpString(portalOutput.DimensionId);

            builder.AppendLine("    DimensionLayoutDriftPolicyRegistry.Register(");
            builder.Append("        ").Append(dimensionId).AppendLine(",");
            builder.Append("        DimensionLayoutDriftPolicy.")
                .Append(layout.DriftPolicy.ToString()).AppendLine(");");
            builder.AppendLine();

            builder.AppendLine("    DimensionLayoutVersionRegistry.RegisterCurrent(");
            builder.Append("        ").Append(dimensionId).AppendLine(",");
            builder.Append("        ")
                .Append(layout.LayoutVersion.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(layout.CurrentFingerprint)).AppendLine(");");
            builder.AppendLine();

            for (int v = 0; v < published.Length; v++)
            {
                DimensionLayoutArchiveEntry entry = published[v];
                if (entry == null || entry.Regions.Length == 0)
                {
                    continue;
                }

                builder.AppendLine("    {");
                builder.AppendLine("      var zones = new System.Collections.Generic.List<DimensionZoneDefinition>();");

                for (int r = 0; r < entry.Regions.Length; r++)
                {
                    DimensionLayoutArchivedRegion region = entry.Regions[r];
                    if (region == null || string.IsNullOrEmpty(region.ZoneId))
                    {
                        continue;
                    }

                    builder.AppendLine("      zones.Add(new DimensionZoneDefinition(");
                    builder.Append("          ").Append(ToCSharpString(region.ZoneId)).AppendLine(",");
                    builder.Append("          ").Append(ToCSharpString(
                        string.IsNullOrEmpty(region.DisplayName) ? region.BiomeId : region.DisplayName))
                        .AppendLine(",");
                    builder.Append("          ").Append(dimensionId).AppendLine(",");
                    AppendBoundsConstructor(builder, region.LocalBounds, "          ");
                    builder.AppendLine(",");
                    builder.Append("          ").Append(ToCSharpString(region.BiomeId)).AppendLine(",");
                    builder.Append("          ")
                        .Append(region.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                    builder.AppendLine("          true));");
                }

                builder.AppendLine("      DimensionLayoutVersionRegistry.RegisterVersion(");
                builder.Append("          ").Append(dimensionId).AppendLine(",");
                builder.Append("          ")
                    .Append(entry.Version.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("          ").Append(ToCSharpString(entry.Fingerprint)).AppendLine(",");
                builder.AppendLine("          zones);");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendMinimumZonesMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionBounds tileMapBounds)
        {
            builder.AppendLine("  private bool EnsureMinimumZones(IDimensionService current)");
            builder.AppendLine("  {");
            IReadOnlyList<DimensionZoneDefinition> zones = portalOutput.MinimumZones;
            if (zones == null || zones.Count == 0)
            {
                builder.AppendLine("    return true;");
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                DimensionZoneDefinition zone = zones[i];
                if (string.IsNullOrEmpty(zone.ZoneId))
                {
                    continue;
                }

                builder.AppendLine("    if (!TryEnsureZone(");
                builder.AppendLine("        current,");
                builder.AppendLine("        new DimensionZoneDefinition(");
                builder.Append("            ").Append(ToCSharpString(zone.ZoneId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.DisplayName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.DimensionId)).AppendLine(",");
                AppendBoundsConstructor(builder, UnionBounds(zone.LocalBounds, tileMapBounds), "            ");
                builder.AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(zone.Kind)).AppendLine(",");
                builder.Append("            ").Append(zone.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("            ").Append(zone.Enabled ? "true" : "false").AppendLine(")))");
                builder.AppendLine("    {");
                builder.AppendLine("      return false;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendMinimumGenerationPassesMethod(
            StringBuilder builder,
            DimensionRuntimePortalOutput portalOutput,
            DimensionBounds tileMapBounds)
        {
            builder.AppendLine("  private bool EnsureMinimumGenerationPasses(IDimensionService current)");
            builder.AppendLine("  {");
            IReadOnlyList<DimensionGenerationPassDefinition> generationPasses =
                portalOutput.MinimumGenerationPasses;
            if (generationPasses == null || generationPasses.Count == 0)
            {
                builder.AppendLine("    return true;");
                builder.AppendLine("  }");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition generationPass = generationPasses[i];
                if (string.IsNullOrEmpty(generationPass.PassId))
                {
                    continue;
                }

                builder.AppendLine("    if (!TryEnsureGenerationPass(");
                builder.AppendLine("        current,");
                builder.AppendLine("        new DimensionGenerationPassDefinition(");
                builder.Append("            ").Append(ToCSharpString(generationPass.PassId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.DisplayName)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.DimensionId)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.ZoneId)).AppendLine(",");
                builder.Append("            ").Append(generationPass.HasLocalBounds ? "true" : "false").AppendLine(",");
                AppendBoundsConstructor(builder, UnionBounds(generationPass.LocalBounds, tileMapBounds), "            ");
                builder.AppendLine(",");
                builder.Append("            (DimensionGenerationPassPhase)")
                    .Append(((int)generationPass.Phase).ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.Append("            ").Append(generationPass.Priority.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("            ").Append(ToCSharpString(generationPass.ProviderId)).AppendLine(",");
                builder.Append("            ").Append(generationPass.Enabled ? "true" : "false").AppendLine(")))");
                builder.AppendLine("    {");
                builder.AppendLine("      return false;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine("    return true;");
            builder.AppendLine("  }");
            builder.AppendLine();
        }

        private static void AppendBoundsConstructor(
            StringBuilder builder,
            DimensionBounds bounds,
            string indent)
        {
            string safeIndent = indent ?? string.Empty;
            builder.Append(safeIndent).AppendLine("new DimensionBounds(");
            builder.Append(safeIndent)
                .Append("    new int2(")
                .Append(bounds.Min.x.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(bounds.Min.y.ToString(CultureInfo.InvariantCulture))
                .AppendLine("),");
            builder.Append(safeIndent)
                .Append("    new int2(")
                .Append(bounds.MaxExclusive.x.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(bounds.MaxExclusive.y.ToString(CultureInfo.InvariantCulture))
                .Append("))");
        }

        /// <summary>
        /// True when the placed portal (V1) should be craftable: an enabled user-accessible rule
        /// with Craftable set exists — or no placed rule was authored at all (legacy default).
        /// </summary>
        /// <summary>
        /// Makes every generated block item show up at the Wooden Workbench, alongside the portals.
        /// A block with no authored recipe still registers — it simply crafts from nothing, which is
        /// what lets a creator place and look at a brand-new block before designing its cost.
        /// Ingredients, once authored, ride the item's own InventoryItem authoring like vanilla.
        /// </summary>
        private static void AppendBlockCraftingRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            for (int i = 0; i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null || !tileset.Enabled || !tileset.GenerateWallBlock)
                {
                    continue;
                }

                string itemId = tileset.WallBlockItemId;
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                builder.AppendLine("    DimensionCraftingRegistry.Register(");
                builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                builder.Append("            ").Append(ToCSharpString(itemId)).AppendLine(",");
                builder.AppendLine("            ObjectID.WoodenWorkBench,");
                builder.AppendLine("            1,");
                builder.AppendLine("            0f,");
                builder.Append("            ").Append(ToCSharpString(tileset.BlockName + " Block")).AppendLine("));");
            }
        }

        /// <summary>
        /// Emits the title card each custom biome shows the first time a player walks into it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The tilesets a biome is recognised by are DERIVED, not authored a second time: a biome
        /// already names the floor and wall blocks it builds itself from, and each of those blocks
        /// belongs to a tileset. Asking the author to also list "which tilesets mean this biome" would
        /// be asking the same question twice, and the two answers would eventually disagree — at which
        /// point a title fires for a place the player is not standing in.
        /// </para>
        /// <para>
        /// A biome made entirely of vanilla blocks emits nothing. Its tilesets are Core Keeper's own,
        /// and claiming them would mean walking onto ordinary stone announced this mod's biome.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Emits each biome's ore list as a waiting gate registration.
        /// </summary>
        /// <remarks>
        /// The bootstrap knows which ores a biome names but not where the biome lies — the
        /// bounds only exist once the manifest's zones apply. So the emission registers the
        /// LIST, and the manifest-apply path marries it to each zone carrying the biome's id
        /// (<c>DimensionOreBiomeGate.BindZone</c>). Before this, the gate's one Register call
        /// sat on a method with no callers and every biome ore chip was decorative.
        /// </remarks>
        private static void AppendOreBiomeGateRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null)
            {
                return;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                System.Collections.Generic.IReadOnlyList<string> ores =
                    biome.OreObjectIds;
                if (ores == null || ores.Count == 0)
                {
                    continue;
                }

                builder.Append("    ExpandNullforge.Generation.DimensionOreBiomeGate.RegisterBiomeOres(")
                    .Append(ToCSharpString(template.DimensionId))
                    .Append(", ")
                    .Append(ToCSharpString(biome.BiomeId))
                    .Append(", new string[] { ");
                bool wroteOre = false;
                for (int o = 0; o < ores.Count; o++)
                {
                    if (string.IsNullOrEmpty(ores[o]))
                    {
                        continue;
                    }

                    string oreName = IsModOwnedObjectName(template, ores[o])
                        ? DimensionObjectNamespace.Qualify(modName, ores[o])
                        : ores[o];
                    if (wroteOre)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(ToCSharpString(oreName));
                    wroteOre = true;
                }

                builder.AppendLine(" });");
            }
        }

        /// <summary>
        /// Emits what each biome's ground and walls are made of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The other half of the same shape the ore gate uses: the bootstrap knows which block a
        /// biome names but not where the biome lies, so it registers the CHOICE and zone
        /// registration marries it to the geography. Before this the terrain provider carried one
        /// hardcoded tileset and every generated dimension came out a dirt platform, whatever the
        /// Biome page said.
        /// </para>
        /// <para>
        /// A biome whose Ground and Walls both name something unresolvable emits nothing at all,
        /// rather than a row of dirt. The rows are read last-one-wins, so a meaningless row would
        /// take a cell away from an overlapping biome that did resolve. The unresolved entry is
        /// reported by the compiler's <c>biome-terrain-block-unresolved</c> issue instead.
        /// </para>
        /// <para>
        /// A biome that resolves only one half gets that half and dirt for the other, so naming a
        /// floor and no wall gives the floor rather than nothing.
        /// </para>
        /// </remarks>
        private static void AppendTerrainMaterialRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null)
            {
                return;
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled || string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                int groundTileset;
                string groundNamed;
                bool groundHasGround;
                DimensionBiomeTerrainSource groundSource = DimensionBiomeTerrainMaterial.ResolveFirst(
                    biome.FloorObjectIds, tilesets, out groundTileset, out groundNamed, out groundHasGround);

                int wallTileset;
                string wallNamed;
                bool wallHasGround;
                DimensionBiomeTerrainSource wallSource = DimensionBiomeTerrainMaterial.ResolveFirst(
                    biome.WallObjectIds, tilesets, out wallTileset, out wallNamed, out wallHasGround);

                bool groundResolved = groundSource == DimensionBiomeTerrainSource.ModBlock ||
                                      groundSource == DimensionBiomeTerrainSource.VanillaBlock;
                bool wallResolved = wallSource == DimensionBiomeTerrainSource.ModBlock ||
                                    wallSource == DimensionBiomeTerrainSource.VanillaBlock;
                if (!groundResolved && !wallResolved)
                {
                    continue;
                }

                System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                builder
                    .Append("    ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.RegisterBiomeMaterial(")
                    .Append(ToCSharpString(template.DimensionId))
                    .Append(", ")
                    .Append(ToCSharpString(biome.BiomeId))
                    .Append(", ")
                    .Append((groundResolved
                        ? groundTileset
                        : ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.DefaultTileset)
                        .ToString(inv))
                    .Append(", ")
                    .Append((wallResolved
                        ? wallTileset
                        : ExpandNullforge.Generation.DimensionTerrainMaterialRegistry.DefaultTileset)
                        .ToString(inv))
                    .AppendLine(");");
            }
        }

        private static void AppendRegionTitleRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (biomes == null || tilesets == null)
            {
                return;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null || !biome.Enabled)
                {
                    continue;
                }

                List<int> tilesetIds = CollectBiomeTilesetIds(biome, tilesets);
                if (tilesetIds.Count == 0)
                {
                    continue;
                }

                AppendBiomeAtmosphereRegistration(builder, biome, tilesetIds);

                if (!biome.ShowTitleOnDiscovery)
                {
                    continue;
                }

                UnityEngine.Color color = biome.TitleColor;
                string iconName = string.IsNullOrEmpty(biome.TitleIconObjectId)
                    ? string.Empty
                    : (IsModOwnedObjectName(template, biome.TitleIconObjectId)
                        ? DimensionObjectNamespace.Qualify(modName, biome.TitleIconObjectId)
                        : biome.TitleIconObjectId);

                builder.AppendLine("    DimensionRegionTitleRegistry.Register(");
                builder.Append("        ").Append(ToCSharpString(biome.BiomeId)).AppendLine(",");

                // The localization term, not the text. The generator writes the biome's display name
                // into the mod's own CSV under this key, so a translated mod translates its titles too.
                builder.Append("        ")
                    .Append(ToCSharpString(DimensionBiomeTitleTerms.ForBiome(modName, biome.BiomeId)))
                    .AppendLine(",");

                builder.Append("        new UnityEngine.Color(")
                    .Append(color.r.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                    .Append(color.g.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                    .Append(color.b.ToString("R", CultureInfo.InvariantCulture)).Append("f, 1f),");
                builder.AppendLine();

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
                builder.Append("        ").Append(ToCSharpString(iconName)).AppendLine(");");
            }

            AppendNamedAreaRegistrations(builder, template, modName);
        }

        /// <summary>
        /// Emits each named area: one name fanned out to the title, ambience and music
        /// registries under a synthetic area id, carried by its signature blocks.
        /// </summary>
        /// <remarks>
        /// The game has no "area" object — the Meadow is three tile-counting systems agreeing.
        /// The synthetic "area:" id keeps a named area from ever colliding with a real biome's
        /// id in the shared registries, while the framework's own current-biome derivation
        /// (top tileset → registered id) makes standing among the area's blocks read as being
        /// IN the area, which is what fires its title and music.
        /// </remarks>
        private static void AppendNamedAreaRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionNamedAreaAsset[] areas = template == null ? null : template.NamedAreas;
            if (areas == null)
            {
                return;
            }

            for (int i = 0; i < areas.Length; i++)
            {
                DimensionNamedAreaAsset area = areas[i];
                if (area == null || !area.Enabled || string.IsNullOrEmpty(area.AreaId))
                {
                    continue;
                }

                List<int> tilesetIds = new List<int>();
                DimensionTilesetAsset[] blocks = area.Blocks;
                for (int b = 0; b < blocks.Length; b++)
                {
                    if (blocks[b] != null && blocks[b].Enabled && !tilesetIds.Contains(blocks[b].TilesetId))
                    {
                        tilesetIds.Add(blocks[b].TilesetId);
                    }
                }

                if (tilesetIds.Count == 0)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Named area '" + area.AreaId + "' names no blocks, so " +
                        "nothing could ever stand inside it. It was left out.");
                    continue;
                }

                string syntheticId = "area:" + area.AreaId;
                string tilesetLiteral = BuildIntArrayLiteral(tilesetIds);

                if (area.ShowTitleOnDiscovery)
                {
                    UnityEngine.Color color = area.TitleColor;
                    string iconName = string.IsNullOrEmpty(area.TitleIconObjectId)
                        ? string.Empty
                        : (IsModOwnedObjectName(template, area.TitleIconObjectId)
                            ? DimensionObjectNamespace.Qualify(modName, area.TitleIconObjectId)
                            : area.TitleIconObjectId);

                    builder.AppendLine("    DimensionRegionTitleRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(syntheticId)).AppendLine(",");
                    builder.Append("        ")
                        .Append(ToCSharpString(DimensionBiomeTitleTerms.ForBiome(modName, syntheticId)))
                        .AppendLine(",");
                    builder.Append("        new UnityEngine.Color(")
                        .Append(color.r.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                        .Append(color.g.ToString("R", CultureInfo.InvariantCulture)).Append("f, ")
                        .Append(color.b.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f, 1f),");
                    builder.Append("        ").Append(tilesetLiteral).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(iconName)).AppendLine(");");
                }

                if (!string.IsNullOrEmpty(area.AmbienceSoundKey) ||
                    !string.IsNullOrEmpty(area.MusicRosterName))
                {
                    builder.AppendLine("    DimensionBiomeAtmosphereRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(syntheticId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(area.AmbienceSoundKey)).AppendLine(",");
                    builder.Append("        ")
                        .Append(area.AmbienceVolume.ToString("R", CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("        ").Append(ToCSharpString(area.MusicRosterName)).AppendLine(",");
                    builder.Append("        ").Append(tilesetLiteral).AppendLine(");");
                }
            }
        }

        private static DimensionTilesetAsset FindTileset(
            DimensionTemplateAsset template,
            string tilesetName)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            for (int i = 0; tilesets != null && i < tilesets.Length; i++)
            {
                if (tilesets[i] != null &&
                    string.Equals(tilesets[i].TilesetName, tilesetName, System.StringComparison.Ordinal))
                {
                    return tilesets[i];
                }
            }

            return null;
        }

        private static BiomeTemplateAsset FindBiome(DimensionTemplateAsset template, string biomeId)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null || string.IsNullOrEmpty(biomeId))
            {
                return null;
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] != null &&
                    string.Equals(biomes[i].BiomeId, biomeId, StringComparison.Ordinal))
                {
                    return biomes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Emits what a biome sounds like: its ambience loop and which music playlist it plays.
        /// </summary>
        /// <remarks>
        /// Emitted even when a biome has neither, because the registration is also what claims the
        /// biome's tilesets — and a biome with no sound of its own still needs the game to know the
        /// player is standing in it.
        /// </remarks>
        private static void AppendBiomeAtmosphereRegistration(
            StringBuilder builder,
            BiomeTemplateAsset biome,
            List<int> tilesetIds)
        {
            builder.AppendLine("    DimensionBiomeAtmosphereRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(biome.BiomeId)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(biome.AmbienceSoundKey)).AppendLine(",");
            builder.Append("        ")
                .Append(biome.AmbienceVolume.ToString("R", CultureInfo.InvariantCulture))
                .AppendLine("f,");
            builder.Append("        ").Append(ToCSharpString(biome.MusicRosterName)).AppendLine(",");

            builder.Append("        new int[] { ");
            for (int t = 0; t < tilesetIds.Count; t++)
            {
                if (t > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(tilesetIds[t].ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine(" });");
        }

        /// <summary>
        /// The custom tilesets a biome's own floor and wall blocks belong to.
        /// </summary>
        /// <remarks>
        /// Matched on the generated block item ids rather than on the tileset name, because that is
        /// what a biome actually references — the author picks blocks, and the tileset is what those
        /// blocks are made of.
        /// </remarks>
        private static List<int> CollectBiomeTilesetIds(
            BiomeTemplateAsset biome,
            DimensionTilesetAsset[] tilesets)
        {
            List<int> ids = new List<int>();
            string[] floors = biome.FloorObjectIds;
            string[] walls = biome.WallObjectIds;

            for (int i = 0; i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null || !tileset.Enabled)
                {
                    continue;
                }

                if (!ContainsOrdinal(floors, tileset.GroundBlockItemId) &&
                    !ContainsOrdinal(floors, tileset.WallBlockItemId) &&
                    !ContainsOrdinal(walls, tileset.WallBlockItemId) &&
                    !ContainsOrdinal(walls, tileset.GroundBlockItemId))
                {
                    continue;
                }

                int id = tileset.TilesetId;
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static bool ContainsOrdinal(string[] values, string candidate)
        {
            if (values == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
