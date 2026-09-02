using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What one creature's generated art turned into.</summary>
    internal sealed class DimensionCreatureSpriteAssetResult
    {
        /// <summary>The sprite asset, or null when the creature had no clips to build one from.</summary>
        public SpriteAsset Asset;

        public long AddressLow;

        public long AddressHigh;

        public bool HasAsset
        {
            get { return Asset != null; }
        }
    }

    /// <summary>
    /// Builds the sprite asset a creature's body plays from, out of the strips its author drew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NAMING IS THE WHOLE MECHANISM, AND IT IS NOT OBVIOUS. Core Keeper does not store a clip's
    /// name anywhere. It works one out at load by taking the clip's TEXTURE name and deleting the
    /// sprite asset's own name from it, then hashes what is left. So a clip is called "attack" only
    /// because its texture is called "Ember_attack" inside an asset called "Ember" — which is why
    /// this generator copies every authored strip to a name of its own choosing rather than using
    /// the file the author drew in place. The same rule one level down gives the up and side
    /// variants their names.
    /// </para>
    /// <para>
    /// WHAT WOULD GO WRONG WITHOUT THE CHECKS BELOW. A clip whose hold list is a different length
    /// from its picture count reads off the end of that list at load; a strip that is not a PNG on
    /// disk cannot be copied at all; a thirty-third named moment has no bit left in the frame mask
    /// to occupy. Each of those fails quietly at runtime, so each is reported here instead.
    /// </para>
    /// </remarks>
    internal static class DimensionCreatureSpriteAssetUtility
    {
        /// <summary>The folder inside a creature output folder where generated art lands.</summary>
        public const string ArtFolderName = "Art";

        /// <summary>
        /// Writes (or refreshes) one creature's sprite asset and everything it references.
        /// </summary>
        /// <remarks>
        /// The caller's asset-editing batch is paused around the writes that need an import to be
        /// visible immediately: inside a batch, a freshly written texture cannot be loaded back,
        /// and the loaded texture is exactly what the sprite asset has to point at.
        /// </remarks>
        public static DimensionCreatureSpriteAssetResult Build(
            string creatureId,
            string creatureObjectName,
            DimensionCreatureAnimationTemplate animation,
            string outputFolder,
            Action<string> warn)
        {
            DimensionCreatureSpriteAssetResult result = new DimensionCreatureSpriteAssetResult();
            if (animation == null || !animation.HasAnyClip)
            {
                return result;
            }

            string assetName = SanitizeAssetName(creatureId);
            string artFolder = outputFolder + "/" + ArtFolderName;
            DimensionAssetFolders.Ensure(artFolder);

            Warn(animation, warn);

            List<DimensionCreatureClipTemplate> usable = CollectUsableClips(animation, warn);
            if (usable.Count == 0)
            {
                return result;
            }

            string[] moments = animation.CollectMomentNames();
            if (moments.Length > DimensionCreatureAnimationNames.MaximumEvents)
            {
                Array.Resize(ref moments, DimensionCreatureAnimationNames.MaximumEvents);
            }

            // Textures first, and outside the asset batch: the sprite asset below has to hold real
            // loaded Texture2D references, which only exist once Unity has imported the copies.
            List<ClipArt> art = new List<ClipArt>();
            AssetDatabase.StopAssetEditing();
            try
            {
                for (int i = 0; i < usable.Count; i++)
                {
                    ClipArt copied = CopyClipArt(usable[i], assetName, artFolder, warn);
                    if (copied.Base != null)
                    {
                        art.Add(copied);
                    }
                }
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
            }

            if (art.Count == 0)
            {
                return result;
            }

            string seed = string.IsNullOrEmpty(creatureObjectName) ? creatureId : creatureObjectName;
            result.AddressLow = StableAddressPart(seed, 0x4372656174757265UL);
            result.AddressHigh = StableAddressPart(seed, 0x616E696D6174696FUL);

            string assetPath = artFolder + "/" + assetName + ".asset";
            SpriteAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(assetPath);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SpriteAsset>();
                asset.name = assetName;
                created = true;
            }

            WriteSpriteAsset(asset, assetName, art, moments, animation, result);

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
                    warn("could not save its animations, so it has no body this build.");
                    return result;
                }

                // The instance that was written is not the instance that came back off disk.
                WriteSpriteAsset(asset, assetName, art, moments, animation, result);
            }

            EditorUtility.SetDirty(asset);
            result.Asset = asset;
            EnsureManifestContains(outputFolder, asset, warn);
            return result;
        }

        /// <summary>Everything a clip contributes, once its strips are copies we control.</summary>
        private struct ClipArt
        {
            public DimensionCreatureClipTemplate Clip;
            public Texture2D Base;
            public Texture2D Up;
            public Texture2D Side;
        }

        private static List<DimensionCreatureClipTemplate> CollectUsableClips(
            DimensionCreatureAnimationTemplate animation,
            Action<string> warn)
        {
            List<DimensionCreatureClipTemplate> usable = new List<DimensionCreatureClipTemplate>();
            List<DimensionCreatureClipKind> seen = new List<DimensionCreatureClipKind>();
            DimensionCreatureClipTemplate[] clips = animation.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                DimensionCreatureClipTemplate clip = clips[i];
                if (clip == null || !clip.Enabled)
                {
                    continue;
                }

                if (clip.Strip == null)
                {
                    if (clip.HasADirectionWithoutABaseStrip)
                    {
                        warn(
                            "has a '" + DimensionCreatureAnimationNames.LabelFor(clip.Kind) +
                            "' clip drawn from behind or from the side but not facing the camera. " +
                            "The camera-facing row is the one the game asks for by default, so " +
                            "draw that one too or the clip never appears.");
                    }

                    continue;
                }

                if (seen.Contains(clip.Kind))
                {
                    continue;
                }

                seen.Add(clip.Kind);
                usable.Add(clip);
            }

            return usable;
        }

        /// <summary>Says the things that make a creature look finished and animate wrong.</summary>
        private static void Warn(DimensionCreatureAnimationTemplate animation, Action<string> warn)
        {
            if (animation.HasDuplicateClips)
            {
                warn(
                    "describes the same thing more than once in its clip list. Only the first is " +
                    "kept, so the later art never plays. Delete the duplicate rows.");
            }

            if (animation.HasTooManyMoments)
            {
                warn(
                    "names more than " + DimensionCreatureAnimationNames.MaximumEvents +
                    " different moments across its clips. A frame only records that many, so the " +
                    "extra ones are dropped. Reuse a moment name instead of adding a new one.");
            }

            if (animation.ClipFor(DimensionCreatureClipKind.Standing) == null)
            {
                warn(
                    "has no 'Standing' clip. That is the one the game asks for whenever nothing " +
                    "else is happening, so it will spend most of its life showing nothing.");
            }

            if (animation.TurnsToFaceWhereItGoes &&
                animation.ClipFor(DimensionCreatureClipKind.Walking) == null)
            {
                warn(
                    "turns to face where it walks but has no 'Walking' clip, so it will slide " +
                    "around in its standing pose.");
            }

            // A moment is only ever a sound. One with no sound sets a bit every time the clip comes
            // round and nothing reads it, which looks exactly like a sound that will not play.
            string[] momentNames = animation.CollectMomentNames();
            string[] momentSounds = animation.CollectMomentSounds();
            for (int i = 0; i < momentNames.Length && i < momentSounds.Length; i++)
            {
                if (!string.IsNullOrEmpty(momentSounds[i]))
                {
                    continue;
                }

                warn(
                    "names a moment '" + momentNames[i] + "' in its clips but never says what it " +
                    "sounds like, so nothing happens when that frame comes round. Give it a sound " +
                    "or remove the moment.");
            }

            DimensionCreatureClipTemplate[] clips = animation.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                DimensionCreatureClipTemplate clip = clips[i];
                if (clip == null || !clip.Enabled || clip.Strip == null)
                {
                    continue;
                }

                string label = DimensionCreatureAnimationNames.LabelFor(clip.Kind);
                if (clip.HoldListIsTheWrongLength)
                {
                    warn(
                        "gives its '" + label + "' clip a hold list of a different length from its " +
                        "picture count (" + clip.FrameCount + "). Give one number per picture, or " +
                        "leave the list empty.");
                }

                if (clip.HasAMomentOffTheEndOfTheStrip)
                {
                    warn(
                        "puts a moment of its '" + label + "' clip on a picture the strip does not " +
                        "have. Pictures are counted from 0, so the last one is " +
                        (clip.FrameCount - 1) + ".");
                }

                if (clip.Strip.width % clip.FrameCount != 0)
                {
                    warn(
                        "has a '" + label + "' strip " + clip.Strip.width + " pixels wide, which " +
                        "does not divide into " + clip.FrameCount + " pictures. Every picture has " +
                        "to be the same width, so trim the strip or change the picture count.");
                }
            }
        }

        private static ClipArt CopyClipArt(
            DimensionCreatureClipTemplate clip,
            string assetName,
            string artFolder,
            Action<string> warn)
        {
            string animationName = clip.AnimationName;
            string stem = assetName + "_" + animationName;
            ClipArt art = new ClipArt { Clip = clip };
            art.Base = CopyStrip(clip.Strip, artFolder + "/" + stem + ".png", clip, warn);
            if (art.Base == null)
            {
                return art;
            }

            if (clip.StripFromBehind != null)
            {
                art.Up = CopyStrip(
                    clip.StripFromBehind, artFolder + "/" + stem + "_up.png", clip, warn);
            }

            if (clip.StripFromTheSide != null)
            {
                art.Side = CopyStrip(
                    clip.StripFromTheSide, artFolder + "/" + stem + "_side.png", clip, warn);
            }

            return art;
        }

        /// <summary>
        /// Copies one authored strip to the name the game will read its clip name out of.
        /// </summary>
        /// <remarks>
        /// A source already sitting at the destination is left alone rather than copied onto
        /// itself: regenerating a creature whose art was picked from a previous generation would
        /// otherwise rewrite the file it is reading.
        /// </remarks>
        /// <summary>
        /// Copies one authored strip to the name the game will read its clip name out of.
        /// </summary>
        /// <remarks>
        /// The copying and importing itself is shared with the plant generator — the mechanism and
        /// its silent failures are identical — and only the wording of what went wrong is written
        /// here, where a clip has a name a modder would recognise.
        /// </remarks>
        private static Texture2D CopyStrip(
            Texture2D source,
            string destinationPath,
            DimensionCreatureClipTemplate clip,
            Action<string> warn)
        {
            DimensionArtCopyProblem problem;
            Texture2D copied = DimensionSpriteArtFileUtility.CopyPicture(
                source, destinationPath, out problem);
            string label = DimensionCreatureAnimationNames.LabelFor(clip.Kind);
            switch (problem)
            {
                case DimensionArtCopyProblem.NotAPng:
                    warn(
                        "draws its '" + label + "' clip with something that is not a PNG file in " +
                        "the project. Save the strip as a .png and point the clip at that.");
                    break;
                case DimensionArtCopyProblem.NotOnDisk:
                    warn(
                        "could not read its '" + label +
                        "' strip from disk, so that clip was left out.");
                    break;
                case DimensionArtCopyProblem.ImportFailed:
                    warn("could not import a copy of its '" + label + "' strip.");
                    break;
            }

            return copied;
        }

        private static void WriteSpriteAsset(
            SpriteAsset asset,
            string assetName,
            List<ClipArt> art,
            string[] moments,
            DimensionCreatureAnimationTemplate animation,
            DimensionCreatureSpriteAssetResult result)
        {
            asset.name = assetName;
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();

            SetLong(serialized, "m_address.m_low", result.AddressLow);
            SetLong(serialized, "m_address.m_high", result.AddressHigh);

            // No still picture and no static variants, exactly as the caveling ships: standing IS
            // the resting state, and a still frame beside it would only ever be seen if standing
            // were missing.
            ClearSpriteData(serialized.FindProperty("m_staticSpriteData"));
            SetArraySize(serialized.FindProperty("m_staticVariants"), 0);
            SetArraySize(serialized.FindProperty("m_positionalData"), 0);

            SerializedProperty events = serialized.FindProperty("m_events");
            SetArraySize(events, moments.Length);
            for (int i = 0; i < moments.Length; i++)
            {
                events.GetArrayElementAtIndex(i).stringValue = moments[i];
            }

            SerializedProperty animations = serialized.FindProperty("m_animations");
            SetArraySize(animations, art.Count);
            for (int i = 0; i < art.Count; i++)
            {
                WriteAnimation(
                    animations.GetArrayElementAtIndex(i),
                    art[i],
                    assetName,
                    moments,
                    animation);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteAnimation(
            SerializedProperty animationProperty,
            ClipArt art,
            string assetName,
            string[] moments,
            DimensionCreatureAnimationTemplate animation)
        {
            DimensionCreatureClipTemplate clip = art.Clip;
            string animationName = clip.AnimationName;

            SetString(animationProperty, "m_guid", AnimationGuid(assetName, animationName));

            DimensionCreatureClipKind? exit = animation.ExitFor(clip.Kind);
            SetString(
                animationProperty,
                "m_exitAnimationGuid",
                exit.HasValue
                    ? AnimationGuid(
                        assetName,
                        DimensionCreatureAnimationNames.AnimationNameFor(exit.Value))
                    : Guid.Empty.ToString());

            SerializedProperty spriteData = animationProperty.FindPropertyRelative("m_spriteData");
            ClearSpriteData(spriteData);
            if (spriteData != null)
            {
                spriteData.FindPropertyRelative("texture").objectReferenceValue = art.Base;
            }

            int frames = clip.FrameCount;
            SetInt(animationProperty, "srcFrameCount", frames);
            SetFloat(animationProperty, "fps", clip.Speed);
            SetBool(animationProperty, "loop", animation.RepeatsFor(clip));

            SerializedProperty transitions = animationProperty.FindPropertyRelative("m_transitions");
            SetArraySize(transitions, 0);

            SerializedProperty variants = animationProperty.FindPropertyRelative("m_variants");
            int variantCount = (art.Up != null ? 1 : 0) + (art.Side != null ? 1 : 0);
            SetArraySize(variants, variantCount);
            int variantIndex = 0;
            if (art.Up != null)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(variantIndex++);
                ClearSpriteData(variant);
                variant.FindPropertyRelative("texture").objectReferenceValue = art.Up;
            }

            if (art.Side != null)
            {
                SerializedProperty variant = variants.GetArrayElementAtIndex(variantIndex);
                ClearSpriteData(variant);
                variant.FindPropertyRelative("texture").objectReferenceValue = art.Side;
            }

            // One entry per picture, never fewer. The game walks this list by picture index while
            // building a clip's playback table, so a short list is an exception at load and a long
            // one is frames nobody ever sees.
            SerializedProperty frameData = animationProperty.FindPropertyRelative("frameData");
            SetArraySize(frameData, frames);
            int[] holds = clip.HoldFrames;
            int[] masks = BuildEventMasks(clip, moments, frames);
            for (int i = 0; i < frames; i++)
            {
                SerializedProperty frame = frameData.GetArrayElementAtIndex(i);
                SetInt(frame, "holdFrames", holds[i]);
                SetInt(frame, "eventMask", masks[i]);
            }
        }

        /// <summary>Which named moments fire on which picture, as one bit each.</summary>
        private static int[] BuildEventMasks(
            DimensionCreatureClipTemplate clip,
            string[] moments,
            int frames)
        {
            int[] masks = new int[frames];
            string[] names = clip.MomentNames;
            int[] pictures = clip.MomentPictures;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                int bit = Array.IndexOf(moments, names[i]);
                if (bit < 0 || bit >= DimensionCreatureAnimationNames.MaximumEvents)
                {
                    continue;
                }

                int picture = i < pictures.Length ? pictures[i] : 0;
                if (picture < 0 || picture >= frames)
                {
                    continue;
                }

                masks[picture] |= 1 << bit;
            }

            return masks;
        }

        /// <summary>
        /// The identity a clip is chained to by, derived rather than random.
        /// </summary>
        /// <remarks>
        /// Exit chaining is stored as the target clip's guid, so a freshly minted guid on every
        /// regeneration would break every chain the moment a creature was regenerated. Deriving it
        /// from the asset and clip names means the same clip always has the same identity.
        /// </remarks>
        private static string AnimationGuid(string assetName, string animationName)
        {
            string seed = assetName + ":" + animationName;
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(StableAddressPart(seed, 0x436C69704775696EUL)).CopyTo(bytes, 0);
            BitConverter.GetBytes(StableAddressPart(seed, 0x64666F72616E696DUL)).CopyTo(bytes, 8);
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

        private static void EnsureManifestContains(
            string outputFolder,
            SpriteAsset asset,
            Action<string> warn)
        {
            DimensionSpriteArtFileUtility.EnsureManifestContains(outputFolder, asset, warn);
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

        /// <summary>
        /// A stable half of a data-block address, derived from a name.
        /// </summary>
        /// <remarks>
        /// The salts are creature-specific so a creature's address can never land on a portal's or
        /// a crop's, each of which is derived the same way from its own salts.
        /// </remarks>
        public static long StableAddressPart(string value, ulong salt)
        {
            return DimensionSpriteArtFileUtility.StableAddressPart(value, salt);
        }

        /// <summary>The address a creature's sprite asset will carry, without building it.</summary>
        /// <remarks>
        /// The generated bootstrap needs the same two numbers the generator wrote, and deriving
        /// them twice from the same name is what saves the two steps having to meet.
        /// </remarks>
        public static void AddressFor(string creatureObjectName, out long low, out long high)
        {
            low = StableAddressPart(creatureObjectName, 0x4372656174757265UL);
            high = StableAddressPart(creatureObjectName, 0x616E696D6174696FUL);
        }

        /// <summary>
        /// The name this utility gives a creature's sprite asset and every picture under it.
        /// </summary>
        /// <remarks>
        /// Public because the stale-art sweep has to agree with it exactly. The sweep runs after
        /// generation, from a caller that never sees the sprite assets being written, and the only
        /// thing that keeps the two in step is that both ask this one method.
        /// </remarks>
        public static string AssetNameFor(string creatureId)
        {
            return SanitizeAssetName(creatureId);
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Creature";
            }

            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char c = characters[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }

    }
}
