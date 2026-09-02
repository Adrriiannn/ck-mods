using ExpandNullforge.Authoring;
using ExpandNullforge.Explosives;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Writes the second half of a bomb — the blast it turns into — so an author only makes one
    /// thing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS NOT OPTIONAL. Core Keeper's explosion is a separate object with its own
    /// <c>ObjectID</c>, and the numbers are split across the pair: reach is authored on the blast
    /// and used as written, while damage and terrain damage are copied onto the blast from the bomb
    /// every time it goes off (<c>ck-db\Pug.Other\ExplosiveSystem.cs:175-176</c>). Asking an author
    /// to make and wire two objects to get one bomb is the kind of assembly this framework exists to
    /// remove.
    /// </para>
    /// <para>
    /// THE FOUR COMPONENTS THE GAME'S DAMAGE PASS QUERIES FOR are <c>ExplosionCD</c>,
    /// <c>BehaviourTagsCD</c>, <c>GhostInstance</c> and <c>ObjectDataCD</c>
    /// (<c>ck-db\Pug.Other\ExplosionDamageSystem.cs:707-712</c>). Miss any of them and the blast is
    /// spawned, is visible for a moment, and does nothing at all — with no error. Behaviour tags
    /// come with <c>ExplosionAuthoring</c>'s own <c>[RequireComponent]</c> and are added explicitly
    /// here as well, because a required component that arrives by side effect is one refactor away
    /// from not arriving.
    /// </para>
    /// <para>
    /// The blast does NOT carry damage numbers of its own. Numbers here would be a lie: every
    /// path a mod can reach — a placed bomb, a chained charge, a creature's death rattle — writes
    /// its own numbers over them before the blast acts.
    /// </para>
    /// </remarks>
    internal static class DimensionExplosiveBlast
    {
        /// <summary>How long the generated blast object lives, in seconds.</summary>
        /// <remarks>
        /// VERIFIED: <c>ExplosionEntity.prefab</c> authors <c>lifetime.pc = 1</c>. The game adds the
        /// explosion delay on top when it spawns one (<c>ExplosiveSystem.cs:169-174</c>), so this is
        /// how long the puff is on screen, not how long the damage lasts.
        /// </remarks>
        public const float LifetimeSeconds = 1f;

        /// <summary>The suffix the generated blast's id takes after the bomb's own.</summary>
        public const string IdSuffix = "Blast";

        /// <summary>The blast object id for a bomb, in the author's own unqualified terms.</summary>
        public static string BlastIdFor(string itemId)
        {
            return string.IsNullOrEmpty(itemId) ? string.Empty : itemId + IdSuffix;
        }

        /// <summary>
        /// Writes (or rewrites) the blast prefab for one bomb and returns its unqualified id.
        /// </summary>
        /// <remarks>
        /// Returns empty when the item is not a bomb or names a blast of its own, so the caller can
        /// use the return value to decide whether there is anything to register or to keep alive
        /// through the orphan sweep.
        /// </remarks>
        public static string Write(
            DimensionItemAsset item,
            string outputFolder,
            DimensionNamingContext naming,
            System.Action<string> reportWarning,
            System.Action<string> reportError,
            System.Action<string> reportCreated,
            System.Action<string> reportUpdated)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId))
            {
                return string.Empty;
            }

            DimensionExplosiveTemplate explosive = item.Explosive;
            if (!explosive.MakesItsOwnBlast)
            {
                return string.Empty;
            }

            string blastId = BlastIdFor(item.ItemId);
            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(blastId, "Blast") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(blastId);

            bool written = false;
            try
            {
                Configure(root, blastId, explosive, naming);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                written = true;
                if (updating && reportUpdated != null)
                {
                    reportUpdated(prefabPath);
                }
                else if (!updating && reportCreated != null)
                {
                    reportCreated(prefabPath);
                }
            }
            catch (System.Exception exception)
            {
                if (reportError != null)
                {
                    reportError(
                        "the blast for '" + item.ItemId + "' failed to generate: " +
                        exception.Message);
                }

                blastId = string.Empty;
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

            if (written && explosive.ExplodesAndReachesNothing && reportWarning != null)
            {
                reportWarning(
                    "'" + item.ItemId + "' makes a blast that reaches nothing, so it goes off and " +
                    "catches nobody. Vanilla's ordinary bomb reaches " +
                    DimensionExplosiveTemplate.OrdinaryBlastReach + " tiles.");
            }

            return blastId;
        }

        private static void Configure(
            GameObject root,
            string blastId,
            DimensionExplosiveTemplate explosive,
            DimensionNamingContext naming)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(blastId);

            // NonObtainable, matching vanilla's own explosion objects: it is spawned, never held,
            // never crafted, and must not show up in any list a player can reach.
            obj.objectType = ObjectType.NonObtainable;
            obj.initialAmount = 1;

            ExplosionAuthoring blast = EnsureComponent<ExplosionAuthoring>(root);
            blast.radius = explosive.BlastReach;

            // Zero on purpose. The bomb's numbers are written over these the moment it goes off, so
            // anything put here would only ever be read by somebody reading the prefab.
            blast.damage = 0;
            blast.tileDamage = 0;

            // Named explicitly rather than left to ExplosionAuthoring's [RequireComponent]: without
            // BehaviourTagsCD the blast is outside the damage pass's query and does nothing.
            EnsureComponent<BehaviourTagsAuthoring>(root);

            DestroyTimerAuthoring timer = EnsureComponent<DestroyTimerAuthoring>(root);
            timer.lifetime = new Pug.UnityExtensions.PlatformDependentValue<float>(LifetimeSeconds);

            // The same three every vanilla explosion carries: it must not fall on the floor as an
            // item when it dies, must not be written into the save, and must not be a target.
            EnsureComponent<DontDropSelfAuthoring>(root);
            EnsureComponent<DontSerializeAuthoring>(root);
            EnsureComponent<FactionAuthoring>(root);

            // THE FOURTH COMPONENT THE GAME'S DAMAGE PASS QUERIES FOR. ExplosionDamageSystem's
            // query is ExplosionCD, BehaviourTagsCD, GhostInstance and ObjectDataCD; this file's
            // own header has said so for a long time and the generator wrote three of the four. A
            // blast without it goes off, shows its puff, and hurts nothing at all. Every one of the
            // game's own explosion prefabs carries the ghost.
            DimensionQueryCompanions.IsSentToPlayers(root);

            // WHAT MAKES A POISON BOMB POISONOUS. The attacker for an explosion is the explosion
            // itself, and the game only reads an attacker's conditions when the attacker carries
            // the three condition buffers — which come from nowhere but this component. It is also
            // where the blast picks up the player's explosive-damage and blast-radius bonuses.
            // The game's own ExplosionEntity carries it, set to ignore auras, weather and healing,
            // which is right for something that exists for a third of a second.
            SupportsConditionsAuthoring conditions = EnsureComponent<SupportsConditionsAuthoring>(root);
            conditions.cantBeAffectedByAuras = true;
            conditions.cantBeAffectedByEnvironment = true;
            conditions.cantBeAffectedByHealing = true;

            // What makes the grass and the props move when it goes off, the way the game's own
            // blasts do. Cosmetic, and its own system's whole query is this and a position.
            EnsureComponent<InteractWithEnvironmentAuthoring>(root);

            int napalm = explosive.NapalmVariation;
            if (napalm >= 0)
            {
                EnsureComponent<DimensionBlastFireAuthoring>(root).napalmVariation = napalm;
            }
            else
            {
                RemoveComponentIfPresent<DimensionBlastFireAuthoring>(root);
            }
        }

        /// <summary>
        /// Takes a component off unless something on the object declares that it needs it.
        /// </summary>
        /// <remarks>
        /// Routed through the one dependency-aware removal, which says so in the console when a
        /// RequireComponent blocks it. Destroying the component outright is harmless while
        /// nothing here is required by anything, and a silent stale value the first time one
        /// is — which is the shape of bug this framework keeps finding.
        /// </remarks>
        private static void RemoveComponentIfPresent<T>(GameObject root) where T : Component
        {
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }
        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T existing = root.GetComponent<T>();
            return existing != null ? existing : root.AddComponent<T>();
        }


    }
}
