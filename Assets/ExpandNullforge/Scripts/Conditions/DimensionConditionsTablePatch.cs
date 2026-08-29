using HarmonyLib;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Conditions
{
    /// <summary>
    /// Makes room in Core Keeper's condition table for the ones a mod invented.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE GAME BUILDS ITS CONDITION TABLE ONCE, as a fixed array of 357 entries addressed by
    /// condition number. Everything that reads a condition at runtime goes through that array, and
    /// the read is <em>already</em> bounds-checked against the array's own length — so a longer
    /// array simply works. What does not work is the build: it allocates exactly 357, and writing
    /// entry 357 into it would run off the end.
    /// </para>
    /// <para>
    /// So this replaces the build when, and only when, a mod has claimed conditions of its own. With
    /// nothing claimed the game's own method runs untouched and this costs one boolean check — which
    /// matters, because the same table is built in every world whether or not this mod's content is
    /// anywhere near it.
    /// </para>
    /// <para>
    /// THE COPY IS DELIBERATE AND SMALL. Replacing a method means owning it, so this mirrors what
    /// Core Keeper's own converter does field for field. It is thirty lines; if it ever changes, the
    /// symptom is conditions with the wrong stacking rules rather than a crash, so the fields are
    /// listed out explicitly rather than copied by reflection.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ConditionsTableConverter), "Convert")]
    internal static class DimensionConditionsTablePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            ConditionsTableConverter __instance,
            ConditionsTableAuthoring authoring)
        {
            if (!DimensionConditionRegistry.HasAny ||
                authoring == null ||
                authoring.conditionsTable == null)
            {
                // Let the game build its own table exactly as it always has.
                return true;
            }

            try
            {
                Build(__instance, authoring);
                return false;
            }
            catch (System.Exception exception)
            {
                // A mod's extra conditions are never worth losing the game's own table over. Falling
                // through to the original leaves every vanilla condition working and only the custom
                // ones missing, which is the failure worth having.
                DimensionLog.Fatal(DimensionLogChannels.Condition, null, 
                    "Could not add this mod's conditions to the game's table, so " +
                    "only Core Keeper's own will work: " + exception);
                return true;
            }
        }

        /// <summary>Builds the table at the size the mod's conditions need, and fills both halves.</summary>
        private static void Build(
            ConditionsTableConverter converter,
            ConditionsTableAuthoring authoring)
        {
            int size = DimensionConditionRegistry.RequiredTableSize;

            using (BlobBuilder builder = new BlobBuilder(Allocator.Temp))
            {
                ref ConditionsTableBlob root = ref builder.ConstructRoot<ConditionsTableBlob>();
                BlobBuilderArray<ConditionInfoBlob> infos = builder.Allocate(ref root.infos, size);

                WriteTheGamesOwn(authoring, infos, size);
                WriteThisModsOwn(infos, size);

                BlobAssetReference<ConditionsTableBlob> asset =
                    builder.CreateBlobAssetReference<ConditionsTableBlob>(Allocator.Persistent);

                HandBlobToTheEngine(ref asset);
                converter.AddComponentData(new ConditionsTableCD { Value = asset });
            }
        }

        /// <summary>Every condition Core Keeper ships, copied field for field from its own table.</summary>
        private static void WriteTheGamesOwn(
            ConditionsTableAuthoring authoring,
            BlobBuilderArray<ConditionInfoBlob> infos,
            int size)
        {
            foreach (ConditionsTable.ConditionCategory category
                in authoring.conditionsTable.conditionCategories)
            {
                foreach (ConditionInfo condition in category.conditions)
                {
                    int number = (int)condition.Id;
                    if (number < 0 || number >= size)
                    {
                        continue;
                    }

                    infos[number] = new ConditionInfoBlob
                    {
                        effect = condition.effect,
                        isAdditiveWithSelf = condition.isAdditiveWithSelf,
                        isPermanent = condition.isPermanent,
                        isNegative = condition.isNegative,
                        isUnique = condition.isUnique,
                        isInheritedByProjectiles = condition.isInheritedByProjectiles,
                        overrideIfRemainingValueIsHigher =
                            condition.overrideIfRemainingValueIsHigher
                    };
                }
            }
        }

        /// <summary>The mod's own, in the order the registry hands out numbers.</summary>
        private static void WriteThisModsOwn(BlobBuilderArray<ConditionInfoBlob> infos, int size)
        {
            System.Collections.Generic.List<DimensionCustomCondition> ours =
                DimensionConditionRegistry.InNumberOrder();

            for (int i = 0; i < ours.Count; i++)
            {
                int number = DimensionConditionRegistry.FirstFreeNumber + i;
                if (number >= size)
                {
                    break;
                }

                DimensionCustomCondition condition = ours[i];
                infos[number] = new ConditionInfoBlob
                {
                    effect = condition.Effect,
                    isAdditiveWithSelf = condition.AddsToItself,
                    isPermanent = condition.LastsForever,
                    isNegative = condition.IsBad,
                    isUnique = condition.OnlyOneAtATime,
                    isInheritedByProjectiles = condition.PassedOnByShots,
                    overrideIfRemainingValueIsHigher = condition.AStrongerOneWins
                };
            }
        }

        /// <summary>
        /// Hands the blob to the engine's own store, so the engine and not this mod owns how long
        /// it lives.
        /// </summary>
        /// <remarks>
        /// The store is protected on the converter's base class and the property that would reach
        /// it another way has a private getter, so this used to go through reflection. It cannot:
        /// Core Keeper's mod sandbox denies <c>System.Reflection</c> and <c>AccessTools</c> alike
        /// and refuses the whole mod over either. <see cref="DimensionConversionStoreProbe"/> is
        /// asked instead, and it is inside the hierarchy, so this is an ordinary typed call. It
        /// does not need the converter passed in: one store serves the whole process, which is why
        /// this takes no converter any more. Not having it is worth saying out loud rather than
        /// passing over: the table still works, but the blob then lives until the process ends
        /// rather than until the world does.
        /// </remarks>
        private static void HandBlobToTheEngine(ref BlobAssetReference<ConditionsTableBlob> asset)
        {
            BlobAssetStore store;
            if (!DimensionConversionStoreProbe.TryGetStore(out store))
            {
                DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                    "The engine's blob store was never handed to " +
                    "DimensionConversionStoreProbe, so this mod's condition table is not in the " +
                    "engine's own bookkeeping. Every custom condition still works. What does not " +
                    "happen is the table being freed with the world: it stays allocated until the " +
                    "game is closed, once per world loaded. The probe is a Converter the game " +
                    "builds for itself, and the game looks for converters exactly once, in " +
                    "ECSManager.Init, and reuses that list for every world afterwards — so this " +
                    "means this assembly was not loaded yet when Init ran. Reloading a world will " +
                    "not change it; only starting the game with the mod present will.");
                return;
            }

            store.TryAdd(ref asset);
        }
    }

    /// <summary>
    /// A converter that exists only to be handed the engine's blob store, so the condition table can
    /// be registered with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A CONVERTER AND NOT A PATCH. <c>Converter.BlobAssetStore</c> is protected and reads
    /// <c>ConversionManager.BlobAssetStore</c>, and <c>Converter.ConversionManager</c> has a private
    /// getter, so from outside the class hierarchy there is no way to ask for it. The obvious trick
    /// is to patch the public SETTER and read the value going past. That setter is an auto-property
    /// — about eight bytes of IL — and its only caller, <c>ConversionManager.AddConverter</c>, is
    /// first compiled after a mod loads. Mono inlines a method that small, and an inlined copy of
    /// the original body is not the patched one, so the patch would bind, report success, and never
    /// run. Nothing available to a mod can tell the two apart at run time. The way out is to stop
    /// needing the patch: be a converter, and the value is handed over by ordinary means.
    /// </para>
    /// <para>
    /// HOW IT IS REACHED. <c>ConversionManager.FindAllConvertersInCurrentAssembly</c> walks every
    /// loaded assembly that references PugConversion and instantiates each non-abstract
    /// <c>Converter</c> in it — this mod's assembly among them. <c>AddConverter</c> then assigns
    /// <c>ConversionManager</c> and, on the very next line, reads
    /// <c>RequireComponentTypeToRun</c> — a virtual call, on a type of ours, with the manager
    /// already in place. That is where the store is taken.
    /// (<c>ck-db\PugConversion\Pug\Conversion\ConversionManager.cs:138-139</c>.)
    /// </para>
    /// <para>
    /// THE GATE IS ONE CALL, NOT EVERY WORLD LOAD. <c>ECSManager.Init()</c> runs
    /// <c>FindAllConvertersInCurrentAssembly()</c> once into <c>_cachedPugConverterTypes</c>
    /// (<c>ck-db\Pug.Other\ECSManager.cs:71</c>) and hands that same cached list to every
    /// <c>ConversionManager</c> it builds afterwards (<c>:82</c>, <c>:704</c>). An assembly absent
    /// from the AppDomain at that moment is never looked for again, however many worlds load. This
    /// mod is present: <c>_modManager</c> comes before <c>_ecsManager</c> in
    /// <c>Manager._allManagers</c> (<c>Pug.Other\Manager.cs:789</c> against <c>:803</c>, initialised
    /// in list order at <c>:921</c>), and <c>ModManager.EarlyInit()</c> runs earlier still
    /// (<c>Manager.cs:174</c>). Worth writing down because the ordering is the whole reason this
    /// works, and nothing else in the tree records it.
    /// </para>
    /// <para>
    /// IN THE EDITOR IT CAN CAPTURE A STORE THAT IS LATER DISPOSED.
    /// <c>Pug.Dev\DungeonDevSpawner.cs:90-101</c> builds a ConversionManager of its own with
    /// <c>new BlobAssetStore(1024)</c> and no cached list. <see cref="TryGetStore"/> checks
    /// <c>IsCreated</c> for that reason, so the worst case is the warning above rather than a
    /// crash on a disposed store.
    /// </para>
    /// <para>
    /// ONE STORE, NOT ONE PER MANAGER. <c>ECSManager</c> builds every <c>ConversionManager</c> with
    /// <c>BlobAssetStore = Manager.ecs.BlobAssetStore</c>, the one store it made at
    /// <c>Init</c> (<c>ck-db\Pug.Other\ECSManager.cs:69,76,697</c>), so the value taken here is the
    /// store every converter in the process converts into, whichever manager handed it over.
    /// </para>
    /// <para>
    /// IT CONVERTS NOTHING. The required component is the conditions table's own authoring, so this
    /// runs at most once per world and does nothing when it does. Throwing here would take the
    /// game's whole conversion down with it, which is why the one line that can throw is wrapped.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// Public and preserved on purpose, the way every converter Core Keeper ships is: the engine
    /// finds this type by reflection and builds it with <c>Activator.CreateInstance</c>, and a type
    /// the linker dropped or a constructor the binder would not take is a converter that never
    /// exists, with nothing said about it anywhere.
    /// </remarks>
    [UnityEngine.Scripting.Preserve]
    public sealed class DimensionConversionStoreProbe : Converter
    {
        private static BlobAssetStore captured;
        private static bool haveCaptured;

        /// <summary>
        /// Answers the question the engine asks every converter once, and takes the store while
        /// answering it.
        /// </summary>
        public override System.Type RequireComponentTypeToRun
        {
            get
            {
                try
                {
                    BlobAssetStore store = BlobAssetStore;
                    if (store.IsCreated)
                    {
                        captured = store;
                        haveCaptured = true;
                    }
                }
                catch (System.Exception exception)
                {
                    // Never worth taking the game's conversion down over. The condition table's own
                    // warning covers what is lost.
                    DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                        "Could not read the engine's blob store while being " +
                        "registered as a converter: " + exception);
                }

                return typeof(ConditionsTableAuthoring);
            }
        }

        /// <summary>Nothing. The type above is what this class is for.</summary>
        public override void Convert(GameObject authoring)
        {
        }

        /// <summary>The engine's blob store, if it has been handed over yet.</summary>
        internal static bool TryGetStore(out BlobAssetStore store)
        {
            store = haveCaptured ? captured : default;
            return haveCaptured && store.IsCreated;
        }
    }
}
