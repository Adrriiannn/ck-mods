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
    /// Makes a farmable custom block till into ITS OWN soil rather than into dirt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ONE THING THE HOE ASKS. Tilling is tileset-agnostic — <c>HoeSlot</c> elects
    /// <c>HOE_GROUND</c> for any tile whose type is <c>ground</c>, whatever tileset it belongs to, so
    /// a custom block is already tillable with no patch at all. What decides the RESULT's tileset is
    /// a single lookup:
    /// </para>
    /// <code>
    /// int num = 0;                                              // HoeSlot.cs:122-131
    /// if (PugDatabase.TryGetTileItemInfo(TileType.dugUpGround, (Tileset)tile.tileset, …).objectID
    ///     != ObjectID.None)
    /// {
    ///     num = tile.tileset;                                   // keep the block's own tileset
    /// }
    /// EntityUtility.AddTile(num, TileType.dugUpGround, …);      // else num stays 0 == Dirt
    /// </code>
    /// <para>
    /// So without an object registered for <c>(our tileset, dugUpGround)</c>, hoeing a beautiful
    /// custom soil produces DIRT tilled soil — the one visual break a farming player would notice
    /// immediately. This class registers exactly that object, which is all the lookup needs: it
    /// tests only that the id is not <c>ObjectID.None</c>.
    /// </para>
    /// <para>
    /// WATERING NEEDS NOTHING. <c>WaterCanSlot</c> writes
    /// <c>EntityUtility.AddTile(top.tileset, TileType.wateredGround, …)</c> — it inherits the tileset
    /// of the tile it waters, with no lookup and no fallback. Once the hoe produces custom-tileset
    /// tilled soil, watering it produces custom-tileset watered soil for free. No second object.
    /// </para>
    /// <para>
    /// The object is deliberately <c>NonObtainable</c> and carries no inventory item: it exists only
    /// to answer a database question. Nothing should be able to pick it up, and the drop path
    /// explicitly refuses to drop a <c>NonObtainable</c> object.
    /// </para>
    /// </remarks>
    internal static class DimensionTilesetFarmingAuthoring
    {
        /// <summary>
        /// Writes the hidden tilled-ground object for <paramref name="tileset"/> and returns its
        /// prefab path, or null when nothing was written.
        /// </summary>
        public static string CreateTilledGroundPrefab(
            string outputFolder,
            DimensionTilesetAsset tileset,
            DimensionItemGenerationReport report)
        {
            if (tileset == null || string.IsNullOrEmpty(outputFolder))
            {
                return null;
            }

            int tilesetId = tileset.TilesetId;
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId))
            {
                report.Warnings.Add(
                    "Tileset '" + tileset.TilesetName + "' resolved to id " + tilesetId +
                    ", which is not in the custom range; its tilled-ground object was not generated. " +
                    "Hoeing it would produce dirt soil.");
                return null;
            }

            // Already mod-qualified: TilesetName is "{mod}:{name}".
            string objectName = tileset.TilesetName + ".tilled.infra";
            string fileName = DimensionGeneratedPrefabUtility.SanitizeFileName(objectName);
            string prefabPath = outputFolder + "/" + fileName + ".prefab";

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;
            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(fileName);

            try
            {
                ObjectAuthoring authoring =
                    DimensionGeneratedPrefabUtility.EnsureComponent<ObjectAuthoring>(root);
                authoring.objectName = objectName;
                authoring.initialAmount = 1;
                authoring.additionalSprites = new List<Sprite>();

                if (!DimensionGeneratedPrefabUtility.TrySetEnumMember(
                        authoring, "objectType", "NonObtainable"))
                {
                    report.Warnings.Add(
                        "Could not set objectType=NonObtainable on the tilled-ground object for '" +
                        tileset.TilesetName + "'. Core Keeper may have renamed the enum member; the " +
                        "object was still written and tilling will still keep the tileset.");
                }

                TileAuthoring tile =
                    DimensionGeneratedPrefabUtility.EnsureComponent<TileAuthoring>(root);
                tile.tileset = (Tileset)tilesetId;
                tile.tileType = TileType.dugUpGround;

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
                    "The tilled-ground object for '" + tileset.TilesetName +
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
        /// Deletes a previously generated tilled-ground object for a tileset that is no longer
        /// farmable, so turning "Can be farmed" off actually takes effect.
        /// </summary>
        /// <remarks>
        /// Generation updates in place, so without this the object would survive and the block would
        /// keep tilling into its own soil after the capability was switched off — the same
        /// add-but-never-remove trap that made the rigid-surface toggle look broken.
        /// </remarks>
        public static void RemoveTilledGroundPrefab(
            string outputFolder,
            DimensionTilesetAsset tileset,
            DimensionItemGenerationReport report)
        {
            if (tileset == null || string.IsNullOrEmpty(outputFolder))
            {
                return;
            }

            string fileName = DimensionGeneratedPrefabUtility.SanitizeFileName(
                tileset.TilesetName + ".tilled.infra");
            string prefabPath = outputFolder + "/" + fileName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                return;
            }

            if (AssetDatabase.DeleteAsset(prefabPath))
            {
                report.Removed.Add(prefabPath);
            }
            else
            {
                report.Warnings.Add(
                    "'" + prefabPath + "' is left over from a block that is no longer farmable and " +
                    "could not be deleted. Tilling that block will keep using its own soil until it " +
                    "is removed by hand.");
            }
        }
    }
}
