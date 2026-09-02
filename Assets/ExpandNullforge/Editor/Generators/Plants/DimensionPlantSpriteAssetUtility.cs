using System;
using System.Collections.Generic;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What one crop's generated art turned into.</summary>
    internal sealed class DimensionPlantSpriteAssetResult
    {
        /// <summary>The sprite asset, or null when nothing was drawn to build one from.</summary>
        public SpriteAsset Asset;

        public long AddressLow;

        public long AddressHigh;

        public bool HasAsset
        {
            get { return Asset != null; }
        }
    }

    /// <summary>
    /// Builds the sprite asset a generated crop draws itself from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE ASSET PER LOOK, NOT ONE PER CROP PLUS A SKIN. Core Keeper's own crops share a single
    /// sprite asset and layer a <c>SpriteAssetSkin</c> per crop over it, which works because every
    /// vanilla crop has the same five animations in the same order. A framework cannot assume that:
    /// a modded crop may have two stages or six, so the animation LIST is part of what varies and
    /// there is nothing fixed for a skin to override. Writing the whole asset costs the same file
    /// and removes the assumption.
    /// </para>
    /// <para>
    /// NAMING IS THE WHOLE MECHANISM. The game stores no animation names: it takes each picture's
    /// TEXTURE name at load, deletes the asset's own name from it and hashes what is left. So a
    /// stage is called "stage2" only because its texture is called "carrot_stage2" inside an asset
    /// called "carrot" — which is why every authored picture is copied to a name of this
    /// generator's choosing rather than being pointed at where it lies.
    /// </para>
    /// <para>
    /// THE PIVOT IS WHERE THE TILE IS, NOT WHERE THE MIDDLE IS. A plant stands on its tile, so the
    /// picture has to be pinned near its bottom edge or a tall crop sinks halfway into the ground.
    /// Every vanilla crop pins four pixels up from the bottom of the frame — measured across
    /// <c>plant_ripe</c>, <c>plant_stage3</c> and <c>seedHeartBerry</c>, whose fractional pivots all
    /// work out to exactly four — so that is what the ground line is counted in.
    /// </para>
    /// </remarks>
    internal static class DimensionPlantSpriteAssetUtility
    {
        /// <summary>The folder inside a plant output folder where generated art lands.</summary>
        public const string ArtFolderName = DimensionSpriteArtFileUtility.ArtFolderName;

        /// <summary>The name of the twinkle that runs over a ripe plant.</summary>
        /// <remarks>Vanilla's spelling, kept so crop art files read the way the game's own do.</remarks>
        public const string ShineAnimationName = "shine";

        /// <summary>The name of the damp version of a seed's picture.</summary>
        public const string WateredVariantName = "watered";

        /// <summary>How many pixels up from the bottom of a frame the tile sits, by default.</summary>
        public const int VanillaGroundLinePixels = 4;

        private const ulong AddressLowSalt = 0x506C616E74417274UL;

        private const ulong AddressHighSalt = 0x4772617068696373UL;

        /// <summary>The animation name for one stage, counting from one the way vanilla does.</summary>
        /// <remarks>
        /// "stage1" is the first thing a plant shows, not a pre-sprout picture. Core Keeper's own
        /// renderer plays <c>stage[currentStage + 1]</c> out of a fixed four, which leaves its
        /// "stage1" unused by every crop it ships; the framework's own view indexes straight by
        /// stage, so here the names line up with the stages and none is wasted.
        /// </remarks>
        public static string StageAnimationName(int stageIndex)
        {
            return "stage" + (stageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The two numbers a look's sprite asset will carry, without building it.</summary>
        /// <remarks>
        /// The generated bootstrap needs the same pair the generator wrote, and deriving them twice
        /// from the same name is what saves the two steps having to meet.
        /// </remarks>
        public static void AddressFor(string addressSeed, out long low, out long high)
        {
            low = DimensionSpriteArtFileUtility.StableAddressPart(addressSeed, AddressLowSalt);
            high = DimensionSpriteArtFileUtility.StableAddressPart(addressSeed, AddressHighSalt);
        }

        /// <summary>
        /// Writes (or refreshes) the sprite asset for a plant in the ground.
        /// </summary>
        /// <param name="pictures">One per stage, from just sprouted to ripe. Nulls are skipped.</param>
        /// <param name="picturesInRow">How many pictures each of those rows holds. Parallel.</param>
        /// <param name="groundLinePixels">How far up each frame the tile sits.</param>
        /// <remarks>
        /// The caller's asset-editing batch is paused around the writes that need an import to be
        /// visible immediately: inside a batch a freshly written texture cannot be loaded back, and
        /// the loaded texture is exactly what the sprite asset has to point at.
        /// </remarks>
        public static DimensionPlantSpriteAssetResult BuildPlant(
            string assetName,
            string addressSeed,
            Texture2D[] pictures,
            int[] picturesInRow,
            float speed,
            int groundLinePixels,
            Texture2D shinePicture,
            int shinePictures,
            string outputFolder,
            Action<string> warn)
        {
            DimensionPlantSpriteAssetResult result = new DimensionPlantSpriteAssetResult();
            if (pictures == null || pictures.Length == 0)
            {
                return result;
            }

            string safeName = DimensionSpriteArtFileUtility.SanitizeName(assetName, "Plant");
            string artFolder = outputFolder + "/" + ArtFolderName;
            DimensionSpriteArtFileUtility.EnsureFolder(artFolder);

            List<Row> rows = new List<Row>();
            bool ripeWasDrawn = false;

            // Textures first, and outside the asset batch: the sprite asset below has to hold real
            // loaded Texture2D references, which only exist once Unity has imported the copies.
            AssetDatabase.StopAssetEditing();
            try
            {
                for (int i = 0; i < pictures.Length; i++)
                {
                    // A stage whose picture is missing simply gets no animation. Nothing shifts
                    // down to take its place: the runtime asks for a stage by NAME derived from its
                    // own number, so the stage that was not drawn is the stage that draws nothing.
                    Texture2D copied = Copy(
                        pictures[i], safeName, StageAnimationName(i), artFolder,
                        "stage " + (i + 1), warn);
                    if (copied == null)
                    {
                        continue;
                    }

                    int count = picturesInRow != null && i < picturesInRow.Length && picturesInRow[i] > 0
                        ? picturesInRow[i]
                        : 1;
                    rows.Add(new Row
                    {
                        Name = StageAnimationName(i),
                        Texture = copied,
                        Frames = count,
                        Speed = speed,
                        Loop = true
                    });
                    ripeWasDrawn = i == pictures.Length - 1;
                }

                if (shinePicture != null && ripeWasDrawn)
                {
                    Texture2D copied = Copy(
                        shinePicture, safeName, ShineAnimationName, artFolder, "twinkle", warn);
                    if (copied != null)
                    {
                        rows.Add(new Row
                        {
                            Name = ShineAnimationName,
                            Texture = copied,
                            Frames = shinePictures < 1 ? 1 : shinePictures,
                            Speed = speed,

                            // Plays once and hands back to the ripe picture, which is what makes it
                            // a twinkle rather than a plant that flickers for ever. Only chained
                            // when the ripe picture was actually drawn: a chain to an animation the
                            // asset does not hold is an error logged at every world load.
                            Loop = false,
                            ExitsTo = StageAnimationName(pictures.Length - 1)
                        });
                    }
                }
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
            }

            if (rows.Count == 0)
            {
                return result;
            }

            return Write(result, safeName, addressSeed, artFolder, rows, null, null, groundLinePixels, outputFolder, warn);
        }

        /// <summary>
        /// Writes (or refreshes) the sprite asset for a seed sitting in the soil.
        /// </summary>
        /// <remarks>
        /// A seed has no animations at all — it is one still picture with a second, damp one the
        /// game swaps to over watered ground. That second picture is a STATIC VARIANT, whose name
        /// comes off its texture the same way an animation's does, which is why it is copied to
        /// "&lt;asset&gt;_watered" and nothing else.
        /// </remarks>
        public static DimensionPlantSpriteAssetResult BuildSeed(
            string assetName,
            string addressSeed,
            Texture2D seedPicture,
            Texture2D wateredPicture,
            int groundLinePixels,
            string outputFolder,
            Action<string> warn)
        {
            DimensionPlantSpriteAssetResult result = new DimensionPlantSpriteAssetResult();
            if (seedPicture == null)
            {
                return result;
            }

            string safeName = DimensionSpriteArtFileUtility.SanitizeName(assetName, "Seed");
            string artFolder = outputFolder + "/" + ArtFolderName;
            DimensionSpriteArtFileUtility.EnsureFolder(artFolder);

            Texture2D still;
            Texture2D damp = null;
            AssetDatabase.StopAssetEditing();
            try
            {
                still = Copy(seedPicture, safeName, null, artFolder, "seed", warn);
                if (wateredPicture != null)
                {
                    damp = Copy(
                        wateredPicture, safeName, WateredVariantName, artFolder,
                        "watered seed", warn);
                }
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
            }

            if (still == null)
            {
                return result;
            }

            return Write(
                result, safeName, addressSeed, artFolder, new List<Row>(), still, damp,
                groundLinePixels, outputFolder, warn);
        }

        /// <summary>One animation's worth of picture and how it plays.</summary>
        private struct Row
        {
            public string Name;
            public Texture2D Texture;
            public int Frames;
            public float Speed;
            public bool Loop;
            public string ExitsTo;
        }

        private static Texture2D Copy(
            Texture2D source,
            string assetName,
            string animationName,
            string artFolder,
            string what,
            Action<string> warn)
        {
            string stem = string.IsNullOrEmpty(animationName)
                ? assetName
                : assetName + "_" + animationName;
            DimensionArtCopyProblem problem;
            Texture2D copied = DimensionSpriteArtFileUtility.CopyPicture(
                source, artFolder + "/" + stem + ".png", out problem);
            switch (problem)
            {
                case DimensionArtCopyProblem.NotAPng:
                    warn(
                        "draws its " + what + " with something that is not a PNG file in the " +
                        "project. Save the picture as a .png and point it at that.");
                    break;
                case DimensionArtCopyProblem.NotOnDisk:
                    warn(
                        "could not read its " + what + " picture from disk, so that one was left " +
                        "out and nothing will be drawn for it.");
                    break;
                case DimensionArtCopyProblem.ImportFailed:
                    warn("could not import a copy of its " + what + " picture.");
                    break;
            }

            return copied;
        }

        private static DimensionPlantSpriteAssetResult Write(
            DimensionPlantSpriteAssetResult result,
            string assetName,
            string addressSeed,
            string artFolder,
            List<Row> rows,
            Texture2D still,
            Texture2D stillVariant,
            int groundLinePixels,
            string outputFolder,
            Action<string> warn)
        {
            AddressFor(addressSeed, out result.AddressLow, out result.AddressHigh);

            string assetPath = artFolder + "/" + assetName + ".asset";
            SpriteAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SpriteAsset>();
                asset.name = assetName;
                created = true;
            }

            WriteSpriteAsset(asset, assetName, rows, still, stillVariant, groundLinePixels, result);

            if (created)
            {
                AssetDatabase.StopAssetEditing();
                try
                {
                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                }
                finally
                {
                    AssetDatabase.StartAssetEditing();
                }

                asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
                if (asset == null)
                {
                    warn("could not save its pictures, so it will not be drawn this build.");
                    return result;
                }

                // The instance that was written is not the instance that came back off disk.
                WriteSpriteAsset(asset, assetName, rows, still, stillVariant, groundLinePixels, result);
            }

            EditorUtility.SetDirty(asset);
            result.Asset = asset;
            DimensionSpriteArtFileUtility.EnsureManifestContains(outputFolder, asset, warn);
            return result;
        }

        private static void WriteSpriteAsset(
            SpriteAsset asset,
            string assetName,
            List<Row> rows,
            Texture2D still,
            Texture2D stillVariant,
            int groundLinePixels,
            DimensionPlantSpriteAssetResult result)
        {
            asset.name = assetName;
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();

            SetLong(serialized, "m_address.m_low", result.AddressLow);
            SetLong(serialized, "m_address.m_high", result.AddressHigh);

            SerializedProperty staticData = serialized.FindProperty("m_staticSpriteData");
            ClearSpriteData(staticData);
            if (still != null)
            {
                SetObject(staticData, "texture", still);
                SetPivot(staticData, still, 1, groundLinePixels);
                SetBool(staticData, "inheritPivot", false);
            }

            SerializedProperty staticVariants = serialized.FindProperty("m_staticVariants");
            SetArraySize(staticVariants, stillVariant != null ? 1 : 0);
            if (stillVariant != null)
            {
                SerializedProperty variant = staticVariants.GetArrayElementAtIndex(0);
                ClearSpriteData(variant);
                SetObject(variant, "texture", stillVariant);

                // Left inheriting on purpose: a damp seed is the same seed, so pinning it anywhere
                // but where the dry one is pinned would make it hop when the ground is watered.
                SetBool(variant, "inheritPivot", true);
            }

            SetArraySize(serialized.FindProperty("m_positionalData"), 0);
            SetArraySize(serialized.FindProperty("m_events"), 0);

            SerializedProperty animations = serialized.FindProperty("m_animations");
            SetArraySize(animations, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                WriteAnimation(
                    animations.GetArrayElementAtIndex(i), rows[i], assetName, groundLinePixels);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteAnimation(
            SerializedProperty animationProperty,
            Row row,
            string assetName,
            int groundLinePixels)
        {
            SetString(animationProperty, "m_guid", AnimationGuid(assetName, row.Name));
            SetString(
                animationProperty,
                "m_exitAnimationGuid",
                string.IsNullOrEmpty(row.ExitsTo)
                    ? Guid.Empty.ToString()
                    : AnimationGuid(assetName, row.ExitsTo));

            SerializedProperty spriteData = animationProperty.FindPropertyRelative("m_spriteData");
            ClearSpriteData(spriteData);
            SetObject(spriteData, "texture", row.Texture);
            SetPivot(spriteData, row.Texture, row.Frames, groundLinePixels);

            // Never inherited: an inherited pivot is taken from the first animation that has one,
            // so a two-tile-tall ripe picture would be pinned wherever the sprout is pinned.
            SetBool(spriteData, "inheritPivot", false);

            int frames = row.Frames < 1 ? 1 : row.Frames;
            SetInt(animationProperty, "srcFrameCount", frames);
            SetFloat(animationProperty, "fps", row.Speed <= 0f ? 10f : row.Speed);
            SetBool(animationProperty, "loop", row.Loop);
            SetArraySize(animationProperty.FindPropertyRelative("m_transitions"), 0);
            SetArraySize(animationProperty.FindPropertyRelative("m_variants"), 0);

            // One entry per picture, never fewer. The game walks this list by picture index while
            // building a clip's playback table, so a short list is an exception at load and a long
            // one is frames nobody ever sees.
            SerializedProperty frameData = animationProperty.FindPropertyRelative("frameData");
            SetArraySize(frameData, frames);
            for (int i = 0; i < frames; i++)
            {
                SerializedProperty frame = frameData.GetArrayElementAtIndex(i);
                SetInt(frame, "holdFrames", 0);
                SetInt(frame, "eventMask", 0);
            }
        }

        /// <summary>
        /// Pins a picture to its tile rather than to its own middle.
        /// </summary>
        /// <remarks>
        /// The pivot is a fraction of one FRAME, not of the whole row, so the width is divided by
        /// the picture count before anything is worked out. Horizontally it is always the middle;
        /// vertically it is the ground line, counted in pixels so a taller picture does not need a
        /// different number.
        /// </remarks>
        public static Vector2 PivotFor(int frameWidth, int frameHeight, int groundLinePixels)
        {
            float y = frameHeight > 0
                ? Mathf.Clamp01((float)groundLinePixels / frameHeight)
                : 0.5f;
            return new Vector2(0.5f, y);
        }

        private static void SetPivot(
            SerializedProperty spriteData,
            Texture2D texture,
            int frames,
            int groundLinePixels)
        {
            if (spriteData == null || texture == null)
            {
                return;
            }

            SerializedProperty pivot = spriteData.FindPropertyRelative("pivot");
            if (pivot == null)
            {
                return;
            }

            int count = frames < 1 ? 1 : frames;
            pivot.vector2Value = PivotFor(texture.width / count, texture.height, groundLinePixels);
        }

        /// <summary>
        /// The identity an animation is chained to by, derived rather than random.
        /// </summary>
        /// <remarks>
        /// A twinkle hands back to the ripe picture by that picture's guid, so a freshly minted
        /// guid on every regeneration would break the chain the moment a crop was regenerated.
        /// Deriving it from the asset and animation names means the same animation always has the
        /// same identity.
        /// </remarks>
        private static string AnimationGuid(string assetName, string animationName)
        {
            string seed = assetName + ":" + animationName;
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(
                DimensionSpriteArtFileUtility.StableAddressPart(seed, 0x506C616E7447756FUL))
                .CopyTo(bytes, 0);
            BitConverter.GetBytes(
                DimensionSpriteArtFileUtility.StableAddressPart(seed, 0x6964666F72616E69UL))
                .CopyTo(bytes, 8);
            return new Guid(bytes).ToString();
        }

        private static void ClearSpriteData(SerializedProperty spriteData)
        {
            if (spriteData == null)
            {
                return;
            }

            SetObject(spriteData, "texture", null);
            SetObject(spriteData, "emissiveTexture", null);
            SetObject(spriteData, "normalTexture", null);
            SerializedProperty pivot = spriteData.FindPropertyRelative("pivot");
            if (pivot != null)
            {
                pivot.vector2Value = new Vector2(0.5f, 0.5f);
            }

            SetArraySize(spriteData.FindPropertyRelative("positionalData"), 0);
            SetBool(spriteData, "inheritPivot", true);
        }

        // ---- Small serialized-property helpers -------------------------------------------------

        private static void SetArraySize(SerializedProperty property, int size)
        {
            if (property != null && property.isArray)
            {
                property.arraySize = size;
            }
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(name);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(name);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetBool(SerializedProperty parent, string name, bool value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(name);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(name);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetObject(
            SerializedProperty parent,
            string name,
            UnityEngine.Object value)
        {
            SerializedProperty property = parent == null ? null : parent.FindPropertyRelative(name);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetLong(SerializedObject serialized, string path, long value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
            {
                property.longValue = value;
            }
        }
    }
}
