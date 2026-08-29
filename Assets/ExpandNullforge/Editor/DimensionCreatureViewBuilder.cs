using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Creatures;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Builds the object Core Keeper actually draws when a generated creature is on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE PIECE THAT WAS MISSING. A creature entity carries everything needed to be told
    /// what it is doing — the animation buffer, the orientation, the state machines that write into
    /// them — but a creature with no graphical prefab has nothing on the other end of that, so it
    /// spawned, chased, attacked and killed players while being completely invisible. Only bosses
    /// ever got a prefab, and theirs was a single still picture.
    /// </para>
    /// <para>
    /// THE SHAPE IS THE CAVELING'S, PART FOR PART. A flipping node with the body sprite and the
    /// shadow beneath it, a Flashable on the root listing that body, and the animation events wired
    /// on the body itself. Anything else is a shape nobody at Pugstorm has ever run: the game
    /// reaches into these by name and by list position in several places, and the ones that would
    /// break are the quiet ones — no hit flash, no footsteps, a shadow that does not follow.
    /// </para>
    /// <para>
    /// Nothing per-creature is baked in that a pooled instance could not survive being handed on.
    /// See <see cref="DimensionCreatureView"/> for why that is a rule rather than a preference.
    /// </para>
    /// <para>
    /// THE FLIPPING NODE IS ALWAYS BUILT, even for a creature that never turns. A view that reads
    /// its facing writes a scale onto that node every frame without checking it exists, so a
    /// missing one is an exception per frame rather than a missing flip — and since the flag that
    /// decides whether facing is read is re-derived per creature from a pooled instance, there is
    /// no build-time answer to "will this one need it". Building it always is what makes the
    /// question moot.
    /// </para>
    /// </remarks>
    internal static class DimensionCreatureViewBuilder
    {
        /// <summary>
        /// The mod SDK's stand-in for the game's lit sprite material.
        /// </summary>
        /// <remarks>
        /// It is swapped for the game's own material at load, which is the sanctioned way for a mod
        /// to draw a SpriteObject: the real material is not something a mod can ship. A SpriteObject
        /// with no material at all draws nothing and says nothing about it.
        /// </remarks>
        private const string LitMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Lit.mat";

        private const string ShadowMaterialPath =
            "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/UGC SpriteObject Shadow.mat";

        /// <summary>Keeps the shadow out of the sprite sorter, the way vanilla shadows are.</summary>
        private const string ShadowSortingTag = "ExcludeFromSpriteAutoSort";

        private static readonly int IdleAnimationHash = Animator.StringToHash("idle");

        /// <summary>What a built view turned out to be.</summary>
        public sealed class Result
        {
            /// <summary>The root of the built view, before it is saved. Never null.</summary>
            public GameObject Root;

            /// <summary>
            /// The view component when the creature has a plain one, or null when it has one of
            /// the walked-up-to roles.
            /// </summary>
            /// <remarks>
            /// Null for a tended animal and a talking creature, because Core Keeper's tending and
            /// trading windows only accept a <c>Cattle</c> and an <c>NPC</c> and both of those
            /// derive from <c>EntityMonoBehaviour</c> directly. The boss nameplate path reads this,
            /// and a boss is never one of those roles.
            /// </remarks>
            public DimensionCreatureView View;

            /// <summary>
            /// The view component whatever it turned out to be, for a caller that needs to wire
            /// something onto the body itself.
            /// </summary>
            public EntityMonoBehaviour Behaviour;
        }

        /// <summary>
        /// The kind of body a creature gets, which decides its component type and its pool.
        /// </summary>
        /// <remarks>
        /// ONE COMPONENT TYPE PER DISTINCT BEHAVIOUR, and the reason is the pool rather than tidy
        /// design. Core Keeper pools graphical objects by component TYPE and hands the instance
        /// that was drawing one creature a moment ago straight to the next, so anything that
        /// differs between two creatures of the same type has to be re-derived on occupy. A boss
        /// must not share a pool with mobs: it would be handed an instance with no nameplate to
        /// render its name into. A tended animal and a talking creature must not share one with
        /// either, because their windows demand the game's own <c>Cattle</c> and <c>NPC</c> base
        /// classes and a plain creature view is neither.
        /// </remarks>
        public enum Role
        {
            /// <summary>A creature nobody walks up to.</summary>
            Plain = 0,

            /// <summary>A boss, with a floating name.</summary>
            Boss = 1,

            /// <summary>An animal a player tends and names.</summary>
            TendedAnimal = 2,

            /// <summary>A creature a player talks to and trades with.</summary>
            TalkingCreature = 3
        }

        /// <summary>
        /// Builds a creature's view in memory. The caller saves it and assigns it as the
        /// graphical prefab.
        /// </summary>
        /// <param name="isBoss">
        /// Decides the component TYPE, which decides the pool. Kept for callers that only ever
        /// build a plain creature or a boss.
        /// </param>
        public static Result Build(
            string creatureId,
            DimensionCreatureAnimationTemplate animation,
            DimensionCreatureSpriteAssetResult art,
            bool isBoss,
            Action<string> warn)
        {
            return Build(creatureId, animation, art, isBoss ? Role.Boss : Role.Plain, warn);
        }

        /// <summary>
        /// The same, for a creature whose body is one a player walks up to.
        /// </summary>
        public static Result Build(
            string creatureId,
            DimensionCreatureAnimationTemplate animation,
            DimensionCreatureSpriteAssetResult art,
            Role role,
            Action<string> warn)
        {
            DimensionCreatureAnimationTemplate clips =
                animation ?? new DimensionCreatureAnimationTemplate();

            GameObject root = new GameObject(creatureId + "Visual");

            DimensionCreatureView plain = null;
            Creatures.DimensionTendedAnimalView tended = null;
            Creatures.DimensionTalkingCreatureView talking = null;
            EntityMonoBehaviour view;

            switch (role)
            {
                case Role.Boss:
                    plain = root.AddComponent<DimensionBossView>();
                    view = plain;
                    break;

                case Role.TendedAnimal:
                    tended = root.AddComponent<Creatures.DimensionTendedAnimalView>();
                    view = tended;
                    break;

                case Role.TalkingCreature:
                    talking = root.AddComponent<Creatures.DimensionTalkingCreatureView>();
                    view = talking;
                    break;

                default:
                    plain = root.AddComponent<DimensionCreatureView>();
                    view = plain;
                    break;
            }

            GameObject scaler = new GameObject("XScaler");
            scaler.transform.SetParent(root.transform, false);
            view.XScaler = scaler.transform;

            SpriteObject body = BuildBody(scaler, clips, art, warn);
            if (plain != null)
            {
                plain.bodySprite = body;
            }
            else if (tended != null)
            {
                tended.bodySprite = body;
            }
            else if (talking != null)
            {
                talking.bodySprite = body;
            }

            view.spriteObjects = new List<SpriteObject>();
            if (body != null)
            {
                view.spriteObjects.Add(body);
            }

            // The game plays a clip's transform animation on every listed sprite object, which is
            // what keeps a shadow under a body that hops. Left off, a jumping creature's shadow
            // stays where the creature was.
            view.useSharedTransformAnimations = true;

            SpriteObject shadow = BuildShadow(scaler, clips);
            if (plain != null)
            {
                plain.shadowSprite = shadow;
            }
            else if (tended != null)
            {
                tended.shadowSprite = shadow;
            }
            else if (talking != null)
            {
                talking.shadowSprite = shadow;
            }

            if (shadow != null)
            {
                view.shadow = shadow.gameObject;
            }

            if (clips.FlashesWhenHit && body != null)
            {
                Flashable flashable = root.AddComponent<Flashable>();
                flashable.spriteObjects = new List<SpriteObject> { body };
            }

            // Written so a fresh instance is right before it is ever occupied; the view re-derives
            // both every time it is handed an entity, because a pooled one arrives holding the
            // previous creature's numbers.
            view.soundOptions = new EntityMonoBehaviour.SoundOptions();

            return new Result { Root = root, View = plain, Behaviour = view };
        }

        /// <summary>
        /// Builds, saves and attaches a plain creature's view in one call.
        /// </summary>
        /// <remarks>
        /// For the kinds with nothing extra hanging off the view — mobs, animals, critters. Bosses
        /// go the long way round because their nameplate has to be added before the prefab is
        /// saved.
        /// </remarks>
        public static bool Apply(
            GameObject root,
            string creatureId,
            string creatureObjectName,
            DimensionSpawnableVisualTemplate visual,
            string outputFolder,
            Action<string> warn)
        {
            DimensionCreatureAnimationTemplate animation = visual != null ? visual.Animation : null;
            DimensionCreatureSpriteAssetResult art = DimensionCreatureSpriteAssetUtility.Build(
                creatureId,
                creatureObjectName,
                animation,
                outputFolder,
                warn);
            if (!art.HasAsset)
            {
                // Cleared rather than left alone: an author who deletes the last clip has said the
                // creature has no body, and a prefab left over from the previous generation would
                // keep drawing art nothing in the project still describes.
                ObjectAuthoring cleared = root.GetComponent<ObjectAuthoring>();
                if (cleared != null)
                {
                    cleared.graphicalPrefab = null;
                }

                warn(
                    "has nothing drawn for it, so it will be invisible in the world. Add at least " +
                    "a 'Standing' clip to its Visual template.");
                return false;
            }

            Result built = Build(creatureId, animation, art, false, warn);
            try
            {
                string viewPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(creatureId, "Creature") + "Visual.prefab";
                GameObject saved = SaveInsideBatch(built.Root, viewPath);
                if (saved == null)
                {
                    warn("could not save its view prefab, so it has no body this build.");
                    return false;
                }

                ObjectAuthoring authoring = root.GetComponent<ObjectAuthoring>();
                if (authoring != null)
                {
                    authoring.graphicalPrefab = saved;
                }

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(built.Root);
            }
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

        private static SpriteObject BuildBody(
            GameObject scaler,
            DimensionCreatureAnimationTemplate clips,
            DimensionCreatureSpriteAssetResult art,
            Action<string> warn)
        {
            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(scaler.transform, false);
            SpriteObject body = bodyObject.AddComponent<SpriteObject>();

            Material material = clips.BodyMaterial != null
                ? clips.BodyMaterial
                : AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (material == null)
            {
                warn(
                    "has no material to draw its body with, so it will be invisible even with art. " +
                    "Leave the material empty on its Visual template to use the standard one.");
            }

            body.material = material;
            body.color = Color.white;
            body.emissiveColor = Color.white;

            bool hasMoments = clips.CollectMomentNames().Length > 0;
            body.processAnimationEvents = hasMoments;

            SetAssetAddress(
                body,
                art != null ? art.AddressLow : 0L,
                art != null ? art.AddressHigh : 0L,
                IdleAnimationHash,
                0);

            // The game's own SpriteObjectAnimationEventEffects is deliberately NOT added here. It
            // reads a table of sounds, puffs and flashes that this framework has no way for an
            // author to fill, so it would sit on every creature subscribing to events and doing
            // nothing with them. The view listens for the same events itself and plays the sound
            // the author gave each moment, which is the part that has an authoring surface.
            return body;
        }

        private static SpriteObject BuildShadow(
            GameObject scaler,
            DimensionCreatureAnimationTemplate clips)
        {
            if (clips.Shadow == DimensionCreatureShadowSize.None)
            {
                return null;
            }

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(scaler.transform, false);
            shadowObject.tag = ShadowSortingTag;

            SpriteObject shadow = shadowObject.AddComponent<SpriteObject>();
            shadow.material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
            shadow.color = Color.white;

            // The shadow is not art this mod ships. Every creature in the game points at one shared
            // asset and picks a size out of it, and these two numbers are that asset's address.
            SetAssetAddress(
                shadow,
                DimensionCreatureAnimationNames.SharedShadowAddressLow,
                DimensionCreatureAnimationNames.SharedShadowAddressHigh,
                0,
                DimensionCreatureAnimationNames.ShadowVariantHashFor(clips.Shadow));

            return shadow;
        }

        /// <summary>
        /// Points a sprite object at an asset by address, which is the only way to reference one
        /// the mod does not own.
        /// </summary>
        /// <remarks>
        /// The reference, the starting clip and the starting size are all private serialized
        /// fields with no way in from code, so a serialized object is not a shortcut here — it is
        /// the only door.
        /// </remarks>
        private static void SetAssetAddress(
            SpriteObject spriteObject,
            long addressLow,
            long addressHigh,
            int startAnimationHash,
            int startVariantHash)
        {
            SerializedObject serialized = new SerializedObject(spriteObject);
            serialized.Update();
            SetLong(serialized, "m_assetRef.m_address.m_low", addressLow);
            SetLong(serialized, "m_assetRef.m_address.m_high", addressHigh);
            SetInt(serialized, "m_startAnimationHash", startAnimationHash);
            SetInt(serialized, "m_startVariantHash", startVariantHash);
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
