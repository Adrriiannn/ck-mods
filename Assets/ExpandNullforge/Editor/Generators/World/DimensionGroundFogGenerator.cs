using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating ground fog did, so the dashboard can report it.</summary>
    public sealed class DimensionGroundFogReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Gives a custom block the low-lying fog that hangs over Core Keeper's mold ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THERE IS NOTHING TO PATCH HERE, WHICH IS THE WHOLE POINT. Ground fog in Core Keeper is a piece
    /// of data, not code: a <c>GroundFogDataBlock</c> naming a tile type, a tileset and a tint. The
    /// renderer reads every one of those blocks at startup and builds a lookup, and it reads the
    /// tileset as a raw number — so a block naming a custom tileset works exactly like a block naming
    /// a vanilla one. Generating the asset IS the feature.
    /// </para>
    /// <para>
    /// The tint's alpha is the fog's density rather than a transparency: the renderer takes RGB as
    /// colour and A as intensity. That is worth stating because a fog authored at alpha 1 comes out
    /// far heavier than an author expects from a colour picker.
    /// </para>
    /// <para>
    /// ADDRESSES ARE DERIVED FROM THE ASSET PATH, not random. A data block that changed address every
    /// time it was regenerated would look like a different block to anything holding a reference, and
    /// the deterministic form means regenerating twice produces byte-identical assets.
    /// </para>
    /// </remarks>
    public static class DimensionGroundFogGenerator
    {
        private const ulong AddressSaltLow = 0x67726F756E64666FUL;
        private const ulong AddressSaltHigh = 0x6720666F67626C6BUL;

        /// <summary>The folder inside a mod where its generated fog blocks live.</summary>
        public const string FolderName = "GroundFog";

        /// <summary>
        /// Writes one fog block per block that asks for fog, and removes the ones that no longer do.
        /// </summary>
        /// <remarks>
        /// Removal matters as much as creation. A fog block left behind after an author turns fog off
        /// keeps rendering — the renderer has no idea the tileset's owner changed its mind — so the
        /// biome stays foggy with nothing in the project still saying it should be.
        /// </remarks>
        public static DimensionGroundFogReport Generate(
            IEnumerable<DimensionTilesetAsset> tilesets,
            string modRoot)
        {
            DimensionGroundFogReport report = new DimensionGroundFogReport();
            if (string.IsNullOrEmpty(modRoot))
            {
                report.Warnings.Add(
                    "The owning mod folder could not be resolved, so no ground fog was generated.");
                return report;
            }

            string folder = modRoot + "/" + FolderName;
            Dictionary<string, DimensionTilesetAsset> wanted =
                new Dictionary<string, DimensionTilesetAsset>(StringComparer.Ordinal);

            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset == null || !tileset.Enabled || !tileset.HasGroundFog)
                    {
                        continue;
                    }

                    // A fog block is keyed by the block's own tileset id, and a reskin's tiles carry
                    // the game's id instead — the block would ship and never be matched. Reported
                    // rather than dropped in silence, because the toggle may have been set before
                    // the block became a reskin.
                    if (tileset.ItemMode == ExpandNullforge.Tilesets.DimensionTilesetItemMode.ReskinVanilla)
                    {
                        report.Warnings.Add(
                            "'" + tileset.BlockName + "' asks for ground fog, but it dresses one of " +
                            "the game's blocks rather than being one of its own. Fog is looked up by " +
                            "the block's own number and a reskinned tile keeps the game's, so no fog " +
                            "was written. Make it a block of its own to give it fog.");
                        continue;
                    }

                    wanted[AssetPathFor(folder, tileset)] = tileset;
                }
            }

            if (wanted.Count == 0 && !AssetDatabase.IsValidFolder(folder))
            {
                // Nothing wants fog and nothing ever did. Creating an empty folder just to delete
                // from it would leave a stray directory in every mod that never used the feature.
                return report;
            }

            DimensionAssetFolders.Ensure(folder);
            RemoveUnwanted(folder, wanted, report);

            foreach (KeyValuePair<string, DimensionTilesetAsset> pair in wanted)
            {
                Write(pair.Key, pair.Value, report);
            }

            AssetDatabase.SaveAssets();
            return report;
        }

        private static string AssetPathFor(string folder, DimensionTilesetAsset tileset)
        {
            return folder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(tileset.TilesetName, "Block") + "_GroundFog.asset";
        }

        private static void Write(
            string assetPath,
            DimensionTilesetAsset tileset,
            DimensionGroundFogReport report)
        {
            GroundFogDataBlock block = AssetDatabase.LoadAssetAtPath<GroundFogDataBlock>(assetPath);
            bool created = block == null;
            if (created)
            {
                block = ScriptableObject.CreateInstance<GroundFogDataBlock>();
                block.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }

            block.tileType = TileType.ground;
            block.tileset = (Tileset)tileset.TilesetId;
            block.tint = tileset.GroundFogTint;

            if (created)
            {
                AssetDatabase.CreateAsset(block, assetPath);
            }

            if (!TryAssignAddress(block, assetPath))
            {
                report.Warnings.Add(
                    "Ground fog for '" + tileset.BlockName + "' could not be given an address, so the " +
                    "game may not load it. The block itself still works; only its fog is affected.");
            }

            EditorUtility.SetDirty(block);

            if (created)
            {
                report.Created.Add(assetPath);
            }
            else
            {
                report.Updated.Add(assetPath);
            }
        }

        /// <summary>
        /// Deletes fog blocks in our folder that no tileset asks for any more.
        /// </summary>
        /// <remarks>
        /// Scoped to our own folder and our own naming so a hand-authored fog block a creator dropped
        /// in beside ours is never deleted by a regeneration they did not connect to it.
        /// </remarks>
        private static void RemoveUnwanted(
            string folder,
            Dictionary<string, DimensionTilesetAsset> wanted,
            DimensionGroundFogReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:GroundFogDataBlock", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) ||
                    wanted.ContainsKey(path) ||
                    !path.EndsWith("_GroundFog.asset", StringComparison.Ordinal))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                {
                    report.Removed.Add(path);
                }
            }
        }

        /// <summary>
        /// Gives the block a stable address derived from where it lives.
        /// </summary>
        /// <remarks>
        /// Only assigned when it has none. Overwriting an existing address on every regeneration would
        /// break any reference already pointing at the block, and would make two builds of the same
        /// mod disagree about what its fog is called.
        /// </remarks>
        private static bool TryAssignAddress(GroundFogDataBlock block, string assetPath)
        {
            SerializedObject serialized = new SerializedObject(block);
            serialized.Update();

            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null ? null : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null ? null : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                return false;
            }

            if (low.longValue != 0L || high.longValue != 0L)
            {
                return true;
            }

            low.longValue = DimensionSpriteAssetAddress.Part(assetPath, AddressSaltLow);
            high.longValue = DimensionSpriteAssetAddress.Part(assetPath, AddressSaltHigh);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

    }
}
