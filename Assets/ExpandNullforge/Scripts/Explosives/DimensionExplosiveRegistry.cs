using System;
using System.Collections.Generic;

namespace ExpandNullforge.Explosives
{
    /// <summary>
    /// One bomb a mod added and the blast it turns into, both named rather than numbered.
    /// </summary>
    /// <remarks>
    /// Names, not object ids, for the reason every other named link in this framework exists:
    /// <c>ExplosiveAuthoring.explosionID</c> is a raw <c>ObjectID</c> and a mod's object ids are
    /// handed out while the game loads, so the editor has no number to bake. The generator bakes the
    /// number directly when the blast is one of the GAME's own objects and leaves the name here
    /// otherwise; the hydration system is what turns the name into the number.
    /// </remarks>
    public sealed class DimensionExplosiveDefinition
    {
        public DimensionExplosiveDefinition(
            string bombObjectName,
            string blastObjectName,
            int blastVariation)
        {
            BombObjectName = bombObjectName ?? string.Empty;
            BlastObjectName = blastObjectName ?? string.Empty;
            BlastVariation = blastVariation < 0 ? 0 : blastVariation;
        }

        /// <summary>The qualified object name of the bomb the player places.</summary>
        public string BombObjectName { get; }

        /// <summary>The qualified object name of the blast it turns into.</summary>
        public string BlastObjectName { get; }

        /// <summary>Which variation of that blast object.</summary>
        public int BlastVariation { get; }
    }

    /// <summary>
    /// Every bomb this mod adds whose blast is one of the mod's own objects, filled by the
    /// generated bootstrap at load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THIS PREVENTS IS SILENT. <c>ExplosiveSystem.CreateExplosion</c> spawns the named
    /// object and then bails with no log at all if what it spawned carries no <c>ExplosionCD</c>
    /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:88-91</c>) — and <c>ObjectID.None</c> spawns nothing.
    /// A bomb whose blast never resolved therefore vanishes when its fuse runs out and does
    /// absolutely nothing, with no error anywhere. Registering the pair is what stops that.
    /// </para>
    /// </remarks>
    public static class DimensionExplosiveRegistry
    {
        private static readonly List<DimensionExplosiveDefinition> Definitions =
            new List<DimensionExplosiveDefinition>();

        public static IReadOnlyList<DimensionExplosiveDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>Adds one bomb, replacing an earlier registration of the same name.</summary>
        public static void Register(
            string bombObjectName,
            string blastObjectName,
            int blastVariation)
        {
            Register(new DimensionExplosiveDefinition(
                bombObjectName, blastObjectName, blastVariation));
        }

        public static void Register(DimensionExplosiveDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.BombObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].BombObjectName,
                        definition.BombObjectName,
                        StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        /// <summary>Empties the registry. Only for tests, which must not leak into each other.</summary>
        public static void Clear()
        {
            Definitions.Clear();
        }
    }
}
