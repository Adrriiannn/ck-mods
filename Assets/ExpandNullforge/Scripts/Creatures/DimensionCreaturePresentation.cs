using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Everything a generated creature view has to re-learn each time it is handed an entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS AT ALL. Core Keeper pools graphical objects by the TYPE of the component on
    /// them, not by the prefab they came from, so the instance that was just a lava slime is handed
    /// straight to an ember bat. Anything baked into that instance — which sprite asset it draws,
    /// how big its shadow is, what it sounds like — arrives already wrong. The only safe shape is
    /// to bake nothing and look everything up again by the object id the entity is carrying, which
    /// is what this registry is for.
    /// </para>
    /// <para>
    /// The sprite asset is held as its ADDRESS rather than as a reference. A view resolves an
    /// address through the game's own data-block table, so the registry never has to hold a Unity
    /// object and the generated bootstrap can register a creature with two plain numbers.
    /// </para>
    /// </remarks>
    public sealed class DimensionCreaturePresentationDefinition
    {
        public DimensionCreaturePresentationDefinition(
            string creatureObjectName,
            long spriteAssetAddressLow,
            long spriteAssetAddressHigh,
            bool turnsToFaceWhereItGoes,
            int shadowVariantHash,
            bool hasShadow,
            int spawnSound,
            int idleSound,
            int aggroSound,
            int hitSound,
            int deathSound,
            int[] momentNames = null,
            int[] momentSounds = null)
        {
            MomentNames = momentNames ?? new int[0];
            MomentSounds = momentSounds ?? new int[0];
            CreatureObjectName = creatureObjectName ?? string.Empty;
            SpriteAssetAddressLow = spriteAssetAddressLow;
            SpriteAssetAddressHigh = spriteAssetAddressHigh;
            TurnsToFaceWhereItGoes = turnsToFaceWhereItGoes;
            ShadowVariantHash = shadowVariantHash;
            HasShadow = hasShadow;
            SpawnSound = spawnSound;
            IdleSound = idleSound;
            AggroSound = aggroSound;
            HitSound = hitSound;
            DeathSound = deathSound;
        }

        /// <summary>The mod-qualified object name — what resolves to an ObjectID at runtime.</summary>
        public readonly string CreatureObjectName;

        public readonly long SpriteAssetAddressLow;

        public readonly long SpriteAssetAddressHigh;

        /// <summary>Whether it flips and picks up/side art from the way it is walking.</summary>
        public readonly bool TurnsToFaceWhereItGoes;

        /// <summary>Which size out of the game's shared shadow sprite. Zero is the plain one.</summary>
        public readonly int ShadowVariantHash;

        public readonly bool HasShadow;

        public readonly int SpawnSound;

        public readonly int IdleSound;

        public readonly int AggroSound;

        public readonly int HitSound;

        public readonly int DeathSound;

        /// <summary>
        /// The named moments inside its clips, as the numbers the sprite system fires.
        /// </summary>
        /// <remarks>
        /// Stored as numbers rather than names because that is what arrives at the other end: a
        /// sprite object hands its listeners the hash of the moment's name and nothing else.
        /// </remarks>
        public readonly int[] MomentNames;

        /// <summary>The sound each moment plays, one per name, in the same order.</summary>
        public readonly int[] MomentSounds;

        /// <summary>Whether a sprite asset was generated for this creature at all.</summary>
        public bool HasBody
        {
            get { return SpriteAssetAddressLow != 0L || SpriteAssetAddressHigh != 0L; }
        }

        /// <summary>Whether anything inside its clips makes a noise partway through.</summary>
        public bool HasMoments
        {
            get { return MomentNames.Length > 0; }
        }

        /// <summary>The sound a fired moment plays, or zero for one nobody gave a sound.</summary>
        public int SoundForMoment(int momentHash)
        {
            for (int i = 0; i < MomentNames.Length && i < MomentSounds.Length; i++)
            {
                if (MomentNames[i] == momentHash)
                {
                    return MomentSounds[i];
                }
            }

            return 0;
        }
    }

    /// <summary>Every creature's look and voice, keyed by the object id the game gives it.</summary>
    /// <remarks>
    /// Filled by the generated bootstrap at load, read by <see cref="DimensionCreatureView"/> every
    /// time a view is handed an entity. The object-id map is rebuilt whenever the registration
    /// count changes rather than at a fixed moment, because object ids do not exist until the mod's
    /// objects are registered and there is no callback that says when that has happened.
    /// </remarks>
    public static class DimensionCreaturePresentationRegistry
    {
        private static readonly List<DimensionCreaturePresentationDefinition> Definitions =
            new List<DimensionCreaturePresentationDefinition>();

        private static readonly Dictionary<ObjectID, DimensionCreaturePresentationDefinition> ById =
            new Dictionary<ObjectID, DimensionCreaturePresentationDefinition>();

        private static int idsResolvedForCount = -1;

        public static IReadOnlyList<DimensionCreaturePresentationDefinition> All
        {
            get { return Definitions; }
        }

        public static void Register(DimensionCreaturePresentationDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.CreatureObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].CreatureObjectName,
                        definition.CreatureObjectName,
                        StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    ById.Clear();
                    idsResolvedForCount = -1;
                    return;
                }
            }

            Definitions.Add(definition);
            ById.Clear();
            idsResolvedForCount = -1;
        }

        /// <summary>The look and voice for a live creature, or null for anything unregistered.</summary>
        public static DimensionCreaturePresentationDefinition For(ObjectID objectID)
        {
            if (objectID == ObjectID.None || Definitions.Count == 0)
            {
                return null;
            }

            if (idsResolvedForCount != Definitions.Count)
            {
                ById.Clear();
                for (int i = 0; i < Definitions.Count; i++)
                {
                    ObjectID id = API.Authoring.GetObjectID(Definitions[i].CreatureObjectName);
                    if (id != ObjectID.None)
                    {
                        ById[id] = Definitions[i];
                    }
                }

                idsResolvedForCount = Definitions.Count;
            }

            DimensionCreaturePresentationDefinition definition;
            return ById.TryGetValue(objectID, out definition) ? definition : null;
        }

        public static void Clear()
        {
            Definitions.Clear();
            ById.Clear();
            idsResolvedForCount = -1;
        }
    }
}
