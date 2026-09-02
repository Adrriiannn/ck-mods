using System;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Turns "what this looks like on the character" into the skin data block Core Keeper reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A SEPARATE ASSET. Core Keeper does not put the character art on the item. The item
    /// carries <c>EquipmentSkinAuthoring</c>, which holds nothing but an address, and that address
    /// points at a <c>SkinBaseDataBlock</c> asset holding the textures. So generating a visible
    /// helmet means generating two things, and the second one is a <c>ScriptableObject</c> the
    /// author never has to see.
    /// </para>
    /// <para>
    /// WHY THIS WORKS FOR A MOD AT ALL. Verified in the loader: <c>PugMod</c> hands every mod
    /// bundle to <c>ScriptableData.AddDataBlocksLoader</c>, so a data block shipped inside a mod is
    /// registered exactly like a vanilla one and resolves by address. The textures resolve the same
    /// way — the loader registers each mod asset with the resource provider under its own guid,
    /// which is the guid an <c>AssetReference</c> stores.
    /// </para>
    /// <para>
    /// WHY THE ADDRESS IS THE UNITY GUID. A data block address is a 128-bit guid, and the asset
    /// already has one that is unique by construction and stable across regenerations because Unity
    /// keeps the .meta file. Deriving one from a hash instead would buy nothing and could collide
    /// with a real asset.
    /// </para>
    /// </remarks>
    internal static class DimensionEquipmentSkinGenerator
    {
        /// <summary>Where generated skin data blocks go, under the item output folder.</summary>
        public const string FolderName = "Skins";

        /// <summary>
        /// Creates or updates the skin data block for an item, and returns it.
        /// </summary>
        /// <remarks>
        /// Returns null when the item is not worn, which is the ordinary case and not a problem.
        /// Returns null and reports when it is worn but has no art, because that is the failure that
        /// is otherwise completely silent.
        /// </remarks>
        public static ScriptableDataBlock EnsureSkin(
            DimensionEquipmentSkinTemplate skin,
            string itemId,
            string outputFolder,
            Action<string> report)
        {
            if (skin == null || !skin.IsWorn)
            {
                return null;
            }

            if (skin.WornButInvisible)
            {
                if (report != null)
                {
                    report(
                        "'" + itemId + "' is set to be worn on the " + Describe(skin.Slot) +
                        " but has no character sheet, so a player who equips it will see no " +
                        "change on their character.");
                }

                return null;
            }

            if (skin.SheetIsTheWrongSize && report != null)
            {
                report(
                    "'" + itemId + "' has a character sheet of " + skin.Sheet.width + " x " +
                    skin.Sheet.height + ". Core Keeper samples equipment art as " +
                    DimensionEquipmentSkinTemplate.SheetWidth + " x " +
                    DimensionEquipmentSkinTemplate.SheetHeight +
                    " whatever size it is, so the art will land in the wrong places.");
            }

            string path = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(itemId, "Skin") + "Skin.asset";
            ScriptableDataBlock existing =
                AssetDatabase.LoadAssetAtPath<ScriptableDataBlock>(path);

            Type wanted = TypeFor(skin.Slot);
            if (existing != null && existing.GetType() != wanted)
            {
                // The author changed which body part it goes on. A data block cannot change type,
                // so the old one is replaced rather than left behind pointing at the wrong slot.
                AssetDatabase.DeleteAsset(path);
                existing = null;
            }

            ScriptableDataBlock block = existing;
            if (block == null)
            {
                block = (ScriptableDataBlock)ScriptableObject.CreateInstance(wanted);
                AssetDatabase.CreateAsset(block, path);
            }

            Configure(block, skin);
            StampAddressFromAssetGuid(block, path);
            EditorUtility.SetDirty(block);
            return block;
        }

        /// <summary>Removes a skin asset an item no longer wants.</summary>
        /// <remarks>
        /// Generation is authoritative here too: an item switched back to not-worn must not leave a
        /// data block behind, or the mod ships art nothing references.
        /// </remarks>
        public static void RemoveSkinIfPresent(string itemId, string outputFolder)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(outputFolder))
            {
                return;
            }

            string path = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(itemId, "Skin") + "Skin.asset";
            if (AssetDatabase.LoadAssetAtPath<ScriptableDataBlock>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void Configure(ScriptableDataBlock block, DimensionEquipmentSkinTemplate skin)
        {
            string sheet = GuidOf(skin.Sheet);
            string glow = GuidOf(skin.GlowingParts);

            HelmSkinDataBlock helm = block as HelmSkinDataBlock;
            if (helm != null)
            {
                helm.helmTexture = new UnityEngine.AddressableAssets.AssetReferenceTexture2D(sheet);
                helm.emissiveHelmTexture =
                    new UnityEngine.AddressableAssets.AssetReferenceTexture2D(glow);
                helm.hairType = HairTypeFor(skin.HairUnderIt);
                helm.pixelOffset = skin.Nudge;
                return;
            }

            BreastArmorSkinDataBlock breast = block as BreastArmorSkinDataBlock;
            if (breast != null)
            {
                breast.breastTexture =
                    new UnityEngine.AddressableAssets.AssetReferenceTexture2D(sheet);
                breast.emissiveBreastTexture =
                    new UnityEngine.AddressableAssets.AssetReferenceTexture2D(glow);
                breast.shirtVisibility =
                    skin.HidesTheShirt ? ShirtVisibility.Hide : ShirtVisibility.FullyShow;
                return;
            }

            PantsArmorSkinDataBlock pants = block as PantsArmorSkinDataBlock;
            if (pants != null)
            {
                pants.pantsTexture =
                    new UnityEngine.AddressableAssets.AssetReferenceTexture2D(sheet);
                pants.emissivePantsTexture =
                    new UnityEngine.AddressableAssets.AssetReferenceTexture2D(glow);
                pants.pantsVisibility =
                    skin.HidesTheTrousers ? PantsVisibility.Hide : PantsVisibility.FullyShow;
            }
        }

        /// <summary>
        /// Gives a generated data block the address the game will look it up by.
        /// </summary>
        /// <remarks>
        /// <c>m_address</c> is private and serialized, which is exactly what a <c>SerializedObject</c>
        /// is for. Left unset it stays all-zero, every generated skin in the mod shares that one
        /// address, and the game resolves whichever it happened to register first — so several
        /// custom helmets would all draw as the same one.
        /// </remarks>
        private static void StampAddressFromAssetGuid(ScriptableDataBlock block, string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            Guid parsed;
            if (!Guid.TryParseExact(guid, "N", out parsed))
            {
                return;
            }

            byte[] bytes = parsed.ToByteArray();
            SerializedObject serialized = new SerializedObject(block);
            SerializedProperty address = serialized.FindProperty("m_address");
            if (address == null)
            {
                return;
            }

            SerializedProperty low = address.FindPropertyRelative("m_low");
            SerializedProperty high = address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                return;
            }

            low.longValue = BitConverter.ToInt64(bytes, 0);
            high.longValue = BitConverter.ToInt64(bytes, 8);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GuidOf(Texture2D texture)
        {
            if (texture == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static Type TypeFor(DimensionEquipmentSkinSlot slot)
        {
            switch (slot)
            {
                case DimensionEquipmentSkinSlot.Head:
                    return typeof(HelmSkinDataBlock);
                case DimensionEquipmentSkinSlot.Legs:
                    return typeof(PantsArmorSkinDataBlock);
                default:
                    return typeof(BreastArmorSkinDataBlock);
            }
        }

        private static HelmHairType HairTypeFor(DimensionHairUnderHelm hair)
        {
            switch (hair)
            {
                case DimensionHairUnderHelm.PartlyShown:
                    return HelmHairType.PartlyShown;
                case DimensionHairUnderHelm.FullyShown:
                    return HelmHairType.FullyShow;
                default:
                    return HelmHairType.Hide;
            }
        }

        private static string Describe(DimensionEquipmentSkinSlot slot)
        {
            switch (slot)
            {
                case DimensionEquipmentSkinSlot.Head:
                    return "head";
                case DimensionEquipmentSkinSlot.Legs:
                    return "legs";
                default:
                    return "chest";
            }
        }

        /// <summary>Makes the skin folder, before any batched asset editing opens.</summary>
        /// <remarks>
        /// Public because the caller has to do this OUTSIDE its own StartAssetEditing block:
        /// a folder created inside a batch does not exist until the batch closes, so an asset
        /// written into it lands nowhere. That is exactly how the first generated skin went
        /// missing while later ones appeared, which reads as flakiness rather than as a rule.
        /// </remarks>
        public static void EnsureSkinFolder(string folder)
        {
            DimensionAssetFolders.Ensure(folder);
        }

    }
}
