using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The roles an object plays in the world, and the components everything gets.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// What every generated object gets, whatever it is.
        /// </summary>
        /// <remarks>
        /// <c>AnimationAuthoring</c> is on more vanilla prefabs than any other component. Without it
        /// nothing we generate can play an animation at all — not a chest lid, not a creature's walk,
        /// not a plant swaying. <c>IgnoreVertexOffsetsAuthoring</c> opts out of Core Keeper's vertex
        /// wobble, which is the same idea the tileset side already exposes as "rigid surface".
        /// </remarks>
        public static void ApplyUniversal(GameObject root, bool rigid)
        {
            EnsureComponent<AnimationAuthoring>(root);

            if (rigid)
            {
                EnsureComponent<IgnoreVertexOffsetsAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<IgnoreVertexOffsetsAuthoring>(root);
            }
        }

        /// <summary>
        /// The state machine anything damageable needs.
        /// </summary>
        /// <remarks>
        /// <c>StateAuthoring</c> is the root the others hang off; without <c>DeathState</c> a thing
        /// with health absorbs hits forever, and without <c>TookDamageState</c> it never visibly
        /// reacts. On 1,267+ vanilla prefabs together, chests and plants included.
        /// </remarks>
        public static void ApplyDamageableStates(GameObject root)
        {
            EnsureComponent<StateAuthoring>(root);
            EnsureComponent<IdleStateAuthoring>(root);
            EnsureComponent<TookDamageStateAuthoring>(root);
            EnsureComponent<DeathStateAuthoring>(root);
        }

        /// <summary>
        /// Ties an object to the difficulty of wherever it ends up.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is how Core Keeper scales health and damage by area — the curves in
        /// <c>HealthAuthoring</c>, <c>MeleeAttackStateAuthoring</c> and friends all read it in
        /// <c>OnValidate</c>.
        /// </para>
        /// <para>
        /// CONDITIONAL, and that is the whole point. <c>HealthAuthoring</c> offers an opt-out
        /// (<c>dontCalculateHealthFromLevel</c>) but the ATTACK components do not: their
        /// <c>OnValidate</c> overwrites damage unconditionally whenever a level is present. So adding
        /// this to a creature whose author asked for exact numbers would make authored attack damage
        /// impossible to keep — the precise failure this framework exists to prevent. It goes on only
        /// when the author chose to scale with the area instead.
        /// </para>
        /// </remarks>
        public static void ApplyAreaLevel(GameObject root, bool scaleWithArea)
        {
            if (scaleWithArea)
            {
                EnsureComponent<AreaLevelAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<AreaLevelAuthoring>(root);
            }
        }

        /// <summary>
        /// <summary>
        /// Which way a placed thing faces, what text it comes with, and how long it is untouchable.
        /// </summary>
        /// <remarks>
        /// <c>RotationAuthoring</c>, <c>DescriptionAuthoring</c> and the one-frame form of
        /// <c>CantBeAttackedAuthoring</c> were all attached with nothing set. The description one is
        /// worth naming: <c>initialText</c> is the text a sign or a labelled chest comes with, which
        /// is a different thing from the tooltip the localization path writes.
        /// </remarks>
        public static void ApplyFacingAndText(
            GameObject root,
            Vector3 initialFacing,
            bool colliderTurnsToo,
            int rotationIconOffset,
            string textItComesWith,
            bool untouchableForOneFrameOnly,
            bool alreadyUntouchableForever,
            System.Action<string> report)
        {
            RotationAuthoring rotation = root.GetComponent<RotationAuthoring>();
            if (rotation != null)
            {
                rotation.initialDirection = new Unity.Mathematics.float3(
                    initialFacing.x,
                    initialFacing.y,
                    initialFacing.z);
                rotation.rotatePhysics = colliderTurnsToo;
                rotation.rotationIconOffset = rotationIconOffset;
            }

            DescriptionAuthoring description = EnsureComponent<DescriptionAuthoring>(root);
            description.initialText = textItComesWith ?? string.Empty;

            if (!untouchableForOneFrameOnly)
            {
                CantBeAttackedAuthoring existing = root.GetComponent<CantBeAttackedAuthoring>();
                if (existing != null)
                {
                    existing.removeAfterFirstFrame = false;
                }

                return;
            }

            if (alreadyUntouchableForever)
            {
                if (report != null)
                {
                    report(
                        "is untouchable forever and also marked untouchable for one frame only. " +
                        "The permanent one wins, so the one-frame rule does nothing.");
                }

                return;
            }

            EnsureComponent<CantBeAttackedAuthoring>(root).removeAfterFirstFrame = true;
        }

        /// The world tier a thing belongs to, and which ways its art can face.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Both components were already being attached with nothing set, which is the most
        /// misleading state a generator can leave: present, therefore assumed configured.
        /// </para>
        /// <para>
        /// The tier is the one that changes numbers. <c>AreaLevelAuthoring</c> is what the health
        /// and damage curves read, so leaving it at Slime made every generated object compute
        /// starting-area stats no matter which biome it was built for.
        /// </para>
        /// </remarks>
        public static void ApplyBasics(
            GameObject root,
            DimensionObjectBasicsTemplate basics,
            System.Action<string> report,
            bool theTierMayBeAdded = true)
        {
            if (basics == null)
            {
                basics = new DimensionObjectBasicsTemplate();
            }

            // Facing is safe to write on anything: AnimationAuthoring is already universal and
            // orientation support changes nothing but whether the art may turn.
            EnsureComponent<AnimationAuthoring>(root).orientationSupport =
                (AnimationAuthoring.OrientationSupport)(int)basics.Facing;

            // THE TIER IS NOT SAFE TO ADD, and this pass used to add it anyway. AreaLevelAuthoring
            // is the level trap: with it present, MeleeAttackStateAuthoring.OnValidate recomputes
            // attack damage from the curve and offers no way out. A creature whose stats are set to
            // "the numbers you typed are kept exactly" has ApplyAreaLevel take the component off
            // during its state machine — and then this ran, four hundred lines later, and put it
            // straight back on, on the same object, in the same generate. The comment three lines
            // above said it only filled in a tier that was already there; the code below it did not.
            //
            // So a caller that has already decided says so, and the two controls are put in touch
            // instead of fighting.
            if (basics.ScalesWithItsTier)
            {
                if (theTierMayBeAdded)
                {
                    EnsureComponent<AreaLevelAuthoring>(root).areaLevel =
                        (AreaLevel)(int)basics.AreaTier;
                    return;
                }

                AreaLevelAuthoring alreadyThere = root.GetComponent<AreaLevelAuthoring>();
                if (alreadyThere != null)
                {
                    alreadyThere.areaLevel = (AreaLevel)(int)basics.AreaTier;
                    return;
                }

                if (report != null)
                {
                    report(
                        "is set to keep the numbers you typed exactly, and also to scale with its " +
                        "tier. It cannot do both: the game works an attacker's damage back out of " +
                        "the tier and there is no way to tell it not to, so your numbers would be " +
                        "thrown away. It keeps your numbers and the tier does nothing. Set its " +
                        "stats to the area level curve if you want the tier to decide them.");
                }

                return;
            }

            AreaLevelAuthoring level = root.GetComponent<AreaLevelAuthoring>();
            if (level != null)
            {
                // Creatures and plants add it themselves through their own stat source. Filling in
                // the tier there is free; adding it here would not be.
                level.areaLevel = (AreaLevel)(int)basics.AreaTier;
                return;
            }

            if (basics.ChoseATierThatWillBeIgnored && report != null)
            {
                report(
                    "was given the " + basics.AreaTier + " tier without ticking 'scales with its " +
                    "tier', and nothing on it reads a tier, so the choice does nothing.");
            }
        }

        /// <summary>
        /// The creatures that arrive alongside this one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The component holds real prefab references, not names, so this can only run once the
        /// companions exist. That makes it a second pass over the creature folder rather than part
        /// of configuring each creature — a creature listed before its companion would otherwise
        /// find nothing and generate silently without it.
        /// </para>
        /// <para>
        /// A companion that resolves to nothing is reported rather than dropped, because an empty
        /// companion list looks exactly like a creature that was never meant to have company.
        /// </para>
        /// </remarks>
        public static void ApplyCompanions(
            GameObject root,
            string[] companionIds,
            bool follow,
            Vector3 appearAt,
            System.Func<string, GameObject> findCompanion,
            System.Action<string> report)
        {
            if (companionIds == null || companionIds.Length == 0)
            {
                RemoveComponentIfPresent<SpawnCompanionsAuthoring>(root);
                return;
            }

            System.Collections.Generic.List<GameObject> found =
                new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < companionIds.Length; i++)
            {
                GameObject companion = findCompanion == null
                    ? null
                    : findCompanion(companionIds[i]);
                if (companion == null)
                {
                    if (report != null)
                    {
                        report(
                            "arrives with '" + companionIds[i] + "', which this mod does not " +
                            "generate as a creature, so it will arrive alone.");
                    }

                    continue;
                }

                found.Add(companion);
            }

            if (found.Count == 0)
            {
                RemoveComponentIfPresent<SpawnCompanionsAuthoring>(root);
                return;
            }

            SpawnCompanionsAuthoring companions = EnsureComponent<SpawnCompanionsAuthoring>(root);
            companions.companions = found;
            companions.follow = follow;
            companions.spawnOffset = new Unity.Mathematics.float3(appearAt.x, appearAt.y, appearAt.z);
        }

        /// <summary>
        /// Puts a health pool back on an unbreakable object whose feature is written over one.
        /// </summary>
        /// <remarks>
        /// "Cannot be attacked" takes the pool off, which is right for scenery and wrong for the
        /// handful of features that use health as a dial rather than as damage: both boss beams
        /// and the hive egg name it as something they WRITE, and are skipped entirely without it.
        /// Core Keeper ships all three of those carrying "cannot be attacked" and a pool together.
        /// Nothing else is put back — no mineable, no hurt state — so the object stays untouchable.
        /// </remarks>
        private static void GiveItBackTheHealthAnUnbreakableThingStillNeeds(
            GameObject root,
            System.Action<string> report,
            string sentence)
        {
            if (root == null || !HasNamed(root, "CantBeAttackedAuthoring"))
            {
                return;
            }

            if (root.GetComponent<HealthAuthoring>() != null)
            {
                return;
            }

            HealthAuthoring pool = EnsureComponent<HealthAuthoring>(root);
            pool.dontCalculateHealthFromLevel = true;
            pool.maxHealth = 1;
            pool.startHealth = 1;
            pool.maxHealthMultiplier = 1f;

            SayWhenTicked(true, report, sentence);
        }

        /// <summary>Whether the object's picture carries a seat the game can sit somebody on.</summary>
        /// <remarks>
        /// Looked up by type NAME through the picture's children rather than by type, because the
        /// seat marker lives in a game assembly and this is the only place that asks about it.
        /// </remarks>
        private static bool ThereIsSomewhereToSit(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
            if (identity == null || identity.graphicalPrefab == null)
            {
                return false;
            }

            Component[] parts = identity.graphicalPrefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].GetType().Name == "SittableObject")
                {
                    return true;
                }
            }

            return false;
        }

        /// <param name="isDeferred">
        /// Answers "this name is one of the mod's own, and the runtime will fill the number in".
        /// Null means the caller has no way to tell, which is the old behaviour: an unresolved name
        /// is treated as a mistake.
        /// </param>
        /// <param name="qualify">
        /// Turns one of the mod's own local ids into the full name the game registers it under. The
        /// summoning list is the one thing here that ships a NAME rather than a number, so without
        /// this a mod boss would be written down as "Emberlord" where the game knows it as
        /// "MyMod:Emberlord" and the idol would find nothing.
        /// </param>
        public static void ApplyWorldRoles(
            GameObject root,
            DimensionWorldRolesTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            System.Func<string, string> qualify = null)
        {
            if (root == null || world == null)
            {
                return;
            }

            Toggle<FireflyAuthoring>(root, world.IsAFirefly);
            Toggle<FireSpreading.Authoring.FireSpreaderAuthoring>(root, world.SpreadsFire);
            Toggle<ConvertToTileAuthoring>(root, world.BecomesGroundWhenItSettles);
            Toggle<RootAuthoring>(root, world.IsARoot);
            Toggle<ManaBarrierAuthoring>(root, world.IsAManaBarrier);
            // BOTH OF THESE GIVE THE OBJECT A "USE", and Core Keeper's step that builds a use
            // reaches into the object's picture for its first usable part and takes it by index —
            // so on a picture with none, the generate throws. Guarded the same way the door is.
            bool canSwitchOffImmunityZones =
                world.SwitchesOffImmunityZones &&
                DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<DisableImmuneZoneAuthoring>(root, canSwitchOffImmunityZones);
            if (canSwitchOffImmunityZones)
            {
                // The system that does the switching off names the zone itself, so an object that
                // only carries the switch is never looked at. The game's own zone emitter carries
                // both. Sized to the switch's own reach when the author has not set one.
                ImmunityZoneAuthoring zone = EnsureComponent<ImmunityZoneAuthoring>(root);
                if (zone.radius <= 0f)
                {
                    zone.radius = VanillaSummonCircleNoticeRadius;
                }
            }

            SayWhenTicked(
                world.SwitchesOffImmunityZones && !canSwitchOffImmunityZones,
                report,
                "switches off immunity zones, and that was not written: switching one off is " +
                "something a player does BY USING the object, and there is nothing on it to use. " +
                "Set what using it does, and give it a picture.");

            Toggle<IsHabitableIdolAuthoring>(root, world.CreaturesCanLiveHere);
            SayWhenTicked(
                world.CreaturesCanLiveHere,
                report,
                "says creatures can live here. In Core Keeper that only adds a line to the tooltip " +
                "when you point at it. No creature will move in because of it.");

            bool canBeAGrave =
                world.IsAPlayerGrave && DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<PlayerGraveAuthoring>(root, canBeAGrave);
            SayWhenTicked(
                world.IsAPlayerGrave && !canBeAGrave,
                report,
                "is a player's grave, and that was not written: a grave is picked up by using it, " +
                "and there is nothing on this a player can use. Set what using it does, and give " +
                "it a picture.");
            Toggle<Affixes.Authoring.AffixAuthoring>(root, world.CarriesAnAffix);

            if (world.AuraReachOverride > 0f)
            {
                EnsureComponent<AuraDistanceOverrideAuthoring>(root).distance =
                    world.AuraReachOverride;
            }
            else
            {
                RemoveComponentIfPresent<AuraDistanceOverrideAuthoring>(root);
            }

            if (world.LetsThePlayerKeepMoving)
            {
                EnsureComponent<MoveFreelyWeaponAuthoring>(root).moveSpeedMultiplier =
                    world.MovingSpeedMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<MoveFreelyWeaponAuthoring>(root);
            }

            WriteMinion(
                root,
                world.IsAMinion,
                world.MinionHitsThisHardForItsTier,
                world.MinionMinesToo,
                world.MinionMinesThisHardForItsTier,
                report);

            if (world.IsASummoningItem && !world.SummonsNothing)
            {
                SummoningItemAuthoring summoner = EnsureComponent<SummoningItemAuthoring>(root);
                summoner.availableBossesToSummon =
                    new System.Collections.Generic.List<ObjectID>();

                // The mod's own bosses ride here as names. This is NOT the object-link table: a
                // summoning item's list is a buffer that the game's own SummoningItemConverter
                // fills from ObjectIDs, and DimensionSummoningItemConverter already appends names
                // into the same SummoningItemBuffer at load. Reusing that path is what makes a mod
                // boss summonable at all; before this the name was silently dropped and the idol
                // did nothing on a circle, forever, with nothing said at generate.
                System.Collections.Generic.List<string> ourBosses =
                    new System.Collections.Generic.List<string>();

                string[] bosses = world.SummonsAnyOf;
                for (int i = 0; i < bosses.Length; i++)
                {
                    ObjectID boss = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(bosses[i]);
                    if (boss != ObjectID.None)
                    {
                        summoner.availableBossesToSummon.Add(boss);
                    }
                    else if (isDeferred != null && isDeferred(bosses[i]))
                    {
                        ourBosses.Add(qualify == null ? bosses[i] : qualify(bosses[i]));
                    }
                    else if (report != null)
                    {
                        report(
                            "summons '" + bosses[i] + "', which is neither one of this mod's " +
                            "bosses nor one the game has, so that one is left off the list.");
                    }
                }

                // Written whole rather than appended to, so a boss removed from the list actually
                // goes. The item generator's own summoning pass runs AFTER this one and unions its
                // boss-asset names in, which is the one order in which neither pass can erase the
                // other's work.
                if (ourBosses.Count > 0)
                {
                    EnsureComponent<ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root)
                        .bossObjectNames = ourBosses;
                }
                else
                {
                    RemoveComponentIfPresent<
                        ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
                }

                if (summoner.availableBossesToSummon.Count == 0 && ourBosses.Count == 0)
                {
                    RemoveComponentIfPresent<SummoningItemAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<SummoningItemAuthoring>(root);
                RemoveComponentIfPresent<
                    ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
            }

            if (world.IsATitanShrine && !world.ShrineHasNoTitan)
            {
                ObjectID titan = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.BoundTitanId);
                bool titanIsOurs =
                    titan == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(world.BoundTitanId);
                if (titan == ObjectID.None && !titanIsOurs)
                {
                    RemoveComponentIfPresent<TitanShrineAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a shrine bound to '" + world.BoundTitanId + "', which the game does " +
                            "not have, so it is an ordinary object.");
                    }
                }
                else
                {
                    // The component stays even for one of the mod's own titans: TitanShrineConverter
                    // always writes TitanShrineCD, and that component is what the link hydration
                    // fills in at load. Strip it and there is nothing left to write into.
                    EnsureComponent<TitanShrineAuthoring>(root).TitanObjectID = titan;
                }
            }
            else
            {
                RemoveComponentIfPresent<TitanShrineAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.SummonsNothing)
            {
                report(
                    "is a summoning item that names nothing to summon, so putting it on a circle " +
                    "does nothing.");
            }

            if (world.ShrineHasNoTitan)
            {
                report("is a titan shrine with no titan bound to it.");
            }

            if (world.MinionMiningWillBeIgnored)
            {
                report(
                    "sets how hard its minion mines without letting it mine, so that number is " +
                    "never read.");
            }
        }

        public static void ApplyTrader(
            GameObject root,
            DimensionTraderTemplate trader,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || trader == null)
            {
                return;
            }

            if (trader.IsATrader && !trader.TradesWithNothingToSell)
            {
                MerchantAuthoring merchant = EnsureComponent<MerchantAuthoring>(root);
                merchant.items = new System.Collections.Generic.List<MerchantItemInfo>();
                DimensionTradeGood[] stock = trader.Stock;
                for (int i = 0; i < stock.Length; i++)
                {
                    ObjectID sold = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(stock[i].ObjectId);
                    if (sold == ObjectID.None &&
                        !(isDeferred != null && isDeferred(stock[i].ObjectId)))
                    {
                        if (report != null)
                        {
                            report(
                                "sells '" + stock[i].ObjectId + "', which is neither one of this " +
                                "mod's objects nor one the game has, so that one is left off the " +
                                "shelf.");
                        }

                        continue;
                    }

                    // A PLACEHOLDER AT THE RIGHT POSITION for one of the mod's own. The shop is a
                    // buffer the link hydration writes into by position, and dropping the line here
                    // would slide every later one along so the write landed on the wrong item.

                    merchant.items.Add(new MerchantItemInfo
                    {
                        objectID = sold,
                        amount = stock[i].Amount,
                        requirementToBeAvailable =
                            (MerchantItemRequirement)(int)stock[i].AppearsAfter
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<MerchantAuthoring>(root);
            }

            Toggle<CantBeSoldAuthoring>(root, trader.CannotBeSold);

            if (trader.CoinValue > 0)
            {
                EnsureComponent<CoinAmountAuthoring>(root).Value = trader.CoinValue;
            }
            else
            {
                RemoveComponentIfPresent<CoinAmountAuthoring>(root);
            }

            SoulID soul;
            if (!string.IsNullOrEmpty(trader.GivesSoul) &&
                System.Enum.TryParse(trader.GivesSoul, false, out soul) && soul != SoulID.None)
            {
                EnsureComponent<SoulOrbAuthoring>(root).givesSoul = soul;
            }
            else
            {
                RemoveComponentIfPresent<SoulOrbAuthoring>(root);
                if (!string.IsNullOrEmpty(trader.GivesSoul) && report != null)
                {
                    report(
                        "grants soul '" + trader.GivesSoul + "', which the game does not have, so " +
                        "collecting it grants nothing.");
                }
            }

            Season season;
            if (!string.IsNullOrEmpty(trader.BelongsToSeason) &&
                System.Enum.TryParse(trader.BelongsToSeason, false, out season))
            {
                SeasonObjectAuthoring seasonal = EnsureComponent<SeasonObjectAuthoring>(root);
                seasonal.belongsToSeason = season;
                seasonal.removeFromWorldWhenOutOfSeason = trader.VanishesOutOfSeason;
            }
            else
            {
                RemoveComponentIfPresent<SeasonObjectAuthoring>(root);
                if (!string.IsNullOrEmpty(trader.BelongsToSeason) && report != null)
                {
                    report(
                        "belongs to season '" + trader.BelongsToSeason + "', which the game does " +
                        "not have, so it is there all year.");
                }
            }

            if (report == null)
            {
                return;
            }

            if (trader.TradesWithNothingToSell)
            {
                report("is a trader with nothing on its shelf, so talking to it offers no trades.");
            }

            if (trader.StockWillBeIgnored)
            {
                report(
                    "lists things to sell without being a trader, so none of them are ever offered.");
            }
        }

        public static void ApplyObjectRoles(
            GameObject root,
            DimensionObjectRolesTemplate roles,
            System.Action<string> report)
        {
            if (root == null || roles == null)
            {
                return;
            }

            // A CHAIR HAS TO HAVE SOMEWHERE TO SIT. Core Keeper's step that makes an object
            // sittable reaches into the object's picture for the marker that says where the
            // sitter's body goes, and takes the first one by index without checking there is one —
            // so on a chair whose picture has no sit position, the generate throws rather than the
            // chair being unsittable. Every one of the game's thirty chairs, stools, thrones,
            // boats and minecarts has that marker on its picture.
            bool thereIsSomewhereToSit = roles.CanBeSatOn && ThereIsSomewhereToSit(root);
            Toggle<SittableAuthoring>(root, thereIsSomewhereToSit);
            SayWhenTicked(
                roles.CanBeSatOn && !thereIsSomewhereToSit,
                report,
                "can be sat on, and that was not written: its picture has no seat on it, so the " +
                "game has nowhere to put the sitter. A seat is a marker inside the picture saying " +
                "where the body goes and which way it faces, the way the game's own chairs and " +
                "boats carry one. Until the picture has one, this object cannot be sat on.");

            Toggle<NameAuthoring>(root, roles.CanBeNamed);
            Toggle<NonHittableAuthoring>(root, roles.NothingCanHitIt);

            if (roles.CanBePainted)
            {
                EnsureComponent<PaintToolAuthoring>(root).paintIndex = roles.StartingPaint;

                // The label says the paint tool works on it. The component underneath makes the
                // object A PAINT TOOL — all fourteen things in the game that carry it are paint
                // brushes — and being paintable is a different answer the framework already writes
                // on every placed object. Said rather than changed, because taking it off would
                // silently drop the starting paint the author chose.
                SayWhenTicked(
                    true,
                    report,
                    "is set so the paint tool works on it. Everything this framework places can " +
                    "already be painted; what this particular answer does is make the object a " +
                    "paint brush of its own, which is almost certainly not what was wanted. Turn " +
                    "it off unless you are building a brush.");
            }
            else
            {
                RemoveComponentIfPresent<PaintToolAuthoring>(root);
            }

            if (roles.SizeCanBeChanged)
            {
                EnsureComponent<ResizableTileSizeAuthoring>(root).StartOnSmallestSize =
                    roles.StartsSmallest;

                SayWhenTicked(
                    true,
                    report,
                    "is set so its footprint can be changed after it is placed. Core Keeper reads " +
                    "that off the TOOL in the player's hand, not off the thing being placed — all " +
                    "thirteen things in the game with it are hoes, shovels, seeders and watering " +
                    "cans — so a placed object gets a resize button that has nothing to read.");
            }
            else
            {
                RemoveComponentIfPresent<ResizableTileSizeAuthoring>(root);
            }

            if (roles.IsADiscovery)
            {
                EnsureComponent<CanBeDiscoveredAuthoring>(root).distanceToDiscover =
                    roles.DiscoveredWithin;
            }
            else
            {
                RemoveComponentIfPresent<CanBeDiscoveredAuthoring>(root);
            }

            if (roles.StartsImmune == DimensionDamageImmunity.LeaveItAlone)
            {
                RemoveComponentIfPresent<ImmuneToDamageAuthoring>(root);
            }
            else
            {
                ImmuneToDamageAuthoring immune = EnsureComponent<ImmuneToDamageAuthoring>(root);
                immune.defaultValue = (ImmuneToDamageState)(int)roles.StartsImmune;

                EffectID turnedAway;
                if (!string.IsNullOrEmpty(roles.TurnedAwayEffectId) &&
                    System.Enum.TryParse(roles.TurnedAwayEffectId, false, out turnedAway))
                {
                    immune.defaultEffectIDOverride = turnedAway;
                }
                else if (!string.IsNullOrEmpty(roles.TurnedAwayEffectId) && report != null)
                {
                    report(
                        "shows effect '" + roles.TurnedAwayEffectId + "' when something is turned " +
                        "away by its immunity, which the game does not have, so it shows the usual one.");
                }
            }

            if (report == null)
            {
                return;
            }

            if (roles.PaintWillBeIgnored)
            {
                report(
                    "starts painted a colour the paint tool cannot touch, so it stays unpainted.");
            }

            if (roles.DiscoveryDistanceWillBeIgnored)
            {
                report(
                    "sets how close a player must get to discover it without being a discovery at " +
                    "all, so walking past does nothing.");
            }
        }

        public static void ApplyReactsToNearby(
            GameObject root,
            DimensionReactsToNearbyTemplate reacts,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || reacts == null)
            {
                return;
            }

            // ---- an object placed nearby ----
            if (reacts.ReactsToANearbyObject && !reacts.WatchesForNothing)
            {
                ObjectID watched = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(reacts.WatchesForObjectId);
                if (watched == ObjectID.None)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "watches for '" + reacts.WatchesForObjectId + "' being placed near it, " +
                            "which the game does not have, so it never reacts.");
                    }
                }
                else
                {
                    ChangeVariationWhenObjectNearbyAuthoring nearby =
                        EnsureComponent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                    nearby.objectID = watched;
                    nearby.objectNearbySpecificVariation = reacts.OnlyOneLookOfIt;
                    nearby.objectNearbyVariation = reacts.ItsLook;
                    nearby.radius = reacts.WithinDistance;
                    nearby.variationToChangeTo = reacts.ChangesToLook;
                    nearby.dontRevertToOriginalVariation = reacts.StaysChanged;
                    nearby.triggerActivateAnimation = reacts.PlaysItsActivationAnimation;
                    nearby.ignorePlayerFaction = reacts.AnyonesObjectCounts;
                    nearby.offset = new Unity.Mathematics.float3(
                        reacts.LooksAtOffset.x,
                        reacts.LooksAtOffset.y,
                        reacts.LooksAtOffset.z);
                }
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
            }

            // ---- an object merely held ----
            if (reacts.ReactsToAHeldObject && !reacts.WatchesForNothingHeld)
            {
                ObjectID held = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(reacts.WatchesForHeldObjectId);
                if (held == ObjectID.None)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "reacts to a player holding '" + reacts.WatchesForHeldObjectId +
                            "', which the game does not have, so it never reacts.");
                    }
                }
                else
                {
                    ChangeVariationWhenPlayerHoldObjectNearbyAuthoring holding =
                        EnsureComponent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                    holding.objectID = held;
                    holding.radius = reacts.HeldWithinDistance;
                    holding.variationToChangeTo = reacts.HeldChangesToLook;
                    holding.alsoRemoveCollider = reacts.AlsoClearsItsCollider;
                    holding.offset = new Unity.Mathematics.float3(
                        reacts.HeldLooksAtOffset.x,
                        reacts.HeldLooksAtOffset.y,
                        reacts.HeldLooksAtOffset.z);
                }
            }
            else
            {
                RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
            }

            // ---- whether it can be used at all ----
            if (reacts.UsableDependsOnItsLook)
            {
                ToggleInteractionOnVariationAuthoring toggle =
                    EnsureComponent<ToggleInteractionOnVariationAuthoring>(root);
                toggle.toggleType = (ToggleInteractionByVariationType)(int)reacts.UsableRule;
                toggle.variation = reacts.TheLookInQuestion;
            }
            else
            {
                RemoveComponentIfPresent<ToggleInteractionOnVariationAuthoring>(root);
            }

            // ---- shoving ----
            if (reacts.ShovesNearbyThings && !reacts.ShovesWithNoForce)
            {
                AddForceToNearbyEntitiesAuthoring shove =
                    EnsureComponent<AddForceToNearbyEntitiesAuthoring>(root);
                shove.radius = reacts.ShoveReaches;
                shove.force = reacts.ShoveForce;
                shove.forceDuringActivation = reacts.ShoveForceWhileWindingUp;
                shove.checkLineOfSight = reacts.OnlyShovesWhatItCanSee;
                shove.activationDelay = reacts.ShoveWindUp;
                shove.activeDuration = reacts.ShoveLasts;
                shove.inactiveDuration = reacts.RestsBetweenShoves;
                shove.activeForceMultiplierCurve = reacts.ShoveStrengthOverTime;
            }
            else
            {
                RemoveComponentIfPresent<AddForceToNearbyEntitiesAuthoring>(root);
                if (reacts.ShovesWithNoForce && report != null)
                {
                    report(
                        "is set to shove things away with no force behind it, so nothing moves. " +
                        "Give it a shove force.");
                }
            }
        }
    }
}
