using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionExplosionGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Explosions: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns a handful of questions about a blast into the object Core Keeper spawns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The component list is taken from vanilla rather than invented. The interesting one is only
    /// the first. The rest are what make it behave like a blast rather than like a dropped item —
    /// <c>DontDropSelfAuthoring</c> so it does not fall on the floor when it dies,
    /// <c>DontSerializeAuthoring</c> so it is not saved into the world,
    /// <c>CantBeAttackedAuthoring</c> so nothing targets it, and
    /// <c>DestroyTimerAuthoring</c> so a blast that hits nothing eventually stops existing.
    /// </para>
    /// <para>
    /// <c>BehaviourTagsAuthoring</c> is the one that fails silently and so is added by name rather
    /// than left to <c>ExplosionAuthoring</c>'s own <c>[RequireComponent]</c>. The game's damage
    /// pass queries for <c>BehaviourTagsCD</c> alongside <c>ExplosionCD</c>
    /// (<c>ck-db\Pug.Other\ExplosionDamageSystem.cs:707-712</c>): without it the blast appears,
    /// makes its noise, and does nothing at all, with no error anywhere.
    /// </para>
    /// <para>
    /// Leaving any of the others off is not a small omission either. Without the timer, every blast
    /// in the mod stays in the world forever; without the serialize opt-out they are saved into the
    /// world file and come back on load.
    /// </para>
    /// </remarks>
    internal static class DimensionExplosionGenerator
    {
        public const string FolderName = "Explosions";

        public static DimensionExplosionGenerationReport Generate(
            IEnumerable<DimensionExplosionAsset> explosions,
            string outputFolder,
            DimensionNamingContext naming)
        {
            DimensionExplosionGenerationReport report = new DimensionExplosionGenerationReport();
            if (explosions == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no explosions were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionExplosionAsset explosion in explosions)
                {
                    GenerateOne(explosion, outputFolder, naming, report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static void GenerateOne(
            DimensionExplosionAsset explosion,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionExplosionGenerationReport report)
        {
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(explosion.ExplosionId))
            {
                report.Skipped.Add("An explosion with no id was skipped.");
                return;
            }


            if (explosion.NeverGoesAway)
            {
                report.Warnings.Add(
                    "'" + explosion.DisplayName + "' has no lifetime, so it stays where it went off, " +
                    "doing its damage, for as long as the world is loaded.");
            }

            if (explosion.ReachesNothing)
            {
                report.Warnings.Add(
                    "'" + explosion.DisplayName + "' has a radius of zero, so it catches nothing.");
            }

            string prefabPath =
                outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(explosion.ExplosionId, "Explosion") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(explosion.ExplosionId);

            bool written = false;
            try
            {
                Configure(root, explosion, naming, report);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                written = true;
                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    explosion.ExplosionId + " failed to generate: " + exception.Message +
                    "\n" + exception.StackTrace);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else if (written)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            DimensionExplosionAsset explosion,
            DimensionNamingContext naming,
            DimensionExplosionGenerationReport report)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(explosion.ExplosionId);
            obj.objectType = ObjectType.NonObtainable;
            obj.initialAmount = 1;

            ExplosionAuthoring blast = EnsureComponent<ExplosionAuthoring>(root);
            blast.radius = explosion.Radius;

            // Zero on purpose, and not offered on the asset. Whatever sets this blast off writes
            // its own numbers over these before the blast acts, so anything put here would only
            // ever be read by somebody reading the prefab.
            blast.damage = 0;
            blast.tileDamage = 0;

            // Named rather than left to ExplosionAuthoring's [RequireComponent]: a required
            // component that arrives by side effect is one refactor away from not arriving, and
            // this one's absence is invisible.
            EnsureComponent<BehaviourTagsAuthoring>(root);

            int napalm = explosion.NapalmVariation;
            if (napalm >= 0)
            {
                EnsureComponent<ExpandNullforge.Explosives.DimensionBlastFireAuthoring>(root)
                    .napalmVariation = napalm;
            }
            else
            {
                RemoveComponentIfPresent<ExpandNullforge.Explosives.DimensionBlastFireAuthoring>(root);
            }

            // The same four an explosion needs for the same reasons a projectile does: it must
            // stop existing, must not be saved into the world, must not be a target, and must not
            // leave itself on the floor.
            DestroyTimerAuthoring timer = EnsureComponent<DestroyTimerAuthoring>(root);
            timer.lifetime = new Pug.UnityExtensions.PlatformDependentValue<float>(
                explosion.LifetimeSeconds);

            EnsureComponent<DontDropSelfAuthoring>(root);
            EnsureComponent<DontSerializeAuthoring>(root);
            EnsureComponent<CantBeAttackedAuthoring>(root);
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

            DimensionObjectSpine.ApplyImpactFeedback(
                root,
                explosion.Feedback,
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        "'" + explosion.DisplayName + "' throws particle burst '" + unknown +
                        "', which the game does not have.");
                });
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
