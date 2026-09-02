using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a world object does once it is there: stock, breaking, drops.
    /// </summary>
    internal static partial class DimensionWorldObjectGenerator
    {
        /// <summary>
        /// The one or two markers that make it a door, a bed or a trophy rather than decoration.
        /// </summary>
        /// <summary>
        /// Stamps or clears the shop's stock. The names ride to runtime and hydrate into the
        /// game's own vending buffer, so the buy window and pricing are entirely vanilla's.
        /// </summary>
        private static void ApplyShopStock(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report)
        {
            DimensionInteractionTemplate interaction = worldObject.Interaction;
            bool sells = interaction != null &&
                         interaction.WhatUsingItDoes == DimensionUseBehaviour.SellsLikeAShop;
            if (!sells)
            {
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionShopStockAuthoring>(root);
                return;
            }

            string[] soldIds = interaction.SoldItemIds;
            if (soldIds.Length == 0)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' sells like a shop but stocks nothing, " +
                    "so its window opens empty.");
            }

            ExpandNullforge.Creatures.DimensionShopStockAuthoring stock =
                EnsureComponent<ExpandNullforge.Creatures.DimensionShopStockAuthoring>(root);
            stock.itemNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < soldIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(soldIds[i]))
                {
                    stock.itemNames.Add(naming.QualifyReference(soldIds[i]));
                }
            }

            stock.sizeX = interaction.ShopGrid.x;
            stock.sizeY = interaction.ShopGrid.y;
        }

        private static void ApplyBreaking(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionWorldObjectGenerationReport report)
        {
            if (worldObject.CannotBeAttacked)
            {
                // THE ONE-FRAME FLAG SURVIVED A REGENERATE. This reuses whatever the reloaded
                // prefab had, and the pass that owns the one-frame answer returns without clearing
                // it on exactly this branch — so an object that was once "untouchable for one frame
                // only" and is now "cannot be attacked" kept the stale true, the game stripped its
                // protection a frame later, and the generation report said in plain words that the
                // permanent rule had won. It is written here because this is the branch that makes
                // that claim.
                EnsureComponent<CantBeAttackedAuthoring>(root).removeAfterFirstFrame = false;

                RemoveComponentIfPresent<MineableAuthoring>(root);
                RemoveComponentIfPresent<HealthAuthoring>(root);
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);

                // FIRST, OR THE REMOVAL BELOW IS REFUSED AND SAYS SO ON EVERY GENERATE. An earlier
                // pass writes "changes look when hit", and that component requires the took-damage
                // state — so trying to take the state off logs a warning naming the object, every
                // time, and the look-change could never fire anyway because nothing can damage an
                // object that cannot be attacked.
                if (root.GetComponent<ChangeVariationWhenTookDamageAuthoring>() != null)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenTookDamageAuthoring>(root);
                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' changes its look when it is hit and " +
                        "also cannot be attacked. Nothing can hit it, so the look never changes; " +
                        "that answer was left off.");
                }

                RemoveComponentIfPresent<TookDamageStateAuthoring>(root);

                // NOT DAMAGEABLE IS NOT THE SAME AS NOT ALIVE. Three separate features on an
                // unbreakable object are gated on it having a state at all: music near it only
                // switches between its combat and its quiet track for something the game sees as
                // idle (MusicAreaSystem asks for StateInfoCD and IdleStateCD), a trader's shelves
                // are only stocked for something with a state (MerchantBuyInventorySystem), and a
                // trap that attacks continuously writes its state every tick
                // (AttackContinuouslyStateSystem). A decorative, unbreakable music source is the
                // natural setting for every one of those, and it used to switch them all off.
                // Idle and the state root cost an unbreakable object nothing — they are how it says
                // "nothing is happening" — while death and took-damage stay off, which is what
                // "cannot be attacked" actually means.
                EnsureComponent<StateAuthoring>(root);
                EnsureComponent<IdleStateAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CantBeAttackedAuthoring>(root);
                EnsureComponent<MineableAuthoring>(root);
                DimensionObjectSpine.ApplyDamageableStates(root);

                HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
                health.dontCalculateHealthFromLevel = true;
                health.maxHealth = worldObject.HitsToBreak;
                health.startHealth = worldObject.HitsToBreak;
                health.maxHealthMultiplier = 1f;

                DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
                reduction.calculateReductionFromLevel = false;
                reduction.reductionMultiplier = 1f;
                reduction.reduction = 0;
                reduction.maxDamagePerHit = DamagePerHit;
                reduction.minDamagePerHit = 0;
            }

            if (worldObject.DisappearsOnItsOwn)
            {
                // PlatformDependentValue, not a float: Core Keeper can give a different lifetime per
                // console. The one-argument constructor is how it says "the same everywhere", which
                // is what an author who typed one number means.
                EnsureComponent<DestroyTimerAuthoring>(root).lifetime =
                    new Pug.UnityExtensions.PlatformDependentValue<float>(
                        worldObject.DisappearsAfterSeconds);
            }
            else
            {
                RemoveComponentIfPresent<DestroyTimerAuthoring>(root);
            }
        }

        private static void Warn(
            DimensionWorldObjectAsset worldObject,
            DimensionWorldObjectGenerationReport report)
        {
            if (worldObject.GlowsButLightsNothing)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' glows but lights nothing. In Core Keeper those " +
                    "are separate components — a glow tints the object, it does not light the room. " +
                    "If it is meant to be a lamp, tick that it lights the room when placed.");
            }

            if (worldObject.IsATrophyThatSummonsNothing)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' is a trophy with no enemy named, so using it " +
                    "will summon nothing.");
            }

            if (worldObject.CanNeverBeRemoved)
            {
                report.Warnings.Add(
                    "'" + worldObject.DisplayName + "' cannot be attacked and never expires, so a " +
                    "player who places one can never take it back.");
            }
        }

        /// <summary>
        /// Writes what other items said drops from this object.
        /// </summary>
        /// <remarks>
        /// A world object can be a drop source too — breaking rubble or a pot is exactly the
        /// <c>Destructible</c> kind. Anything needing an amount range or a biome is left for the
        /// bootstrap to register against a loot table, since per-object custom loot carries neither.
        /// </remarks>
        private static void ApplyCollectedDrops(
            GameObject root,
            DimensionDropsForSource dropsFromThis,
            string objectIdentifier,
            DimensionWorldObjectGenerationReport report)
        {
            // TAKEN BACK BEFORE THE EARLY RETURN, and that is the point. Generation reloads the
            // prefab it wrote last time, so deleting every drop off this object used to leave last
            // time's drops baked on it with no way to take them off, and a renamed mod left it
            // pointing at a loot table minted from the old name that nothing ever fills.
            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(root);

            if (dropsFromThis == null || dropsFromThis.Drops.Count == 0)
            {
                return;
            }

            List<DimensionResolvedDrop> needsATable = DimensionDropEmitter.ApplyCustomLoot(
                root,
                dropsFromThis,
                delegate(string itemId) { return ResolveObject(itemId); },
                delegate(string message) { report.Warnings.Add(message); },
                IsDeferred);

            for (int i = 0; i < needsATable.Count; i++)
            {
                report.DropsNeedingALootTable.Add(needsATable[i]);
            }

            // Something that breaks and gives an item needs a loot table for anything custom loot
            // cannot carry. Most world objects are authored without one, and given none there was
            // nowhere for the drop to be registered at load, so it never happened.
            if (needsATable.Count > 0)
            {
                DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                    root,
                    binder.Naming.QualifyGenerated(objectIdentifier),
                    delegate(string message) { report.Warnings.Add(message); });
            }
        }
    }
}
