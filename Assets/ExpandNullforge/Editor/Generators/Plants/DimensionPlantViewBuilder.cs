using System.Collections.Generic;
using ExpandNullforge.Plants;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Builds the object Core Keeper actually draws when a generated crop is in the ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO PREFABS FOR A WHOLE MOD, NOT TWO PER CROP, AND THAT IS THE MEASURED SHAPE. Core Keeper
    /// pools graphical objects by the TYPE of the component on them, so a prefab per crop with that
    /// crop's art baked in would still be handed to the next crop that came on screen. Core
    /// Keeper's own answer is one shared <c>Plant.prefab</c> and one shared <c>Seed.prefab</c>,
    /// whose skin is re-chosen per entity from a list of conditions; the framework does the same
    /// thing with its own registry, so the prefabs carry no art at all and every crop points at
    /// both of them.
    /// </para>
    /// <para>
    /// THE SHAPE IS PLANT.PREFAB'S, PART FOR PART: a flipping node holding the plant sprite, a
    /// shadow drawing the same pictures in flat black, and a soft blob of indirect light. The game
    /// reaches into these by name and by list position in several places, and the ones that would
    /// break quietly are the ones that matter — a shadow that does not follow, a glow that cannot
    /// be switched off, a plant that does not shake when a player walks through it.
    /// </para>
    /// <para>
    /// THE FLIPPING NODE IS ALWAYS BUILT, even though no crop turns to face anything. Anything that
    /// reads an entity's facing writes a scale onto that node without checking it exists, so a
    /// missing one is an exception per frame rather than a missing flip.
    /// </para>
    /// </remarks>
    internal static class DimensionPlantViewBuilder
    {
        /// <summary>The file the shared plant body is written to, inside a plant output folder.</summary>
        public const string PlantVisualName = "PlantVisual";

        /// <summary>The file the shared seed body is written to.</summary>
        public const string SeedVisualName = "SeedVisual";

        /// <summary>
        /// The mod SDK's stand-in for the game's lit sprite material.
        /// </summary>
        /// <remarks>
        /// Swapped for the game's own material at load, which is the sanctioned way for a mod to
        /// draw a SpriteObject: the real material is not something a mod can ship. A SpriteObject
        /// with no material at all draws nothing and says nothing about it.
        /// </remarks>
        private const string LitMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Lit.mat";

        private const string UnlitMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Unlit.mat";

        private const string ShadowMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Shadow.mat";

        private const string IndirectLightMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject IndirectLight.mat";

        /// <summary>Keeps the shadow out of the sprite sorter, the way vanilla shadows are.</summary>
        private const string ShadowSortingTag = "ExcludeFromSpriteAutoSort";

        /// <summary>
        /// The address of the soft white blob every glowing object in the game throws light with.
        /// </summary>
        /// <remarks>
        /// Not art this mod ships: read straight off <c>Plant.prefab</c>'s indirect light sprite,
        /// which points at <c>Data/SpriteAsset/white16x16_soft.asset</c>.
        /// </remarks>
        private const long IndirectLightAddressLow = 1969411019982982381L;

        private const long IndirectLightAddressHigh = 3364034926224641816L;

        /// <summary>
        /// Where the plant sits relative to its tile, measured off <c>Plant.prefab</c>.
        /// </summary>
        /// <remarks>
        /// The z pushes it towards the camera so it draws in front of the ground it stands on; the
        /// tiny y lifts it off the tile line. These are not tuning knobs — they are the offsets
        /// every crop in the game already uses, and moving them makes a modded crop sort against
        /// the floor differently from the one planted beside it.
        /// </remarks>
        private static readonly Vector3 BodyOffset = new Vector3(0f, 0.015625f, -0.3125f);

        private static readonly Vector3 ShadowOffset = new Vector3(0f, 0.02f, -0.375f);

        private static readonly Vector3 GlowOffset = new Vector3(0f, 0f, -0.3125f);

        /// <summary>
        /// Builds and saves the two shared bodies, if they are not already there.
        /// </summary>
        /// <returns>
        /// The plant body and the seed body, either of which may be null when saving failed.
        /// </returns>
        public static void EnsureVisuals(
            string outputFolder,
            out GameObject plantVisual,
            out GameObject seedVisual)
        {
            plantVisual = EnsureVisual(outputFolder, PlantVisualName, false);
            seedVisual = EnsureVisual(outputFolder, SeedVisualName, true);
        }

        private static GameObject EnsureVisual(string outputFolder, string fileName, bool isSeed)
        {
            string path = outputFolder + "/" + fileName + ".prefab";

            // Rebuilt every generation rather than reused when present. The wiring inside is this
            // generator's output, not an author's file, and an older one left over from a previous
            // version of the framework would be missing whatever was added since — silently, since
            // a missing sprite reference draws nothing rather than erroring.
            GameObject root = Build(fileName, isSeed);
            try
            {
                return SaveInsideBatch(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Builds one shared body in memory. The caller saves it.</summary>
        public static GameObject Build(string name, bool isSeed)
        {
            GameObject root = new GameObject(name);
            DimensionPlantView view = isSeed
                ? root.AddComponent<DimensionSeedView>()
                : root.AddComponent<DimensionPlantView>();

            GameObject scaler = new GameObject("XScaler");
            scaler.transform.SetParent(root.transform, false);
            view.XScaler = scaler.transform;

            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            view.litMaterial = lit;
            view.unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            SpriteObject body = AddSprite(scaler, "SpriteObject", BodyOffset, lit, Color.white);
            view.plantSprite = body;

            SpriteObject shadow = AddSprite(
                scaler,
                "SpriteObject (Shadow)",
                ShadowOffset,
                AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath),
                DimensionPlantView.ShadowColour);
            shadow.gameObject.tag = ShadowSortingTag;
            view.shadowSprite = shadow;

            // Listed on the entity itself as well, which is what makes the game play a clip's
            // transform animation on both together. Left off, a plant that pops as it grows leaves
            // its shadow behind.
            view.spriteObjects = new List<SpriteObject> { body, shadow };
            view.useSharedTransformAnimations = true;

            if (!isSeed)
            {
                SpriteObject glow = AddSprite(
                    scaler,
                    "IndirectLightSprite",
                    GlowOffset,
                    AssetDatabase.LoadAssetAtPath<Material>(IndirectLightMaterialPath),
                    Color.black);
                SetAssetAddress(glow, IndirectLightAddressLow, IndirectLightAddressHigh);

                // Off until a crop that glows is put into it. A blob left on would light the cave
                // around every plain crop with the last glowing one's colour.
                glow.gameObject.SetActive(false);
                view.glowSprite = glow;
            }

            // Written so a fresh instance is right before it is ever occupied. Plants take damage
            // and die like anything else, and the game plays whatever is in here when they do.
            view.soundOptions = new EntityMonoBehaviour.SoundOptions();
            return root;
        }

        private static SpriteObject AddSprite(
            GameObject parent,
            string name,
            Vector3 offset,
            Material material,
            Color colour)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent.transform, false);
            node.transform.localPosition = offset;

            SpriteObject sprite = node.AddComponent<SpriteObject>();
            sprite.material = material;
            sprite.color = colour;
            sprite.emissiveColor = Color.black;

            // No crop has a named moment inside its pictures — there is nowhere to author one and
            // nothing that would listen — so the per-frame event walk is left switched off.
            sprite.processAnimationEvents = false;
            return sprite;
        }

        /// <summary>
        /// Points a sprite object at an asset by address, which is the only way to reference one
        /// the mod does not own.
        /// </summary>
        /// <remarks>
        /// The reference and the starting animation are private serialized fields with no way in
        /// from code, so a serialized object is not a shortcut here — it is the only door.
        /// </remarks>
        private static void SetAssetAddress(
            SpriteObject spriteObject,
            long addressLow,
            long addressHigh)
        {
            SerializedObject serialized = new SerializedObject(spriteObject);
            serialized.Update();
            SetLong(serialized, "m_assetRef.m_address.m_low", addressLow);
            SetLong(serialized, "m_assetRef.m_address.m_high", addressHigh);
            SetInt(serialized, "m_startAnimationHash", 0);
            SetInt(serialized, "m_startVariantHash", 0);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Saves a prefab while a generator's asset batch is open, and hands back the saved asset.
        /// </summary>
        /// <remarks>
        /// Inside StartAssetEditing, SaveAsPrefabAsset returns null because the asset has not
        /// imported yet — fatal here, where the returned reference is the whole point. The batch
        /// pauses for exactly this one save and is reopened, so the caller's own StopAssetEditing
        /// still closes the window it opened.
        /// </remarks>
        public static GameObject SaveInsideBatch(GameObject root, string path)
        {
            AssetDatabase.StopAssetEditing();
            try
            {
                bool saved;
                GameObject written = PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
                if (saved && written != null)
                {
                    return written;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
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

        private static void SetInt(SerializedObject serialized, string path, int value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null)
            {
                property.intValue = value;
            }
        }
    }
}
