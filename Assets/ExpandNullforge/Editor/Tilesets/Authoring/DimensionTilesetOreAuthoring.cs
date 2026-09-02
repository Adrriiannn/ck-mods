using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Makes a mod's own item double as the ore vein found inside a custom block's walls, which is
    /// exactly how Core Keeper does it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE HEADLINE, VERIFIED IN THE DECOMPILE: <b>the vein object and the ore item are the same
    /// ObjectID</b>. <c>CopperOreEntity.prefab</c> IS <c>ObjectID.CopperOre</c> (1500). There is no
    /// hidden vein object and no loot table anywhere in the chain. A vanilla vein prefab carries
    /// only ObjectInfo + <c>TileAuthoring(tileset, tileType = ore)</c> + tile SFX, at
    /// <c>objectType = NonUsable</c>.
    /// </para>
    /// <para>
    /// HOW THE DROP HAPPENS. Mining the wall kills the cell's tiles; ore counts as
    /// <c>IsContainedResource</c>, which takes <c>DropTilesJob</c> down a branch that bypasses
    /// <c>DontDropLootCD</c>/<c>DontDropSelfCD</c> entirely. It then calls
    /// <c>GetObjectData(tileset, ore)</c> — a first-match linear scan over the database's object
    /// infos — and drops whatever ObjectID that resolves to (+1 at 15%, plus mining fortune). So the
    /// only thing needed for "this block contains my ore" is one object info whose tileset is ours
    /// and whose tileType is ore. That is what this stamp creates.
    /// </para>
    /// <para>
    /// WHY THE ITEM'S OWN PREFAB. <c>ObjectAuthoring</c> resolves
    /// <c>objectID = API.Authoring.GetObjectID(objectName)</c> — a lookup, not a mint — so stamping
    /// the item's own prefab keeps one object that is both the inventory item and the vein, matching
    /// vanilla exactly and adding no new ObjectID for the duplicate-name check in
    /// <c>DefaultConvertSystem</c> to trip over.
    /// </para>
    /// <para>
    /// ONE ORE PER (TILESET, ORE) — A VANILLA LIMIT, NOT OURS. The scan is first-match, so a tileset
    /// can only yield one ore. Vanilla has this collision itself: SolariteOre and PandoriumOre are
    /// both registered on Crystal, and the first one found wins. The generator therefore binds each
    /// custom ore item to a single tileset and reports the rest rather than emitting entries that
    /// would silently never be reached.
    /// </para>
    /// <para>
    /// STILL UNPROVEN IN A SESSION: that a mined custom wall actually yields the item. Every step is
    /// verified against the decompiled drop path, but nothing here has been seen running.
    /// </para>
    /// </remarks>
    internal static class DimensionTilesetOreAuthoring
    {
        /// <summary>
        /// Stamps <paramref name="root"/> — the generated prefab for a mod's own item — so it is
        /// also the ore vein inside <paramref name="tileset"/>'s walls.
        /// </summary>
        public static void Apply(
            GameObject root,
            DimensionTilesetAsset tileset,
            DimensionItemGenerationReport report)
        {
            if (root == null || tileset == null)
            {
                return;
            }

            int tilesetId = tileset.TilesetId;
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId))
            {
                report.Warnings.Add(
                    "Tileset '" + tileset.TilesetName + "' resolved to id " + tilesetId +
                    ", which is not in the custom range; its ore vein was not stamped.");
                return;
            }

            // The whole mechanism, in two fields: the game's ObjectAuthoring→ObjectInfo conversion
            // copies these into ObjectInfo.tileset/tileType, and GetObjectData(tileset, ore) finds
            // them there.
            TileAuthoring tile = DimensionGeneratedPrefabUtility.EnsureComponent<TileAuthoring>(root);
            tile.tileset = (Tileset)tilesetId;
            tile.tileType = TileType.ore;

            // Ore needs a wall beneath it (TileType.GetNeededTile), which the block's own wall
            // supplies — nothing to add here for that.

            // The placed vein is chunk data; the entity that represents it is transient.
            DimensionGeneratedPrefabUtility.EnsureComponent<DontSerializeAuthoring>(root);

            // Same gate as the block's, same consequence. The game wraps every tile sound and every
            // puff — its own defaults included — in a check for this component, so a vein without
            // it makes no noise when a pickaxe lands on it and throws nothing when it breaks.
            // CopperOreEntity and TinOreEntity both carry it.
            DimensionGeneratedPrefabUtility.EnsureComponent<TileEffectAuthoring>(root);
        }

        /// <summary>
        /// Writes the standalone vein object that makes a <b>vanilla</b> ore appear in a custom
        /// block's walls, and returns its prefab path (null when nothing was written).
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY A SEPARATE OBJECT HERE. A custom ore can be stamped onto the item's own prefab, but
        /// we do not own <c>CopperOre</c>'s prefab. Instead this emits an object whose
        /// <c>objectName</c> is the bare vanilla name, which adds a tile variation to the EXISTING
        /// ObjectID rather than creating anything new: <c>ObjectAuthoring</c> resolves
        /// <c>objectID = API.Authoring.GetObjectID(objectName)</c> — a lookup, not a mint — so
        /// "CopperOre" binds to 1500.
        /// </para>
        /// <para>
        /// THE NAME MUST NOT BE NAMESPACED. Everything else this framework generates is qualified
        /// <c>{mod}:{id}</c> so two mods can coexist. This one object is the deliberate exception:
        /// qualifying it would mint a brand-new ObjectID named <c>MyMod:CopperOre</c> that no
        /// vanilla recipe, drop or stack knows about, and the vein would yield a look-alike item
        /// that is useless. It is safe precisely because it does NOT create an ObjectID —
        /// <c>DefaultConvertSystem</c>'s duplicate-name check fires when two DIFFERENT ObjectIDs
        /// share a name, which cannot happen when the name resolves to an existing one.
        /// </para>
        /// <para>
        /// <c>objectType</c> is set through <c>SerializedProperty</c> by member name rather than by
        /// referencing the enum, matching how the item generator sets its enums: the value survives
        /// a Core Keeper update that renumbers <c>ObjectType</c>, and a rename fails loudly here
        /// instead of silently writing the wrong constant.
        /// </para>
        /// </remarks>
        public static string CreateVanillaVeinPrefab(
            string outputFolder,
            DimensionTilesetAsset tileset,
            string vanillaOreName,
            DimensionItemGenerationReport report)
        {
            if (tileset == null || string.IsNullOrEmpty(vanillaOreName) ||
                string.IsNullOrEmpty(outputFolder))
            {
                return null;
            }

            int tilesetId = tileset.TilesetId;
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId))
            {
                report.Warnings.Add(
                    "Tileset '" + tileset.TilesetName + "' resolved to id " + tilesetId +
                    ", which is not in the custom range; its '" + vanillaOreName +
                    "' vein was not generated.");
                return null;
            }

            string fileName = DimensionGeneratedPrefabUtility.SanitizeFileName(tileset.TilesetName) + "." + DimensionGeneratedPrefabUtility.SanitizeFileName(vanillaOreName) + ".vein";
            string prefabPath = outputFolder + "/" + fileName + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;
            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(fileName);

            try
            {
                ObjectAuthoring authoring = DimensionGeneratedPrefabUtility.EnsureComponent<ObjectAuthoring>(root);
                authoring.objectName = vanillaOreName;   // deliberately unqualified — see remarks
                authoring.initialAmount = 1;
                authoring.additionalSprites = new List<Sprite>();

                if (!DimensionGeneratedPrefabUtility.TrySetEnumMember(authoring, "objectType", "NonUsable"))
                {
                    report.Warnings.Add(
                        "Could not set objectType=NonUsable on the '" + vanillaOreName +
                        "' vein for '" + tileset.TilesetName +
                        "'. Core Keeper may have renamed the enum member; the vein was still written.");
                }

                TileAuthoring tile = DimensionGeneratedPrefabUtility.EnsureComponent<TileAuthoring>(root);
                tile.tileset = (Tileset)tilesetId;
                tile.tileType = TileType.ore;

                DimensionGeneratedPrefabUtility.EnsureComponent<DontSerializeAuthoring>(root);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }

                return prefabPath;
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    "The '" + vanillaOreName + "' vein for '" + tileset.TilesetName +
                    "' failed to generate: " + exception.Message);
                return null;
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>
        /// Undoes <see cref="Apply"/> on an item that is no longer any tileset's ore.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Generation updates prefabs in place, so a stamp an earlier run applied survives until
        /// something removes it. Without this, taking an ore off a block's list would leave the item
        /// still carrying <c>TileAuthoring{tileType = ore}</c> — it would go on generating as a vein
        /// in walls of a tileset that no longer claims it, and no dashboard action could stop it.
        /// </para>
        /// <para>
        /// It removes the component ONLY when the tile type is <c>ore</c>. A block item legitimately
        /// carries <c>TileAuthoring</c> for ground or wall, and stripping that would turn a working
        /// block into an inert item. <c>DontSerializeAuthoring</c> is deliberately left alone for the
        /// same reason — blocks need it too, and it is inert on an item that has neither role.
        /// </para>
        /// </remarks>
        public static void ClearIfOre(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            TileAuthoring tile = root.GetComponent<TileAuthoring>();
            if (tile != null && tile.tileType == TileType.ore)
            {
                Object.DestroyImmediate(tile, true);
            }
        }
    }
}
