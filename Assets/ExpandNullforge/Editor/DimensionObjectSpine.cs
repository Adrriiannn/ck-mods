using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The components Core Keeper puts on essentially every object of a given kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Measured across the game's prefabs, a handful of components sit on almost
    /// everything — <c>AnimationAuthoring</c> on 1,446, <c>AreaLevelAuthoring</c> on 1,184,
    /// <c>IgnoreVertexOffsetsAuthoring</c> on 837 — and the framework was writing none of them. The
    /// result is objects that generate cleanly and then behave subtly unlike their vanilla
    /// neighbours: a chest that cannot be painted or rotated and that conveyors cannot feed, a plant
    /// a shovel will not lift, a creature whose damage cannot scale with where it spawned.
    /// </para>
    /// <para>
    /// It lives in one place rather than being repeated in each generator so the answer to "what does
    /// every object need" has exactly one definition. Each generator still decides which spine
    /// applies to the kind of thing it makes.
    /// </para>
    /// <para>
    /// ORDERING MATTERS for <see cref="ApplyAreaLevel"/>: <c>AreaLevelAuthoring.CalculateLevel</c>
    /// looks for an <c>EntityMonoBehaviourData</c> and falls back to <c>ObjectAuthoring</c>, warning
    /// to the console when it finds neither. Call it after the object's identity is written.
    /// </para>
    /// </remarks>
    internal static partial class DimensionObjectSpine
    {

        /// <summary>Adds or removes a component that carries nothing but its own presence.</summary>
        private static void Toggle<T>(GameObject root, bool wanted) where T : Component
        {
            if (wanted)
            {
                EnsureComponent<T>(root);
            }
            else
            {
                RemoveComponentIfPresent<T>(root);
            }
        }

        /// <summary>
        /// Tells the author what a tick they turned on will and will not do.
        /// </summary>
        /// <remarks>
        /// <para>
        /// SILENCE IS THE DEFECT, not the gap. Four censuses against the game's own systems found a
        /// group of answers that write exactly the component they say they write, convert cleanly,
        /// and then do nothing — because Core Keeper reads that component only off a player, only
        /// off an item somebody is holding, or in a few cases reads it nowhere at all. None of that
        /// can be fixed from here; what can be fixed is a control that says nothing while it does
        /// nothing. Every sentence below names what the author ticked, what will happen, and where
        /// to put the answer instead when there is somewhere to put it.
        /// </para>
        /// <para>
        /// It fires on every generate rather than once, because a generation report is read per
        /// run and a warning that appeared only the first time would be invisible to whoever picks
        /// the project up next.
        /// </para>
        /// </remarks>
        private static void SayWhenTicked(bool ticked, System.Action<string> report, string sentence)
        {
            if (ticked && report != null)
            {
                report(sentence);
            }
        }

        /// <summary>Whether a component with this type name is on the object.</summary>
        /// <remarks>
        /// By name rather than by type, so a check can name a component from an assembly this file
        /// does not reference without widening the assembly graph for one test.
        /// </remarks>
        private static bool HasNamed(GameObject root, string typeName)
        {
            if (root == null)
            {
                return false;
            }

            Component[] present = root.GetComponents<Component>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] != null && present[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds or removes one of the game's category tags without disturbing the others.
        /// </summary>
        /// <remarks>
        /// The tag list is what the game's slot rules, filters and creature diets all read, and an
        /// object may legitimately carry several. Rewriting the list would silently drop whatever
        /// another pass had already put there, so this only ever touches the one tag it is asked
        /// about — and it removes as well as adds, so an item that stops being food stops being
        /// accepted by the pot.
        /// </remarks>
        public static void SetCategoryTag(GameObject root, ObjectCategoryTag tag, bool wanted)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring == null)
            {
                return;
            }

            if (objectAuthoring.tags == null)
            {
                objectAuthoring.tags = new System.Collections.Generic.List<ObjectCategoryTag>();
            }

            bool present = objectAuthoring.tags.Contains(tag);
            if (wanted && !present)
            {
                objectAuthoring.tags.Add(tag);
            }
            else if (!wanted && present)
            {
                objectAuthoring.tags.Remove(tag);
            }
        }

        /// <summary>Turns a biome the author typed into the game's own, leaving it alone if blank.</summary>
        private static Biome ResolveBiomeName(
            string name,
            Biome fallback,
            string whichWorld,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(name))
            {
                return fallback;
            }

            Biome biome;
            if (System.Enum.TryParse(name, false, out biome))
            {
                return biome;
            }

            if (report != null)
            {
                report(
                    "is placed in '" + name + "' in a " + whichWorld + " world, which is not a " +
                    "biome the game has, so that world places it wherever it would have anyway.");
            }

            return fallback;
        }

        /// <summary>
        /// Turns a condition an author typed into the number the game will know it by.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWO KINDS OF ANSWER ARE VALID HERE. One of Core Keeper's own 357 conditions, named as the
        /// game names it, and one of this mod's own, named as its asset names it. An author should
        /// not have to know which kind they are looking at, so both are tried and the game's own
        /// wins a tie — a mod cannot shadow a vanilla condition by naming one after it.
        /// </para>
        /// <para>
        /// The custom half only answers once the mod's condition assets have been claimed, which is
        /// what <c>DimensionConditionScope</c> does around a generate. Outside that, a custom name
        /// resolves to nothing and is reported like any other name the game does not have.
        /// </para>
        /// </remarks>
        public static bool TryResolveCondition(string name, out ConditionID id)
        {
            id = ConditionID.None;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (System.Enum.TryParse(name, false, out id) && id != ConditionID.None)
            {
                return true;
            }

            ConditionID custom = ExpandNullforge.Conditions.DimensionConditionRegistry.IdFor(name);
            if (custom != ConditionID.None)
            {
                id = custom;
                return true;
            }

            id = ConditionID.None;
            return false;
        }

        /// <summary>A Vector3 as the game's own float3.</summary>
        private static Unity.Mathematics.float3 ToFloat3(Vector3 value)
        {
            return new Unity.Mathematics.float3(value.x, value.y, value.z);
        }

        /// <summary>A tileset by our name, then by the game's own, or -1.</summary>
        private static int ResolveTilesetName(
            string tilesetId,
            System.Func<string, int> resolveTileset)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return -1;
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved >= 0)
            {
                return resolved;
            }

            PugTilemap.Tileset named;
            return System.Enum.TryParse(tilesetId, false, out named) ? (int)named : -1;
        }

        /// <summary>Resolves a tileset name, or -1 with a report when it is not one.</summary>
        private static int ResolveTilesetOrReport(
            string tilesetId,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return -1;
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved < 0 && report != null)
            {
                report(
                    "'" + tilesetId + "' is named as a tile to leave behind, but it is neither one " +
                    "of this mod's tilesets nor one of the game's, so nothing will be left there.");
            }

            return resolved;
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>
        /// Removes a component unless another component on the object declares it as required.
        /// </summary>
        /// <returns>
        /// True when the object no longer has the component. False when something requires it, in
        /// which case the caller has to neutralise it — clear its lists, zero its numbers — instead.
        /// </returns>
        /// <remarks>
        /// <para>
        /// GENERATION IS AUTHORITATIVE: a value the author turned off is removed rather than left
        /// behind. Unity's <c>RequireComponent</c> is the one case where that is not allowed —
        /// <c>DestroyImmediate</c> refuses, logs an error, and leaves the component in place with
        /// whatever it had before.
        /// </para>
        /// <para>
        /// Found by the test suite the moment pursuit started adding <c>ChaseStateAuthoring</c>,
        /// which requires <c>BehaviourTagsAuthoring</c> — a creature with no attack tags then tried
        /// to remove a component its own chase depended on. It had been latent all along: the
        /// removal only ever succeeded because nothing that required those components was present.
        /// </para>
        /// </remarks>
        public static bool TryRemoveComponent<T>(GameObject root)
            where T : Component
        {
            if (root == null)
            {
                return true;
            }

            T component = root.GetComponent<T>();
            if (component == null)
            {
                return true;
            }

            if (SomethingRequires(root, typeof(T)))
            {
                // IT USED TO BE SILENT, and the comment on RemoveComponentIfPresent said the
                // opposite. Every caller discards this answer, so a blocked removal left the
                // component on the object carrying the PREVIOUS generate's fields with nothing said
                // anywhere — the same shape as the stale-flag bugs this framework keeps finding,
                // reached by a different road. It is said here because saying it at every one of
                // the several hundred call sites is not something a person would keep up.
                Debug.LogWarning(
                    "[ExpandNullforge] '" + root.name + "' still has " + typeof(T).Name +
                    " after generation asked for it to be taken off: something else on it requires " +
                    "that component, so it keeps whatever the last generate left in it.");
                return false;
            }

            Object.DestroyImmediate(component, true);
            return true;
        }

        private static bool SomethingRequires(GameObject root, System.Type required)
        {
            Component[] present = root.GetComponents<Component>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] == null || required.IsInstanceOfType(present[i]))
                {
                    continue;
                }

                object[] attributes = present[i]
                    .GetType()
                    .GetCustomAttributes(typeof(RequireComponent), true);
                for (int a = 0; a < attributes.Length; a++)
                {
                    RequireComponent rule = (RequireComponent)attributes[a];
                    if (Requires(rule.m_Type0, required) ||
                        Requires(rule.m_Type1, required) ||
                        Requires(rule.m_Type2, required))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Requires(System.Type declared, System.Type required)
        {
            return declared != null && declared.IsAssignableFrom(required);
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, which says so in the console when a
            // RequireComponent blocks it. The answer is discarded here on purpose: there is nothing
            // this method can do about it, and the one place that knows is the one that says it.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }
    }
}
