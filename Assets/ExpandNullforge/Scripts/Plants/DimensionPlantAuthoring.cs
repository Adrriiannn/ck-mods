using Pug.Conversion;
using Pug.Properties;
using PugMod;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// The property names Core Keeper's growing jobs read a seed and a plant by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SPELLING IS IDENTITY HERE. A property is stored under
    /// <c>Animator.StringToHash(name)</c>, and <c>PlantsGrowJob</c> is Burst-compiled with those
    /// hashes as literal integers — <c>-1534320058</c> for the plant a seed turns into, and so on.
    /// A name spelled one character differently hashes to something else, the job finds nothing, and
    /// the seed destroys itself when it finishes growing without a word in the log.
    /// </para>
    /// <para>
    /// So the names live here rather than being typed at each call site, and a test pins each one
    /// against the number the decompiled job actually looks for.
    /// </para>
    /// </remarks>
    public static class DimensionPlantProperties
    {
        public const string IsSeed = "isSeed";

        public const string TurnsIntoPlantId = "Seed/turnsIntoPlantID";

        public const string RarePlantVariation = "Seed/rarePlantVariation";

        public const string RareSeedVariation = "Seed/rareSeedVariation";

        public const string HighestStage = "Growing/highestStage";

        public const string TimeBetweenStages = "Growing/timeBetweenStages";

        public const string KeepDamageReductionWhenRipe = "Growing/keepDamageReductionWhenRipe";

        /// <summary>Everything the framework's seed converter writes, in the order it writes it.</summary>
        /// <remarks>Copied field for field from <c>Pug.ECS.Conversion/SeedConverter</c>.</remarks>
        public static readonly string[] Seed =
        {
            IsSeed,
            TurnsIntoPlantId,
            RarePlantVariation,
            RareSeedVariation,
            HighestStage,
            TimeBetweenStages,
            KeepDamageReductionWhenRipe
        };

        /// <summary>Everything the framework's plant converter writes.</summary>
        /// <remarks>Copied from <c>Pug.ECS.Conversion/PlantConverter</c>.</remarks>
        public static readonly string[] Plant =
        {
            HighestStage,
            TimeBetweenStages,
            KeepDamageReductionWhenRipe
        };
    }

    /// <summary>
    /// Emits what vanilla's <c>SeedConverter</c> emits, with the plant resolved from its name.
    /// </summary>
    /// <remarks>
    /// The property set is copied field for field from <c>Pug.ECS.Conversion/SeedConverter</c>:
    /// <c>isSeed</c>, the three <c>Seed/*</c> values and the three <c>Growing/*</c> values, plus
    /// <c>GrowingCD</c>. <c>Seed/turnsIntoPlantID</c> lives in the baked, per-(object,variation)
    /// property blob, so it can only ever be written here — no runtime hydration can reach it, and
    /// the job that reads it is Burst-compiled and cannot be patched.
    /// </remarks>
    [Preserve]
    public sealed class DimensionSeedConverter :
        SingleAuthoringComponentConverter<DimensionSeedAuthoring>
    {
        protected override void Convert(DimensionSeedAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            SetProperty(DimensionPlantProperties.IsSeed);

            // Written only when the name already resolves. Conversion order among mod objects is a
            // hash of prefab names, so the plant may not have an id yet; DimensionSeedLinkPostConverter
            // fills the gap once the whole queue has been converted.
            ObjectID plant = DimensionPlantNames.Resolve(authoring.turnsIntoPlantName, "seed");
            if (plant != ObjectID.None)
            {
                SetProperty(DimensionPlantProperties.TurnsIntoPlantId, plant);
            }

            SetProperty(DimensionPlantProperties.RarePlantVariation, authoring.rarePlantVariation);
            SetProperty(DimensionPlantProperties.RareSeedVariation, authoring.rareSeedVariation);

            GrowingSettings growing = authoring.growingSettings ?? new GrowingSettings();
            SetProperty(DimensionPlantProperties.HighestStage, growing.highestStage);
            SetProperty(DimensionPlantProperties.TimeBetweenStages, growing.timeBetweenStages);
            SetProperty(
                DimensionPlantProperties.KeepDamageReductionWhenRipe,
                growing.keepDamageReductionWhenRipe);
            AddComponentData(new GrowingCD { currentStage = growing.currentStage });
        }
    }

    /// <summary>Emits what vanilla's <c>PlantConverter</c> emits, plus the harvest count.</summary>
    [Preserve]
    public sealed class DimensionPlantProduceConverter :
        SingleAuthoringComponentConverter<DimensionPlantProduceAuthoring>
    {
        protected override void Convert(DimensionPlantProduceAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new PlantCD
            {
                // Never below one: a plant that drops zero of its produce reads to a player as a
                // harvest that failed, and the game has no other way to say "yields nothing" than
                // an empty produce name, which resolves to None below.
                numberOfPlantsToDrop = authoring.numberOfPlantsToDrop < 1 ? 1 : authoring.numberOfPlantsToDrop,
                objectToDropWhenHarvested = DimensionPlantNames.Resolve(authoring.produceName, "plant")
            });

            GrowingSettings growing = authoring.growingSettings ?? new GrowingSettings();
            SetProperty(DimensionPlantProperties.HighestStage, growing.highestStage);
            SetProperty(DimensionPlantProperties.TimeBetweenStages, growing.timeBetweenStages);
            SetProperty(
                DimensionPlantProperties.KeepDamageReductionWhenRipe,
                growing.keepDamageReductionWhenRipe);
            AddComponentData(new GrowingCD { currentStage = growing.currentStage });
        }
    }

    /// <summary>
    /// Writes the give-backs straight into the buffer the drop job reads.
    /// </summary>
    /// <remarks>
    /// Parallel arrays rather than a list of a small class: a serialized
    /// <c>List&lt;CustomClass&gt;</c> on an authoring component does not survive the trip into the
    /// game, and the failure is an empty list rather than an error.
    /// </remarks>
    [Preserve]
    public sealed class DimensionPlantDropsConverter :
        SingleAuthoringComponentConverter<DimensionPlantDropsAuthoring>
    {
        protected override void Convert(DimensionPlantDropsAuthoring authoring)
        {
            if (authoring == null || authoring.itemNames == null || authoring.itemNames.Length == 0)
            {
                return;
            }

            EnsureHasBuffer<DropsLootBuffer>();
            bool wroteAny = false;
            for (int i = 0; i < authoring.itemNames.Length; i++)
            {
                ObjectID itemId = DimensionPlantNames.Resolve(authoring.itemNames[i], "plant drop");
                if (itemId == ObjectID.None)
                {
                    continue;
                }

                int amount = 1;
                if (authoring.amounts != null && i < authoring.amounts.Length && authoring.amounts[i] > 0)
                {
                    amount = authoring.amounts[i];
                }

                AddToBuffer(new DropsLootBuffer { lootDropID = itemId, amount = amount });
                wroteAny = true;
            }

            if (wroteAny)
            {
                AddComponentData(new ChanceToDropLootCD
                {
                    chance = authoring.chance < 0f ? 0f : (authoring.chance > 1f ? 1f : authoring.chance)
                });
            }
        }
    }

    /// <summary>
    /// Turns an object name into an id during conversion, saying so once when it cannot.
    /// </summary>
    /// <remarks>
    /// Conversion runs inside a loaded game, so <c>API.Authoring</c> is normally there. It is
    /// checked anyway because the same converters are reachable from editor-side baking, where the
    /// field is null and touching it throws rather than answering "nothing registered".
    /// </remarks>
    internal static class DimensionPlantNames
    {
        private static readonly System.Collections.Generic.HashSet<string> Warned =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        public static ObjectID Resolve(string objectName, string what)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return ObjectID.None;
            }

            // The framework's one resolver. It answers the GAME's own names too, which this did not:
            // a crop authored to drop Wood was resolving to nothing outside a loaded game.
            ObjectID id = Foundation.DimensionObjectNames.Resolve(objectName);
            if (id == ObjectID.None && Warned.Add(objectName))
            {
                Foundation.DimensionFrameworkLog.Warning(
                    "A " + what + " names '" + objectName + "', which resolves to " +
                    "no object. Generate the mod again so the object exists, or correct the name in " +
                    "the plant asset; until then that part of the crop does nothing.");
            }

            return id;
        }

        /// <summary>The id of the plant a seed grows into, read off the seed's baked properties.</summary>
        /// <remarks>
        /// Hashed from the name rather than written as the literal
        /// <c>-1534320058</c> the decompiled job shows, so the two can never disagree.
        /// </remarks>
        public static readonly int TurnsIntoPlantPropertyId =
            Property.StringToHash(DimensionPlantProperties.TurnsIntoPlantId);
    }
}
