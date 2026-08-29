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
    /// A seed, naming the plant it grows into instead of pointing at a baked id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS AT ALL. Vanilla's <c>SeedAuthoring.turnsIntoPlantID</c> is an
    /// <c>ObjectID</c>, and a mod's own plant has no ObjectID while the prefab is being written in
    /// the editor: <c>PugMod.API.Authoring</c> is a static field a loaded mod context assigns, so in
    /// the editor it is null and every lookup answers <c>None</c>. A generator that bakes the id
    /// therefore bakes <c>None</c>, and the failure is silent — the ripe seed reaches
    /// <c>PlantsGrowJob</c>, finds no object behind the property, destroys itself and grows nothing.
    /// A player sees a seed that vanishes ten minutes after planting.
    /// </para>
    /// <para>
    /// THE FIX IS THE TIMING, NOT THE DATA. Names resolve during conversion, which happens inside a
    /// running game after every mod has registered its objects — vanilla's own
    /// <c>ObjectAuthoring.ObjectInfo</c> calls <c>API.Authoring.GetObjectID</c> for other objects by
    /// name at exactly that moment. So this component carries the name and the converter below does
    /// the lookup, emitting the identical property set vanilla's <c>SeedConverter</c> emits.
    /// </para>
    /// <para>
    /// The generated seed prefab carries THIS component and NOT vanilla's <c>SeedAuthoring</c>. Both
    /// would write the same properties, and which one landed last would depend on converter
    /// registration order, which nothing guarantees.
    /// </para>
    /// </remarks>
    public sealed class DimensionSeedAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of the plant this seed grows into.")]
        public string turnsIntoPlantName;

        [Tooltip("Which variation of the seed the game's own golden roll places. 0 turns that roll off.")]
        public int rareSeedVariation;

        [Tooltip("Which variation of the plant a golden seed grows into.")]
        public int rarePlantVariation;

        [Tooltip("How many stages it passes through, how long each takes, and where it starts.")]
        public GrowingSettings growingSettings = new GrowingSettings();
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

    /// <summary>
    /// What a plant gives when it is picked, named rather than baked — and how many.
    /// </summary>
    /// <remarks>
    /// Same name-resolution reason as the seed. It also carries the harvest count, which vanilla's
    /// <c>PlantConverter</c> hardcodes to <c>numberOfPlantsToDrop = 1</c> with no way to author it:
    /// the field is on the mutable <c>PlantCD</c> and the game itself raises it at kill time for the
    /// harvest-chance stat, so a crop that yields three of something is entirely supportable and
    /// simply had no authoring surface.
    /// </remarks>
    public sealed class DimensionPlantProduceAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of what picking it gives.")]
        public string produceName;

        [Tooltip("How many of that a single harvest gives.")]
        public int numberOfPlantsToDrop = 1;

        [Tooltip("How many stages it passes through, how long each takes, and where it starts.")]
        public GrowingSettings growingSettings = new GrowingSettings();
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
    /// The things a plant gives back besides its produce — its seed, and any extras a better
    /// version of the crop is worth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE CHANCE COVERS THE WHOLE LIST, and that is the game's shape rather than a simplification.
    /// <c>DropLootConverter</c> writes each entry into <c>DropsLootBuffer</c> and the single
    /// <c>customLoot.chance</c> into <c>ChanceToDropLootCD</c>, which is one component per object;
    /// <c>DropLootFromLootBuffersJob</c> then rolls that same number separately for every entry.
    /// There is nowhere to put a second chance.
    /// </para>
    /// <para>
    /// Two things move the number at harvest and both are vanilla's doing: a plant that has NOT
    /// finished growing drops everything at 100% (dig up a sprout and you always get the seed
    /// back), and a ripe one adds the Grateful Gardener talent on top.
    /// </para>
    /// </remarks>
    public sealed class DimensionPlantDropsAuthoring : MonoBehaviour
    {
        [Tooltip("Full object names of what it gives back. Parallel to the amounts below.")]
        public string[] itemNames = new string[0];

        [Tooltip("How many of each. Parallel to the names above.")]
        public int[] amounts = new int[0];

        [Tooltip("How often each of them is given, from 0 to 1. Vanilla crops return their seed at 0.75.")]
        public float chance = 0.75f;
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
