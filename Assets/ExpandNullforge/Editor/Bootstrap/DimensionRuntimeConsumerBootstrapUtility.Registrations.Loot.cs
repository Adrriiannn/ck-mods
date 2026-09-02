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
    /// What the emitted script registers about loot tables and drops.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Emits every authored loot table as a real registered table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The half that makes <c>DimensionLootTableAsset</c> more than paperwork: before this,
        /// a table's entries only mattered where a generator copied them onto a prefab, and any
        /// field that wanted the table BY NAME (a dungeon chest, a melody reward) found nothing.
        /// The runtime registry builds the table under its minted id at the game's own loot
        /// conversion seam; item names qualify here because only the editor knows which names
        /// the mod owns.
        /// </para>
        /// <para>
        /// Tables hang off several asset kinds (global list, mobs, elites, bosses, animals), so
        /// the walk deduplicates by table id — registering twice would replace, not stack, but
        /// the log noise would read as a bug.
        /// </para>
        /// </remarks>
        private static void AppendLootTableRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            List<DimensionLootTableAsset> tables = new List<DimensionLootTableAsset>();
            HashSet<string> seenIds = new HashSet<string>(System.StringComparer.Ordinal);

            void Collect(DimensionLootTableAsset table)
            {
                if (table != null && table.Enabled && !string.IsNullOrEmpty(table.LootTableId) &&
                    table.EnabledEntryCount > 0 && seenIds.Add(table.LootTableId))
                {
                    tables.Add(table);
                }
            }

            DimensionLootTableAsset[] globals = template.GlobalLootTables;
            for (int i = 0; globals != null && i < globals.Length; i++)
            {
                Collect(globals[i]);
            }

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                if (mobs[i] == null)
                {
                    continue;
                }

                Collect(mobs[i].LootTable);
                Collect(mobs[i].EliteVariant == null ? null : mobs[i].EliteVariant.LootTable);
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                Collect(bosses[i] == null ? null : bosses[i].LootTable);
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                Collect(animals[i] == null ? null : animals[i].LootTable);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                DimensionLootTableAsset table = tables[i];

                // A vanilla name would resolve to the vanilla table everywhere the resolver
                // runs, so registering a custom table under it could never be reached — say so
                // instead of emitting a dead registration.
                LootTableID vanilla;
                if (System.Enum.TryParse(table.LootTableId, false, out vanilla))
                {
                    Debug.LogWarning(
                        "[Dimensions API] Loot table '" + table.LootTableId + "' shares its " +
                        "name with one of the game's own tables, so the game's is the one " +
                        "everything will roll. Rename yours to make it reachable.");
                    continue;
                }

                builder.Append("    DimensionLootTableRegistry.Register(")
                    .Append(ToCSharpString(table.LootTableId))
                    .Append(", ")
                    .Append(table.AllowEmptyRoll ? "true" : "false")
                    .AppendLine(", new DimensionLootTableRegistry.Entry[] {");

                DimensionLootEntryTemplate[] entries = table.Entries;
                bool wroteEntry = false;
                for (int e = 0; e < entries.Length; e++)
                {
                    DimensionLootEntryTemplate entry = entries[e];
                    if (entry == null || !entry.Enabled || string.IsNullOrEmpty(entry.ItemId))
                    {
                        continue;
                    }

                    string itemName = IsModOwnedObjectName(template, entry.ItemId)
                        ? DimensionObjectNamespace.Qualify(modName, entry.ItemId)
                        : entry.ItemId;

                    // The chance decides the odds, and is made true when the table is built. That
                    // leaves the share nothing to divide, so the share is not drawn. A table
                    // authored while it still was carries whatever was typed into it, and a number
                    // that reaches nothing is exactly the thing this framework refuses
                    // to leave unsaid.
                    if (entry.Weight != 1)
                    {
                        Debug.LogWarning(
                            "[Dimensions API] Loot table '" + table.LootTableId + "' gives '" +
                            entry.ItemId + "' a share of " + entry.Weight + ". Shares are no " +
                            "longer how the odds are decided — the drop chance is, and it now " +
                            "means exactly what it says. This row drops " +
                            (entry.DropChance * 100f).ToString("0.##", CultureInfo.InvariantCulture) +
                            "% of the time. Set its drop chance if that is not what you wanted.");
                    }

                    if (wroteEntry)
                    {
                        builder.AppendLine(",");
                    }

                    builder.Append("        new DimensionLootTableRegistry.Entry { ItemObjectName = ")
                        .Append(ToCSharpString(itemName))
                        .Append(", Weight = ")
                        .Append(((float)entry.Weight).ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, DropChance = ")
                        .Append(entry.DropChance.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, MinAmount = ")
                        .Append(entry.MinAmount.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxAmount = ")
                        .Append(entry.MaxAmount.ToString(CultureInfo.InvariantCulture))
                        .Append(" }");
                    wroteEntry = true;
                }

                builder.AppendLine();
                builder.AppendLine("    });");
            }
        }

        /// <summary>
        /// Emits the drops that could not be written onto a prefab as custom loot.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The far end of the drop-location inversion. An item saying "2 to 5 of me drop off slimes,
        /// but only in the desert" cannot become per-object custom loot — <c>LootDrop</c> has a single
        /// <c>amount</c> and no biome field — so it has to be registered against the source loot table
        /// at load instead.
        /// </para>
        /// <para>
        /// The plan is rebuilt here rather than carried over from prefab generation, because the two
        /// run as separate passes and a value threaded between them is a value that can go stale.
        /// Rebuilding is cheap and cannot disagree with itself.
        /// </para>
        /// <para>
        /// Chance is expressed as a percentage because that is what the registry takes; a drop the
        /// author marked as always-dropping is sent as 100, which is what the registry reads as
        /// guaranteed.
        /// </para>
        /// <para>
        /// Internal rather than private so a test can drive the one capability this whole wave
        /// exists for — a mod's creature dropping a mod's item — instead of grepping for the call.
        /// </para>
        /// </remarks>
        internal static void AppendAuthoredDropRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionDropPlan plan = DimensionDropPlan.Build(
                template.GlobalItems,
                template.GlobalWorldObjects);
            if (plan.IsEmpty)
            {
                return;
            }

            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;

            for (int s = 0; s < plan.Sources.Count; s++)
            {
                DimensionDropsForSource source = plan.Sources[s];
                for (int d = 0; d < source.Drops.Count; d++)
                {
                    DimensionResolvedDrop drop = source.Drops[d];
                    DimensionDropSource settings = drop.Source;

                    // Anything written onto the prefab is skipped here; writing it twice would give
                    // the player two of everything. The question is asked through the one shared
                    // answer, because a drop the prefab writer declined and this one skipped is a
                    // drop that happens nowhere.
                    if (DimensionDropEmitter.GoesOnTheObjectItself(source, drop) ||
                        settings.DropsNothing)
                    {
                        continue;
                    }

                    float chancePercent = settings.AlwaysDrops ? 100f : settings.Chance * 100f;

                    // Mod-own names must ship QUALIFIED, like every other emission — this was
                    // the one emitter that never qualified, so colon-less ids resolved to nothing
                    // at load and the drop silently vanished.
                    string sourceName = IsModOwnedObjectName(template, source.SourceId)
                        ? DimensionObjectNamespace.Qualify(modName, source.SourceId)
                        : source.SourceId;
                    string itemName = IsModOwnedObjectName(template, drop.ItemId)
                        ? DimensionObjectNamespace.Qualify(modName, drop.ItemId)
                        : drop.ItemId;

                    // THE TABLE TRAVELS WITH THE ROW. At load the registry cannot read a source's
                    // loot table off its prefab — it runs inside database conversion, where no
                    // entity world can answer — so a source of this mod's own ships the name of
                    // the table the generator stamped it with. Empty for one of the game's, which
                    // the registry reads off the game's own authoring prefab instead.
                    string sourceTable = SourceLootTableNameOf(template, modName, source.SourceId);

                    builder.AppendLine("    DimensionPortalDropRegistry.Register(");
                    builder.Append("        ").Append(ToCSharpString(sourceName)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(itemName)).AppendLine(",");
                    builder.Append("        ").Append(((float)settings.Weight).ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(chancePercent.ToString(inv)).AppendLine("f,");
                    builder.Append("        ").Append(settings.MinAmount.ToString(inv)).AppendLine(",");
                    builder.Append("        ").Append(settings.MaxAmount.ToString(inv)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(settings.OnlyInBiomeId)).AppendLine(",");
                    builder.Append("        ").Append(ToCSharpString(sourceTable)).AppendLine(");");
                }
            }
        }

        /// <summary>
        /// Warns when a scene tile uses a block whose tileset generates no object for that role.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This catches a bug that is invisible everywhere except inside a dungeon. A directly-placed
        /// scene writes its tiles straight into the map, so any tileset works. A scene embedded in a
        /// generated dungeon room does NOT: <c>DungeonGenerateRoomsSystem</c> resolves each
        /// (tileset, tileType) to an ObjectID through the object database and <b>drops the tile</b>
        /// when nothing matches.
        /// </para>
        /// <para>
        /// The only thing that registers a (tileset, tileType) pair is an object that IS that tile —
        /// the generated block. So a tileset used purely as scene decoration, with both block toggles
        /// off, has no entry, and every tile of it silently vanishes from dungeon rooms while looking
        /// perfectly fine in the open world. Someone hitting that would reasonably conclude their
        /// dungeon was broken, not their block.
        /// </para>
        /// <para>
        /// Warned at generation rather than blocked: the scene is still valid outside dungeons, and
        /// refusing to generate would be worse than telling the author what they will see.
        /// </para>
        /// </remarks>
        private static void WarnIfTilesetHasNoBlockObject(
            DimensionTemplateAsset template,
            SceneTemplateAsset scene,
            DimensionSceneTileTemplate tile)
        {
            DimensionTilesetAsset[] tilesets = template == null ? null : template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            DimensionTilesetAsset match = null;
            for (int i = 0; i < tilesets.Length; i++)
            {
                if (tilesets[i] != null &&
                    string.Equals(tilesets[i].TilesetName, tile.BlockId, System.StringComparison.Ordinal))
                {
                    match = tilesets[i];
                    break;
                }
            }

            if (match == null)
            {
                // A vanilla tileset, or a block from another mod. Vanilla always has its block
                // objects, and another mod's content is not ours to vet.
                return;
            }

            bool needsGround = tile.Role == DimensionTileRole.Ground;
            bool needsWall = tile.Role == DimensionTileRole.Wall;
            if ((needsGround && match.GenerateGroundBlock) || (needsWall && match.GenerateWallBlock) ||
                (!needsGround && !needsWall))
            {
                return;
            }

            Debug.LogWarning(
                "[ExpandNullforge] Scene '" + scene.SceneId + "' places '" + tile.BlockId + "' as " +
                tile.Role + " at " + tile.LocalPosition + ", but that block does not generate a " +
                (needsGround ? "ground" : "wall") + " object. The tile will appear in the open world " +
                "and be silently dropped from any dungeon room this scene is embedded in. Turn on the " +
                "matching block in the Tileset Studio, or accept that this scene is not dungeon-safe.");
        }

        /// <summary>Every scene a template can reach, global and per-biome, without duplicates.</summary>
        /// <summary>
        /// Whether an object a scene places is one this mod defines, rather than one of the game's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Scenes are the one place both kinds of name meet. A scene's tiles are always the mod's own
        /// blocks, so those are namespaced unconditionally; its objects are mostly vanilla — a chest,
        /// a torch, a statue — with the occasional item the mod itself defines. Namespacing everything
        /// would rename <c>Chest</c> into something the game has never heard of; namespacing nothing
        /// would let a mod's own item collide with another mod's item of the same name.
        /// </para>
        /// <para>
        /// So the mod's own declared content is the authority: if the name is something this template
        /// generates, it is qualified; otherwise it is passed through untouched for the database to
        /// resolve at injection time. An unknown name is a warning there, not a silent nothing.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Whether a name is one of the mod's own objects, asked of the one walk both sides share.
        /// </summary>
        /// <remarks>
        /// A hand-written list of the asset kinds that come to mind — items, workbenches, tileset
        /// blocks and creatures — leaves out containers, world objects,
        /// vehicles, projectiles, plants and explosions. A drop from one of the mod's own chests
        /// then ships its source name unqualified, resolves to nothing at load, and the item
        /// drops from nowhere. <c>DimensionGeneratedObjectIds.Collect</c> is the walk the binder
        /// and the link emitter already agree on, so asking it here means one list rather than two.
        /// </remarks>
        /// <summary>
        /// Says so when one of the mod's own things is called the same as one of the game's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE WARNING THAT WAS PROMISED AND NEVER WRITTEN. <c>DimensionNamingContext.Owns</c>'s
        /// own remark says "an id that shadows a game object is reported at generate time", and
        /// nothing anywhere reported it. It matters because the two halves of the framework answer
        /// differently for such a name: the generators stamp the object as the mod's own, while
        /// every reference to it — a drop's source, a recipe ingredient, a summoning item's boss —
        /// asks <c>Owns</c>, which puts the game's names first and answers no.
        /// </para>
        /// <para>
        /// What that does to somebody: they call their boss <c>Larva</c> and tick an item as
        /// dropping from it. Generation is clean. In the game, every wild larva in the world drops
        /// their item and their own boss drops nothing. The same split reaches recipe ingredients
        /// and the list of bosses a summoning item can call.
        /// </para>
        /// <para>
        /// It is said rather than fixed by renaming, because the name is the author's to choose and
        /// silently changing it would break every reference they have already written by hand.
        /// </para>
        /// </remarks>
        private static void SayWhenOneOfOursSharesAGameObjectsName(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            List<string> ours = DimensionGeneratedObjectIds.Collect(template);
            for (int i = 0; i < ours.Count; i++)
            {
                string id = ours[i];
                if (string.IsNullOrEmpty(id) || DimensionObjectBinder.Vanilla(id) == ObjectID.None)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[Dimensions API] One of your own things is called '" + id + "', which is also " +
                    "the name of something the game already has. Anything that points at that " +
                    "name — a drop, a recipe ingredient, a summoning item's boss — will reach the " +
                    "game's one and not yours, and yours will never be reached at all. Rename it.");
            }
        }

        private static bool IsModOwnedObjectName(DimensionTemplateAsset template, string objectId)
        {
            if (template == null || string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            // ONE OWNERSHIP ANSWER, THE SAME ONE THE GENERATORS BAKE FROM. The walk above plus five
            // hand-written ones underneath it, with neither half asking the ObjectID
            // enum first, has a mod item called Torch baked by every generator as the GAME's
            // torch (DimensionObjectBinder.Vanilla wins there) while this says "ours" and ships
            // "MyMod:Torch" in the drop row and the recipe output — the two halves of the same
            // authored thing pointing at different objects. DimensionNamingContext.Owns is the
            // predicate the generators use, vanilla-first and all; asking it here is what makes
            // "both sides call this" true rather than aspirational.
            //
            // The hand-written walks also counted switched-off assets as ours. A switched-off asset
            // generates no object, so a name qualified against it resolves to nothing at load —
            // which is the silent loss Collect's own remark says it exists to prevent.
            return new DimensionNamingContext(
                    string.Empty,
                    DimensionGeneratedObjectIds.Collect(template))
                .Owns(objectId);
        }

        /// <summary>
        /// The loot table a drop registered against this source will land in, or empty when the
        /// source is one of the game's own and the game has to be asked.
        /// </summary>
        /// <remarks>
        /// The load-time injection cannot read a source's loot table off its prefab entity — it
        /// runs inside database conversion, when no entity world can answer. So the answer travels
        /// with the row instead, computed by the same
        /// <see cref="DimensionDropEmitter.LootTableNameFor"/> the generator stamps the prefab from.
        /// </remarks>
        private static string SourceLootTableNameOf(
            DimensionTemplateAsset template,
            string modName,
            string sourceId)
        {
            if (!IsModOwnedObjectName(template, sourceId))
            {
                return string.Empty;
            }

            return DimensionDropEmitter.LootTableNameFor(
                AuthoredLootTableOf(template, sourceId),
                DimensionObjectNamespace.Qualify(modName, sourceId));
        }

        /// <summary>
        /// The loot table asset a creature of this mod's own was pointed at, or null for anything
        /// with no such field (a container, a world object, a critter).
        /// </summary>
        private static DimensionLootTableAsset AuthoredLootTableOf(
            DimensionTemplateAsset template,
            string sourceId)
        {
            if (template == null || string.IsNullOrEmpty(sourceId))
            {
                return null;
            }

            string local = DimensionObjectNamespace.LocalIdOf(sourceId);

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                if (mobs[i] == null)
                {
                    continue;
                }

                if (string.Equals(mobs[i].MobId, local, StringComparison.Ordinal))
                {
                    return mobs[i].LootTable;
                }

                // An elite is generated as a second creature under "<mobId>.elite", sharing the
                // base mob's loot unless it was given its own.
                if (string.Equals(mobs[i].MobId + ".elite", local, StringComparison.Ordinal))
                {
                    DimensionEliteVariantTemplate elite = mobs[i].EliteVariant;
                    return elite != null && elite.LootTable != null
                        ? elite.LootTable
                        : mobs[i].LootTable;
                }
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                if (bosses[i] != null &&
                    string.Equals(bosses[i].BossId, local, StringComparison.Ordinal))
                {
                    return bosses[i].LootTable;
                }
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                if (animals[i] != null &&
                    string.Equals(animals[i].AnimalId, local, StringComparison.Ordinal))
                {
                    return animals[i].LootTable;
                }
            }

            return null;
        }
    }
}
