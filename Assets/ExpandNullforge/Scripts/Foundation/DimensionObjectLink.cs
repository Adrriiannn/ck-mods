using System;
using System.Collections.Generic;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// One place in the game's data where an object points at another object by number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NAMED AFTER THE FIELD, NOT AFTER THE DOMAIN. Two places that write the same field are one
    /// member here; two places that write different fields are two members even when they read the
    /// same way in English. A bow's shot and a creature's shot are the obvious trap — they sound
    /// identical and they are <c>RangeWeaponCD.projectileID</c> and
    /// <c>RangeAttackStateCD.projectileID</c>, two components on two kinds of object, so they are
    /// <see cref="FiresProjectile"/> and <see cref="CreatureShot"/>.
    /// </para>
    /// <para>
    /// EVERY MEMBER IS WIRED END TO END. A member here means three things exist: something writes
    /// the row (a generator's slice of <c>AppendObjectLinkRegistrations</c>), an arm in
    /// <see cref="DimensionObjectLinkHydration"/> writes the field, and the generator that owns the
    /// authored field keeps its component alive rather than stripping it. Two tests hold this: one
    /// walks the enum against the write table and fails if an arm is missing, and one reads
    /// <c>DimensionRuntimeConsumerBootstrapUtility.ObjectLinks.cs</c> and fails on the first member
    /// no walk in it ever names. A member nothing emits is a promise nothing keeps, so it does not
    /// get to sit here waiting for a later wave — it is either wired or it is not a member.
    /// </para>
    /// <para>
    /// WHAT IS DELIBERATELY ABSENT. A field the game's converter writes into the baked property blob
    /// — <c>MeleeAttack/objectToSpawnOnHitTiles</c>, <c>PlaceableObject/canBePlacedOnObjects</c>,
    /// <c>CanBePlaced/allowedObjects</c>, <c>Seed/turnsIntoPlantID</c> — cannot appear here at any
    /// price. The blob is sealed by <c>ConversionManager.FinalizeProperties</c> and read by Burst
    /// jobs; no system on any tick can reach it. Those are handled at conversion time instead
    /// (see <c>DimensionPlacedOnNamesAuthoring</c> and <c>DimensionSeedAuthoring</c>).
    /// </para>
    /// </remarks>
    public enum DimensionObjectLink
    {
        /// <summary>Nothing. A row carrying this is ignored.</summary>
        None = 0,

        /// <summary>What a weapon fires. <c>RangeWeaponCD.projectileID</c>.</summary>
        FiresProjectile,

        /// <summary>What a creature shoots. <c>RangeAttackStateCD.projectileID</c>.</summary>
        CreatureShot,

        /// <summary>One entry of a weapon's random-shot list. <c>RangeWeaponCD.randomProjectiles</c>.</summary>
        RandomProjectile,

        /// <summary>What a fully wound-up shot fires instead. <c>RangeWeaponCD.windupProjectileID</c>.</summary>
        SecondProjectile,

        /// <summary>What a fully wound-up beam fires. <c>BeamWeaponCD.windupProjectileID</c>.</summary>
        BeamProjectile,

        /// <summary>What right-clicking summons. <c>SecondaryUseCD.minionToSpawn</c>.</summary>
        SecondaryUseMinion,

        /// <summary>What a piece of jewellery polishes into. <c>JewelryCanBePolishedCD.polishedVersion</c>.</summary>
        PolishesInto,

        /// <summary>What a scanner points at. <c>ScannerCD.objectToScan</c>.</summary>
        ScansFor,

        /// <summary>What an object sheds as it is hit. <c>DropsLootWhenDamagedCD.dropsLoot</c>.</summary>
        ShedsWhenDamaged,

        /// <summary>The titan a shrine is bound to. <c>TitanShrineCD.titanObjectID</c>.</summary>
        TitanShrine,

        /// <summary>What a container turns into once it holds the right thing. <c>ChangeVariationWhenContainingObjectCD.reinstantiateToNewObjectId</c>.</summary>
        ContainerBecomes,

        /// <summary>The thing a container reacts to holding. <c>ChangeVariationWhenContainingObjectCD.objectID</c>.</summary>
        ContainerReactsTo,

        /// <summary>One id a slot rule accepts. <c>InventorySlotRequirementBuffer.acceptsObjectIds</c>.</summary>
        SlotAccepts,

        /// <summary>What a melody turns an object into. <c>AffectObjectWhenMelodyPlayedCD.newObjectId</c>.</summary>
        MelodyAffects,

        /// <summary>The chest a boss leaves. <c>BossCD.chestToSpawn.objectID</c>.</summary>
        BossChest,

        /// <summary>The second chest a boss leaves. <c>BossCD.optionalChestVersion.objectID</c>.</summary>
        BossSecondChest,

        /// <summary>What a segmented creature's body ends in. <c>SnakeMovementStateCD.tailObjectId</c>.</summary>
        SegmentTail,

        /// <summary>The one thing a segmented creature never hits. <c>SnakeMovementStateCD.cantHitSpecificObject</c>.</summary>
        NeverHits,

        /// <summary>What a shattering shot breaks into. <c>ShatterOnCollisionProjectileCD.shardObjectID</c>.</summary>
        ProjectileShard,

        /// <summary>What a mortar shell leaves where it lands. <c>MortarProjectileDamageEffectCD.spawnNapalmObjectID</c>.</summary>
        ProjectileNapalm,

        /// <summary>The plant a flower belongs to. <c>FlowerCD.plantID</c>.</summary>
        FlowerOfPlant,

        /// <summary>What a spawner platform sends out. <c>EnemySpawnerPlatformCD.enemyToSpawn</c>.</summary>
        SpawnerEnemy,

        /// <summary>The creature a trophy summons from a platform. <c>TrophyCD.enemyToSpawnFromSpawnerPlatform</c>.</summary>
        TrophyEnemy,

        /// <summary>What something leaves in its wake as it moves. <c>LeaveTrailCD.trailObjectID</c>.</summary>
        TrailObject,

        /// <summary>One line of a trader's stock. <c>MerchantItemInfoBuffer.objectID</c>.</summary>
        MerchantStock
    }

    /// <summary>
    /// Each field said the way the person who filled it in would say it.
    /// </summary>
    /// <remarks>
    /// Kept in one place because both ends need it: the generator says "'X' points at a switched-off
    /// Y for its shot" while a creator is still in the dashboard, and the game says "'X' still has
    /// nothing to point at for its shot" hours later. Two copies of these words would drift, and a
    /// creator matching one message against the other would be reading about two different things.
    /// </remarks>
    public static class DimensionObjectLinkWords
    {
        public static string For(DimensionObjectLink link)
        {
            switch (link)
            {
                case DimensionObjectLink.FiresProjectile: return "shot";
                case DimensionObjectLink.CreatureShot: return "shot";
                case DimensionObjectLink.RandomProjectile: return "random shot";
                case DimensionObjectLink.SecondProjectile: return "wound-up shot";
                case DimensionObjectLink.BeamProjectile: return "beam shot";
                case DimensionObjectLink.SecondaryUseMinion: return "right-click minion";
                case DimensionObjectLink.PolishesInto: return "polished version";
                case DimensionObjectLink.ScansFor: return "scan target";
                case DimensionObjectLink.ShedsWhenDamaged: return "shed loot";
                case DimensionObjectLink.TitanShrine: return "bound titan";
                case DimensionObjectLink.ContainerBecomes: return "what it becomes";
                case DimensionObjectLink.ContainerReactsTo: return "what it reacts to";
                case DimensionObjectLink.SlotAccepts: return "accepted item";
                case DimensionObjectLink.MelodyAffects: return "melody result";
                case DimensionObjectLink.BossChest: return "chest";
                case DimensionObjectLink.BossSecondChest: return "second chest";
                case DimensionObjectLink.SegmentTail: return "tail";
                case DimensionObjectLink.NeverHits: return "the thing it never hits";
                case DimensionObjectLink.ProjectileShard: return "shard";
                case DimensionObjectLink.ProjectileNapalm: return "what it leaves behind";
                case DimensionObjectLink.FlowerOfPlant: return "plant";
                case DimensionObjectLink.SpawnerEnemy: return "spawned creature";
                case DimensionObjectLink.TrophyEnemy: return "summoned creature";
                case DimensionObjectLink.TrailObject: return "trail";
                case DimensionObjectLink.MerchantStock: return "shop line";
                default: return "link";
            }
        }
    }

    /// <summary>
    /// One deferred reference: an object, the field on it, and the object that field should name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BOTH NAMES ARE QUALIFIED. The owner is looked up the same way the target is, because the
    /// prefab entity that carries the field belongs to the mod as much as the thing it points at.
    /// </para>
    /// <para>
    /// VARIATION IS CARRIED EXPLICITLY, on both ends. <c>GetObjectID</c> answers an
    /// <c>ObjectID</c> and nothing else — there is no variation in a name. An owner row that left
    /// <see cref="OwnerVariation"/> at zero would hydrate variation 0 of that object and leave every
    /// other variation of it inert, with no error anywhere.
    /// </para>
    /// </remarks>
    public sealed class DimensionObjectLinkDefinition
    {
        public DimensionObjectLinkDefinition(
            string ownerObjectName,
            DimensionObjectLink link,
            string targetObjectName,
            int index = -1,
            int targetVariation = 0,
            int ownerVariation = 0,
            int entryIndex = -1,
            int number = 0)
        {
            OwnerObjectName = ownerObjectName ?? string.Empty;
            Link = link;
            TargetObjectName = targetObjectName ?? string.Empty;
            Index = index;
            TargetVariation = targetVariation < 0 ? 0 : targetVariation;
            OwnerVariation = ownerVariation < 0 ? 0 : ownerVariation;
            EntryIndex = entryIndex;
            Number = number;
        }

        /// <summary>The qualified name of the object carrying the field.</summary>
        public string OwnerObjectName { get; }

        /// <summary>Which field.</summary>
        public DimensionObjectLink Link { get; }

        /// <summary>The qualified name of the object the field should point at.</summary>
        public string TargetObjectName { get; }

        /// <summary>Position in a list or buffer; -1 for a field that holds one id.</summary>
        public int Index { get; }

        /// <summary>
        /// Position INSIDE the element <see cref="Index"/> names, for the two links that are a list
        /// inside a list — a slot rule's accepted ids. -1 everywhere else.
        /// </summary>
        public int EntryIndex { get; }

        /// <summary>Which variation of the target.</summary>
        public int TargetVariation { get; }

        /// <summary>Which variation of the OWNER's prefab carries the field.</summary>
        public int OwnerVariation { get; }

        /// <summary>
        /// A plain number the arm needs alongside the id, for the one field that cannot travel on
        /// its own. Zero everywhere else.
        /// </summary>
        /// <remarks>
        /// The only user is the wound-up shot's explosion size. Vanilla's
        /// <c>RangeWeaponConverter</c> logs a red error when a weapon has an explosion size and no
        /// explosive shot behind it — which is exactly the state a deferred second shot has to be
        /// in while the prefab is being written — so the generator bakes the size as zero and this
        /// carries the authored number back in on the same tick the shot's id lands.
        /// </remarks>
        public int Number { get; }

        /// <summary>
        /// What makes a row unique: the object carrying the field, which field it is, the position
        /// in a list, the position inside that entry, and which variation of the object.
        /// </summary>
        /// <remarks>
        /// The target is deliberately NOT part of it. Re-registering the same field with a
        /// different target has to REPLACE, not append — two rows on one field would both write it
        /// and the last one to run would decide, which is a coin flip. The hydration system's
        /// settled ledger keys on this AND the target name, so a replaced target is applied again
        /// rather than passed over as already done.
        /// </remarks>
        public string Key
        {
            get
            {
                System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                return OwnerObjectName + "|" + (int)Link + "|" +
                    Index.ToString(inv) + "|" + EntryIndex.ToString(inv) + "|" +
                    OwnerVariation.ToString(inv);
            }
        }
    }

    /// <summary>
    /// Every reference in this mod's content that names an object the editor had no number for,
    /// filled by the generated bootstrap at load.
    /// </summary>
    /// <remarks>
    /// A re-registration REPLACES rather than appends, keyed on owner, field and position. The
    /// generated bootstrap calls <c>EnsureStaticRuntimeExtras</c> from both <c>EarlyInit</c> and
    /// <c>Init</c>, and a retry path can call it again; appending would leave the hydration system
    /// walking the same row twice and the settled ledger disagreeing with the row count forever.
    /// </remarks>
    public static class DimensionObjectLinkRegistry
    {
        private static readonly List<DimensionObjectLinkDefinition> Definitions =
            new List<DimensionObjectLinkDefinition>();

        public static IReadOnlyList<DimensionObjectLinkDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>
        /// Goes up on every registration, whether or not it changed anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE COUNT IS NOT A CHANGE DETECTOR, which is what the hydration system was using it as.
        /// A re-registration replaces on <c>Key</c>, and the key does not include the target — so
        /// pointing a field at a different object leaves the count exactly where it was while a
        /// live row has never been looked at, and the pass early-outs before reaching it. This
        /// moves whenever anything is registered.
        /// </para>
        /// <para>
        /// It is deliberately not "moves only on a real change". Comparing a row's every field to
        /// decide whether to bump would be a second definition of equality living beside
        /// <c>Key</c>, and getting it wrong is a settled pass that never wakes up. The cost of the
        /// blunt version is that a bootstrap registering the same rows again makes the settled
        /// pass walk them once more, which is a walk over a list, not work in the world.
        /// </para>
        /// </remarks>
        public static int Version
        {
            get { return version; }
        }

        private static int version;

        public static void Register(
            string ownerObjectName,
            DimensionObjectLink link,
            string targetObjectName,
            int index = -1,
            int targetVariation = 0,
            int ownerVariation = 0,
            int entryIndex = -1,
            int number = 0)
        {
            Register(new DimensionObjectLinkDefinition(
                ownerObjectName, link, targetObjectName, index, targetVariation, ownerVariation,
                entryIndex, number));
        }

        public static void Register(DimensionObjectLinkDefinition definition)
        {
            if (definition == null ||
                definition.Link == DimensionObjectLink.None ||
                string.IsNullOrEmpty(definition.OwnerObjectName) ||
                string.IsNullOrEmpty(definition.TargetObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(Definitions[i].Key, definition.Key, StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    version++;
                    return;
                }
            }

            Definitions.Add(definition);
            version++;
        }

        /// <summary>Empties the registry. Only for tests, which must not leak into each other.</summary>
        public static void Clear()
        {
            Definitions.Clear();
            version++;
        }
    }
}
