using System;
using System.Collections.Generic;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// What one crop, at one variation, looks like in the ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A REGISTRY RATHER THAN A PREFAB PER CROP. Core Keeper pools graphical objects by the
    /// TYPE of the component on them, not by the prefab they came from
    /// (<c>CreateGraphicalObjectSystem</c> asks the pool for a free instance of
    /// <c>prefabComponent.GetType()</c>), so the body that was just a carrot is handed straight to
    /// a pumpkin. Art baked onto that instance arrives already wrong. Core Keeper's own answer is
    /// one shared plant prefab whose skin is re-chosen per entity from a list of conditions; this
    /// is that list, with the framework's own generated bootstrap filling it.
    /// </para>
    /// <para>
    /// KEYED BY OBJECT AND VARIATION, BECAUSE A BETTER VERSION IS NOT A DIFFERENT OBJECT. A golden
    /// crop is the same ObjectID at another variation, which is exactly how the game distinguishes
    /// it too. A row registered at <see cref="AnyVariation"/> answers for every variation nobody
    /// claimed, so the ordinary look, the already-ripe copy and the plain-planted seed all share
    /// one row and only the versions that draw something of their own cost an entry.
    /// </para>
    /// <para>
    /// The sprite asset is held as its ADDRESS rather than as a reference, so this never has to
    /// hold a Unity object and the generated bootstrap can register a look with two plain numbers.
    /// </para>
    /// </remarks>
    public sealed class DimensionPlantPresentationDefinition
    {
        /// <summary>The variation number that means "every variation nobody else claimed".</summary>
        /// <remarks>
        /// Negative on purpose: Core Keeper's variations are indices into a prefab list and are
        /// never below zero, so no real variation can ever collide with this.
        /// </remarks>
        public const int AnyVariation = -1;

        public DimensionPlantPresentationDefinition(
            string plantObjectName,
            int variation,
            long spriteAssetAddressLow,
            long spriteAssetAddressHigh,
            int[] stageAnimations,
            int shineAnimation,
            bool castsAShadow,
            Color colourWash,
            Color glowColour,
            Color groundGlowColour,
            bool glowsOnlyWhenRipe,
            bool ignoresTorchlight,
            int ripeSound = 0,
            int ripePuff = -1)
        {
            PlantObjectName = plantObjectName ?? string.Empty;
            Variation = variation;
            SpriteAssetAddressLow = spriteAssetAddressLow;
            SpriteAssetAddressHigh = spriteAssetAddressHigh;
            StageAnimations = stageAnimations ?? new int[0];
            ShineAnimation = shineAnimation;
            CastsAShadow = castsAShadow;
            ColourWash = colourWash;
            GlowColour = glowColour;
            GroundGlowColour = groundGlowColour;
            GlowsOnlyWhenRipe = glowsOnlyWhenRipe;
            IgnoresTorchlight = ignoresTorchlight;
            RipeSound = ripeSound;
            RipePuff = ripePuff;
        }

        /// <summary>The mod-qualified object name — what resolves to an ObjectID at runtime.</summary>
        public readonly string PlantObjectName;

        /// <summary>Which variation this look belongs to, or <see cref="AnyVariation"/>.</summary>
        public readonly int Variation;

        public readonly long SpriteAssetAddressLow;

        public readonly long SpriteAssetAddressHigh;

        /// <summary>
        /// The animation to play at each stage, from just sprouted to ripe.
        /// </summary>
        /// <remarks>
        /// Indexed by the plant's own <c>GrowingCD.currentStage</c> directly, so this has one more
        /// entry than the crop has growth stages — the game counts stages from zero up to and
        /// including its top stage. Core Keeper's own plant renderer keeps a fixed array of four
        /// and reads off the end of it above three looks; nothing here is fixed, so a crop may have
        /// as many stages as its author draws.
        /// </remarks>
        public readonly int[] StageAnimations;

        /// <summary>The twinkle that runs over a ripe plant, or zero for a crop without one.</summary>
        public readonly int ShineAnimation;

        public readonly bool CastsAShadow;

        /// <summary>A multiply over the plant's own colours. White leaves it alone.</summary>
        public readonly Color ColourWash;

        public readonly Color GlowColour;

        public readonly Color GroundGlowColour;

        public readonly bool GlowsOnlyWhenRipe;

        public readonly bool IgnoresTorchlight;

        /// <summary>The sound played the moment it ripens, or zero for silence.</summary>
        public readonly int RipeSound;

        /// <summary>The puff thrown up when it ripens, as a PuffID number, or -1 for none.</summary>
        public readonly int RipePuff;

        /// <summary>Whether art was generated for this look at all.</summary>
        public bool HasArt
        {
            get { return SpriteAssetAddressLow != 0L || SpriteAssetAddressHigh != 0L; }
        }

        public bool HasShine
        {
            get { return ShineAnimation != 0; }
        }

        /// <summary>Whether it lights anything, which is what decides if the light is switched on.</summary>
        public bool Glows
        {
            get { return IsLit(GlowColour) || IsLit(GroundGlowColour); }
        }

        /// <summary>Whether it throws light onto the ground rather than only glowing itself.</summary>
        public bool LightsTheGround
        {
            get { return IsLit(GroundGlowColour); }
        }

        /// <summary>
        /// The animation for one stage, clamped rather than thrown.
        /// </summary>
        /// <remarks>
        /// A plant whose saved stage is past the end of its pictures is a crop whose author removed
        /// a stage after a world existed. Showing the last picture is wrong by one and readable;
        /// throwing here would be an exception every frame in <c>ManagedLateUpdate</c>, which stops
        /// the whole presentation loop for every object on screen.
        /// </remarks>
        public int AnimationForStage(int stage)
        {
            if (StageAnimations.Length == 0)
            {
                return 0;
            }

            if (stage < 0)
            {
                return StageAnimations[0];
            }

            return stage >= StageAnimations.Length
                ? StageAnimations[StageAnimations.Length - 1]
                : StageAnimations[stage];
        }

        /// <summary>The stage from which the glow is on.</summary>
        public int FirstGlowingStage
        {
            get
            {
                return GlowsOnlyWhenRipe && StageAnimations.Length > 0
                    ? StageAnimations.Length - 1
                    : 0;
            }
        }

        private static bool IsLit(Color colour)
        {
            return colour.r > 0f || colour.g > 0f || colour.b > 0f;
        }
    }

    /// <summary>Every crop's look, keyed by the object id and variation the game gives it.</summary>
    /// <remarks>
    /// Filled by the generated bootstrap at load, read by <see cref="DimensionPlantView"/> every
    /// time a view is handed an entity. The object-id map is rebuilt whenever the registration
    /// count changes rather than at a fixed moment, because object ids do not exist until the mod's
    /// objects are registered and there is no callback that says when that has happened. Copied
    /// deliberately from <c>DimensionCreaturePresentationRegistry</c> so the two behave the same
    /// way under the same conditions.
    /// </remarks>
    public static class DimensionPlantPresentationRegistry
    {
        private static readonly List<DimensionPlantPresentationDefinition> Definitions =
            new List<DimensionPlantPresentationDefinition>();

        private static readonly Dictionary<long, DimensionPlantPresentationDefinition> ByKey =
            new Dictionary<long, DimensionPlantPresentationDefinition>();

        private static int idsResolvedForCount = -1;

        public static IReadOnlyList<DimensionPlantPresentationDefinition> All
        {
            get { return Definitions; }
        }

        public static void Register(DimensionPlantPresentationDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.PlantObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (Definitions[i].Variation == definition.Variation &&
                    string.Equals(
                        Definitions[i].PlantObjectName,
                        definition.PlantObjectName,
                        StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    Invalidate();
                    return;
                }
            }

            Definitions.Add(definition);
            Invalidate();
        }

        /// <summary>
        /// The look for a live plant or seed, or null for anything unregistered.
        /// </summary>
        /// <remarks>
        /// The exact variation wins over the catch-all, which is what lets a golden crop draw its
        /// own art while every other variation of the same object shares one row.
        /// </remarks>
        public static DimensionPlantPresentationDefinition For(ObjectID objectID, int variation)
        {
            if (objectID == ObjectID.None || Definitions.Count == 0)
            {
                return null;
            }

            if (idsResolvedForCount != Definitions.Count)
            {
                Rebuild();
            }

            DimensionPlantPresentationDefinition definition;
            if (ByKey.TryGetValue(KeyFor(objectID, variation), out definition))
            {
                return definition;
            }

            return ByKey.TryGetValue(
                KeyFor(objectID, DimensionPlantPresentationDefinition.AnyVariation),
                out definition)
                ? definition
                : null;
        }

        public static void Clear()
        {
            Definitions.Clear();
            Invalidate();
        }

        private static void Invalidate()
        {
            ByKey.Clear();
            idsResolvedForCount = -1;
        }

        private static void Rebuild()
        {
            ByKey.Clear();
            for (int i = 0; i < Definitions.Count; i++)
            {
                ObjectID id = API.Authoring.GetObjectID(Definitions[i].PlantObjectName);
                if (id != ObjectID.None)
                {
                    ByKey[KeyFor(id, Definitions[i].Variation)] = Definitions[i];
                }
            }

            idsResolvedForCount = Definitions.Count;
        }

        /// <summary>
        /// One number out of an object id and a variation.
        /// </summary>
        /// <remarks>
        /// A tuple key would allocate a comparer per lookup on IL2CPP; this runs inside
        /// <c>OnOccupied</c> for every plant that comes on screen, which is a great many of them in
        /// a farm. The object id takes the whole high half and the variation the whole low half, so
        /// no two pairs can share a number — including the negative catch-all variation, whose sign
        /// is cut off by the cast rather than smeared into the object id.
        /// </remarks>
        public static long KeyFor(ObjectID objectID, int variation)
        {
            return ((long)(int)objectID << 32) ^ (uint)variation;
        }
    }
}
