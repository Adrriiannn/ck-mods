#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The portal offering window's contract, end to end at the edit-mode level: the ledger's
    /// pure slot-satisfaction rule and its report/forget/consume lifecycle, the travel
    /// requirement evaluator that answers item requirements from the ledger (including the
    /// "portal-offering-slot-missing" refusal that replaced the old silent one), the offering
    /// look defaults a required-item template is born with, and the generator-side
    /// EnsurePortalOffering wiring — duplicate item ids merge into one summed slot, the
    /// inventory window is sized to the merged count, the DimensionPortal look table matches,
    /// and a rule that stops asking for items strips the window off again.
    /// </summary>
    internal sealed class DimensionPortalOfferingTests
    {
        private const string PortalId = "test:offering-portal";
        private const string ItemId = "test:ember";

        private static readonly ObjectID ItemA = (ObjectID)5001;
        private static readonly ObjectID ItemB = (ObjectID)5002;

        private readonly List<Object> cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            // The ledger is static process state; every test starts from an empty one.
            DimensionPortalOfferingLedger.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < cleanup.Count; i++)
            {
                if (cleanup[i] != null)
                {
                    Object.DestroyImmediate(cleanup[i]);
                }
            }

            cleanup.Clear();
            DimensionPortalOfferingLedger.Clear();
        }

        [Test]
        public void SlotSatisfies_ExactItemAndAmountIsEnough()
        {
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemA, 2, ItemA, 2), Is.True);
        }

        [Test]
        public void SlotSatisfies_SurplusIsEnough()
        {
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemA, 5, ItemA, 2), Is.True);
        }

        [Test]
        public void SlotSatisfies_WrongItemFails()
        {
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemB, 2, ItemA, 2), Is.False);
        }

        [Test]
        public void SlotSatisfies_ShortAmountFails()
        {
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemA, 1, ItemA, 2), Is.False);
        }

        [Test]
        public void SlotSatisfies_EmptySlotAndNoneRequirementFail()
        {
            Assert.That(
                DimensionPortalOfferingLedger.SlotSatisfies(ObjectID.None, 0, ItemA, 1),
                Is.False);
            // A requirement of None can never be satisfied, even by a slot that also holds None.
            Assert.That(
                DimensionPortalOfferingLedger.SlotSatisfies(ObjectID.None, 1, ObjectID.None, 1),
                Is.False);
        }

        [Test]
        public void SlotSatisfies_RequiredAmountBelowOneClampsToOne()
        {
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemA, 1, ItemA, 0), Is.True);
            Assert.That(DimensionPortalOfferingLedger.SlotSatisfies(ItemA, 0, ItemA, 0), Is.False);
        }

        [Test]
        public void Ledger_ReportKnowsTryGetItemForgetRoundTrip()
        {
            Assert.That(DimensionPortalOfferingLedger.Knows(PortalId), Is.False);
            Assert.That(DimensionPortalOfferingLedger.HasAny, Is.False);

            ReportSingleItem(PortalId, ItemId, 2, 1, true);

            Assert.That(DimensionPortalOfferingLedger.Knows(PortalId), Is.True);
            Assert.That(DimensionPortalOfferingLedger.HasAny, Is.True);

            DimensionPortalOfferingLedger.ItemState state;
            Assert.That(
                DimensionPortalOfferingLedger.TryGetItem(PortalId, ItemId, out state),
                Is.True);
            Assert.That(state.Required, Is.EqualTo(2));
            Assert.That(state.Offered, Is.EqualTo(1));
            Assert.That(state.ConsumeOnTravel, Is.True);

            DimensionPortalOfferingLedger.Forget(PortalId);

            Assert.That(DimensionPortalOfferingLedger.Knows(PortalId), Is.False);
            Assert.That(
                DimensionPortalOfferingLedger.TryGetItem(PortalId, ItemId, out state),
                Is.False);
        }

        [Test]
        public void Ledger_IgnoresBlankIdsAndUnknownLookups()
        {
            DimensionPortalOfferingLedger.Report(
                string.Empty,
                new Dictionary<string, DimensionPortalOfferingLedger.ItemState>());
            Assert.That(DimensionPortalOfferingLedger.HasAny, Is.False);

            ReportSingleItem(PortalId, ItemId, 1, 0, false);
            DimensionPortalOfferingLedger.ItemState state;
            Assert.That(DimensionPortalOfferingLedger.Knows(null), Is.False);
            Assert.That(
                DimensionPortalOfferingLedger.TryGetItem(PortalId, "test:other", out state),
                Is.False);
            Assert.That(
                DimensionPortalOfferingLedger.TryGetItem("test:other-portal", ItemId, out state),
                Is.False);
            Assert.That(
                DimensionPortalOfferingLedger.TryGetItem(PortalId, null, out state),
                Is.False);
        }

        [Test]
        public void Ledger_ConsumptionQueueDrainsOnceAndDeduplicates()
        {
            ReportSingleItem(PortalId, ItemId, 1, 1, true);

            // Queueing twice must still consume once — the travel API may announce a departure
            // more than once for the same portal.
            DimensionPortalOfferingLedger.QueueConsumption(PortalId);
            DimensionPortalOfferingLedger.QueueConsumption(PortalId);

            List<string> drained;
            Assert.That(DimensionPortalOfferingLedger.TryDrainConsumption(out drained), Is.True);
            Assert.That(drained, Is.EqualTo(new List<string> { PortalId }));

            Assert.That(DimensionPortalOfferingLedger.TryDrainConsumption(out drained), Is.False);
            Assert.That(drained, Is.Null);
        }

        [Test]
        public void Ledger_ConsumptionQueueIgnoresUnknownPortals()
        {
            // Only portals the ledger currently knows can owe an offering.
            DimensionPortalOfferingLedger.QueueConsumption("test:never-reported");

            List<string> drained;
            Assert.That(DimensionPortalOfferingLedger.TryDrainConsumption(out drained), Is.False);
        }

        [Test]
        public void Evaluator_LeavesNonItemRequirementsUnhandled()
        {
            DimensionPortalOfferingRequirementEvaluator evaluator =
                new DimensionPortalOfferingRequirementEvaluator();

            DimensionTravelRequirementEvaluationResult result;
            bool handled = evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 1, DimensionTravelRequirementKind.ProgressFlag),
                out result);

            Assert.That(handled, Is.False);
            Assert.That(result.Evaluated, Is.False);
        }

        [Test]
        public void Evaluator_RefusesWhenThePortalHasNoOfferingSlot()
        {
            DimensionPortalOfferingRequirementEvaluator evaluator =
                new DimensionPortalOfferingRequirementEvaluator();

            DimensionTravelRequirementEvaluationResult result;
            bool handled = evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 1, DimensionTravelRequirementKind.Item),
                out result);

            Assert.That(handled, Is.True);
            Assert.That(result.Evaluated, Is.True);
            Assert.That(result.Satisfied, Is.False);
            Assert.That(result.Code, Is.EqualTo("portal-offering-slot-missing"));
            Assert.That(
                result.ProviderId,
                Is.EqualTo(DimensionPortalOfferingRequirementEvaluator.EvaluatorId));
        }

        [Test]
        public void Evaluator_MeetsWhenTheOfferingCoversTheRequirement()
        {
            ReportSingleItem(PortalId, ItemId, 2, 2, true);
            DimensionPortalOfferingRequirementEvaluator evaluator =
                new DimensionPortalOfferingRequirementEvaluator();

            DimensionTravelRequirementEvaluationResult result;
            bool handled = evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 2, DimensionTravelRequirementKind.Item),
                out result);

            Assert.That(handled, Is.True);
            Assert.That(result.Satisfied, Is.True, result.Message);
            Assert.That(result.Code, Is.Empty);
        }

        [Test]
        public void Evaluator_RefusesWhenTheOfferingFallsShort()
        {
            ReportSingleItem(PortalId, ItemId, 3, 1, true);
            DimensionPortalOfferingRequirementEvaluator evaluator =
                new DimensionPortalOfferingRequirementEvaluator();

            DimensionTravelRequirementEvaluationResult result;
            bool handled = evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 3, DimensionTravelRequirementKind.Item),
                out result);

            Assert.That(handled, Is.True);
            Assert.That(result.Satisfied, Is.False);
            Assert.That(result.Code, Is.EqualTo("portal-offering-incomplete"));
            Assert.That(result.Message, Does.Contain("1 offered"));
        }

        [Test]
        public void Evaluator_ClampsAZeroRequirementToOne()
        {
            // A required amount below one still asks for at least one item.
            ReportSingleItem(PortalId, ItemId, 0, 1, false);
            DimensionPortalOfferingRequirementEvaluator evaluator =
                new DimensionPortalOfferingRequirementEvaluator();

            DimensionTravelRequirementEvaluationResult result;
            evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 0, DimensionTravelRequirementKind.Item),
                out result);
            Assert.That(result.Satisfied, Is.True, result.Message);

            ReportSingleItem(PortalId, ItemId, 0, 0, false);
            evaluator.TryEvaluateTravelRequirement(
                BuildContext(PortalId, ItemId, 0, DimensionTravelRequirementKind.Item),
                out result);
            Assert.That(result.Satisfied, Is.False);
        }

        [Test]
        public void RequiredItemTemplate_IsBornWithTheGhostLook()
        {
            DimensionPortalRequiredItemTemplate template =
                new DimensionPortalRequiredItemTemplate(ItemId, "Ember", 2, true);

            Assert.That(
                template.SlotLook,
                Is.EqualTo(DimensionPortalOfferingLook.GhostOfTheItem));
            Assert.That(template.SlotSprite, Is.Null);
            Assert.That(template.SlotDimness, Is.EqualTo(0f));
        }

        [Test]
        public void EnsurePortalOffering_MergesDuplicateItemsAndSizesTheWindow()
        {
            GameObject root = CreatePortalRoot();
            DimensionPortalAccessRuleAsset rule = CreateRule(
                DimensionPortalActivationMode.RequiredItems,
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 2, false),
                new DimensionPortalRequiredItemTemplate(string.Empty, "Blank", 5, true),
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem Again", 3, true),
                new DimensionPortalRequiredItemTemplate("test:key", "Key", 1, false));

            InvokeEnsurePortalOffering(root, "Entry", rule);

            InventoryAuthoring inventory = root.GetComponent<InventoryAuthoring>();
            Assert.That(inventory, Is.Not.Null, "The offering window needs an inventory.");
            Assert.That(inventory.sizeX, Is.EqualTo(2), "Blank ids skip, duplicates merge.");
            Assert.That(inventory.sizeY, Is.EqualTo(1));

            DimensionPortalOfferingAuthoring offering =
                root.GetComponent<DimensionPortalOfferingAuthoring>();
            Assert.That(offering, Is.Not.Null);
            Assert.That(offering.entries.Count, Is.EqualTo(2));
            Assert.That(offering.entries[0].itemName, Is.EqualTo("test:gem"));
            Assert.That(offering.entries[0].amount, Is.EqualTo(5), "Duplicate amounts sum.");
            Assert.That(
                offering.entries[0].consumeOnTravel,
                Is.True,
                "Consume-on-travel survives a merge when either duplicate asks for it.");
            Assert.That(offering.entries[1].itemName, Is.EqualTo("test:key"));
            Assert.That(offering.entries[1].amount, Is.EqualTo(1));
        }

        [Test]
        public void OfferingLooks_LandOnTheVisualPortalInTheSameSlotOrderAsTheEntity()
        {
            // Slot index is the only thing tying the entity's buffer to the hint patch's look,
            // so the two writers must agree on order after the merge.
            DimensionPortalAccessRuleAsset rule = CreateRule(
                DimensionPortalActivationMode.RequiredItems,
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 2, false),
                new DimensionPortalRequiredItemTemplate(string.Empty, "Blank", 5, true),
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem Again", 3, true),
                new DimensionPortalRequiredItemTemplate("test:key", "Key", 1, false));

            GameObject entityRoot = CreatePortalRoot();
            InvokeEnsurePortalOffering(entityRoot, "Entry", rule);
            DimensionPortalOfferingAuthoring offering =
                entityRoot.GetComponent<DimensionPortalOfferingAuthoring>();

            DimensionPortal portal = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(portal, rule, false);

            DimensionPortal.OfferingSlotLook slot;
            for (int i = 0; i < offering.entries.Count; i++)
            {
                Assert.That(portal.TryGetOfferingLook(i, out slot), Is.True);
                Assert.That(
                    slot.itemName,
                    Is.EqualTo(offering.entries[i].itemName),
                    "Slot " + i + " must name the same item on both sides.");
                Assert.That(slot.amount, Is.EqualTo(offering.entries[i].amount));
            }

            Assert.That(
                portal.TryGetOfferingLook(offering.entries.Count, out slot),
                Is.False,
                "The look table must not outrun the entity's slots.");
        }

        [Test]
        public void EnsurePortalOffering_RowsWithNoItemCleanUpLikeAskingForNothing()
        {
            // A rule can say RequiredItems while every row is blank. That asks for nothing, so
            // a reused root must be left as clean as an explicit downgrade would leave it.
            GameObject root = CreatePortalRoot();
            InvokeEnsurePortalOffering(
                root,
                "Entry",
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true)));
            Assert.That(root.GetComponent<InventoryAuthoring>(), Is.Not.Null);

            InvokeEnsurePortalOffering(
                root,
                "Entry",
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate(string.Empty, "Blank", 4, true)));

            Assert.That(root.GetComponent<InventoryAuthoring>(), Is.Null);
            Assert.That(root.GetComponent<DimensionPortalOfferingAuthoring>(), Is.Null);
        }

        [Test]
        public void OfferingLooks_AloneDoNotOpenAWindow()
        {
            // The entry and return portals share one visual prefab, so the look table reaches
            // both. Only the entity's offering buffer may open a window; a return portal that
            // opened one would strand the player inside the dimension.
            DimensionPortal portal = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(
                portal,
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true)),
                false);

            DimensionPortal.OfferingSlotLook slot;
            Assert.That(
                portal.TryGetOfferingLook(0, out slot),
                Is.True,
                "The look table is present.");
            Assert.That(
                portal.HasOfferingWindow,
                Is.False,
                "A look table without an offering entity must never imply a window.");
            Assert.That(
                portal.IsOfferingSatisfiedLocally(),
                Is.True,
                "A portal with no window is never blocked by one.");
        }

        [Test]
        public void OfferingLooks_ItemPortalVisualNeverCarriesThem()
        {
            // The placed and the instant portals are different visual prefabs; only the placed
            // one can be asked for an offering.
            DimensionPortalAccessRuleAsset rule = CreateRule(
                DimensionPortalActivationMode.RequiredItems,
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true));

            DimensionPortal portal = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(portal, rule, true);

            DimensionPortal.OfferingSlotLook slot;
            Assert.That(portal.TryGetOfferingLook(0, out slot), Is.False);
        }

        [Test]
        public void OfferingLooks_AreClearedWhenTheRuleStopsAskingForItems()
        {
            // The visual prefab is regenerated in place, so a rule that stops asking must not
            // leave yesterday's ghosts behind.
            DimensionPortal portal = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(
                portal,
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true)),
                false);
            DimensionPortal.OfferingSlotLook slot;
            Assert.That(portal.TryGetOfferingLook(0, out slot), Is.True);

            InvokeApplyPortalOfferingLooks(
                portal,
                CreateRule(DimensionPortalActivationMode.VanillaCooldown),
                false);

            Assert.That(portal.TryGetOfferingLook(0, out slot), Is.False);
        }

        [Test]
        public void EnsurePortalOffering_RemovesTheWindowWhenTheRuleStopsAskingForItems()
        {
            GameObject root = CreatePortalRoot();
            DimensionPortalAccessRuleAsset asking = CreateRule(
                DimensionPortalActivationMode.RequiredItems,
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true));
            InvokeEnsurePortalOffering(root, "Entry", asking);
            Assert.That(root.GetComponent<InventoryAuthoring>(), Is.Not.Null);

            DimensionPortalAccessRuleAsset silent =
                CreateRule(DimensionPortalActivationMode.VanillaCooldown);
            InvokeEnsurePortalOffering(root, "Entry", silent);

            Assert.That(root.GetComponent<InventoryAuthoring>(), Is.Null);
            Assert.That(root.GetComponent<DimensionPortalOfferingAuthoring>(), Is.Null);
        }

        [Test]
        public void OfferingLooks_MergeKeepsAnAuthoredLookFromEitherRow()
        {
            // A default look is indistinguishable from an untouched one, so the merge must take
            // the first look an author actually chose rather than blindly the first row's.
            DimensionPortal authoredFirst = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(
                authoredFirst,
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate(
                        "test:gem", "Gem", 2, false,
                        DimensionPortalOfferingLook.Mystery, null, 0.4f),
                    new DimensionPortalRequiredItemTemplate("test:gem", "Gem Again", 3, true)),
                false);

            DimensionPortal.OfferingSlotLook slot;
            Assert.That(authoredFirst.TryGetOfferingLook(0, out slot), Is.True);
            Assert.That(slot.amount, Is.EqualTo(5), "Amounts still sum across the merge.");
            Assert.That(slot.look, Is.EqualTo(DimensionPortalOfferingLook.Mystery));
            Assert.That(slot.dimness, Is.EqualTo(0.4f).Within(0.0001f));

            DimensionPortal authoredSecond = CreateVisualPortal();
            InvokeApplyPortalOfferingLooks(
                authoredSecond,
                CreateRule(
                    DimensionPortalActivationMode.RequiredItems,
                    new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 2, false),
                    new DimensionPortalRequiredItemTemplate(
                        "test:gem", "Gem Again", 3, true,
                        DimensionPortalOfferingLook.Mystery, null, 0.4f)),
                false);

            Assert.That(authoredSecond.TryGetOfferingLook(0, out slot), Is.True);
            Assert.That(slot.amount, Is.EqualTo(5));
            Assert.That(
                slot.look,
                Is.EqualTo(DimensionPortalOfferingLook.Mystery),
                "An authored look on the later duplicate must not be silently discarded.");
            Assert.That(slot.dimness, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void EnsurePortalOffering_ReturnPortalNeverGainsAWindow()
        {
            // The return portal must always work — stranding a player inside a dimension is
            // never a feature — so even a rule full of required items adds nothing to it.
            GameObject root = CreatePortalRoot();
            DimensionPortalAccessRuleAsset rule = CreateRule(
                DimensionPortalActivationMode.RequiredItems,
                new DimensionPortalRequiredItemTemplate("test:gem", "Gem", 1, true));

            InvokeEnsurePortalOffering(root, "Return", rule);

            Assert.That(root.GetComponent<InventoryAuthoring>(), Is.Null);
            Assert.That(root.GetComponent<DimensionPortalOfferingAuthoring>(), Is.Null);
        }

        // ------------------------------------------- the Access card's create path --

        [Test]
        public void CreatePortalAccessRule_PlacedPortalIsBornCraftableAndIncluded()
        {
            // The repair button behind the Access card builds one rule through this factory, so a
            // repaired portal has to be born exactly as a brand-new dimension's is: shipped,
            // craftable, and waiting the game's own two seconds between uses.
            DimensionPortalAccessRuleAsset rule = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);

            Assert.That(rule.AccessKind, Is.EqualTo(DimensionPortalAccessKind.PlacedPortal));
            Assert.That(rule.Enabled, Is.True, "The placed portal is the out-of-the-box way in.");
            Assert.That(rule.Craftable, Is.True);
            Assert.That(rule.ActivationMode, Is.EqualTo(DimensionPortalActivationMode.VanillaCooldown));
            Assert.That(rule.EffectiveCooldownSeconds, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(rule.UsesRequiredItems, Is.False);
            Assert.That(rule.RequiredItems.Length, Is.EqualTo(0));
            Assert.That(rule.FromDimensionId, Is.EqualTo(DimensionIds.Overworld));
            Assert.That(
                rule.ToDimensionId,
                Is.EqualTo(StarterDimensionId),
                "A way in has to lead to the dimension, or the generator never matches it.");
            Assert.That(
                rule.GeneratedInWorld,
                Is.False,
                "Growing in the game's own world is opt-in, never a default.");
            Assert.That(rule.Droppable, Is.False);
            Assert.That(
                rule.CraftingStationObjectId,
                Is.Empty,
                "An empty bench is what makes it the Wooden Workbench.");
        }

        [Test]
        public void CreatePortalAccessRule_ItemPortalIsBornWithItsItemAndLeftOut()
        {
            DimensionPortalAccessRuleAsset rule = CreateStarterRule(
                DimensionPortalAccessKind.InventoryItem);

            Assert.That(rule.AccessKind, Is.EqualTo(DimensionPortalAccessKind.InventoryItem));
            Assert.That(
                rule.Enabled,
                Is.False,
                "The item portal starts out of the build so the placed one is the way in.");
            Assert.That(rule.IsItemPortal, Is.True);
            Assert.That(rule.PortalItemObjectId, Is.Not.Empty, "The item's name is seeded for it.");
            Assert.That(rule.ItemPortalDurationSeconds, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(rule.ToDimensionId, Is.EqualTo(StarterDimensionId));
        }

        [Test]
        public void CreatePortalAccessRule_ReturnPortalLeadsBackOutAndIsAlwaysOpen()
        {
            DimensionPortalAccessRuleAsset rule = CreateStarterRule(
                DimensionPortalAccessKind.GeneratedReturnPortal);

            Assert.That(
                rule.AccessKind,
                Is.EqualTo(DimensionPortalAccessKind.GeneratedReturnPortal));
            Assert.That(rule.Enabled, Is.True, "A dimension a player cannot leave is never shipped.");
            Assert.That(
                rule.ActivationMode,
                Is.EqualTo(DimensionPortalActivationMode.AlwaysAvailable));
            Assert.That(rule.EffectiveCooldownSeconds, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(rule.Interactable, Is.True);
            Assert.That(
                rule.FromDimensionId,
                Is.EqualTo(StarterDimensionId),
                "The way out is matched on the dimension it stands in, not the one it leads to.");
            Assert.That(rule.ToDimensionId, Is.EqualTo(DimensionIds.Overworld));
            Assert.That(rule.Craftable, Is.False, "Nobody crafts the way out.");
        }

        // ------------------------------- which rule owns which portal, and what is not --

        [Test]
        public void Ownership_TakesTheFirstEnabledRuleTheGeneratorWouldTake()
        {
            DimensionPortalAccessRuleAsset first = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);
            DimensionPortalAccessRuleAsset second = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);
            DimensionTemplateAsset template = CreateTemplate(first, second);

            Assert.That(
                DimensionPortalRuleOwnership.FindForTemplate(
                    template, DimensionPortalAccessKind.PlacedPortal),
                Is.SameAs(first));
            Assert.That(
                InvokeFindPortalRule(template, DimensionPortalAccessKind.PlacedPortal, false),
                Is.SameAs(first),
                "The studio and the generator must never name different owners.");
        }

        [Test]
        public void Ownership_ExtraEnabledRulesOfTheSameKindAreCountedAsIgnored()
        {
            // The warning chip on the Access card is exactly this count: more than one match means
            // everything after the first makes a service record and no portal.
            DimensionPortalAccessRuleAsset first = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);
            DimensionPortalAccessRuleAsset second = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);
            DimensionTemplateAsset template = CreateTemplate(first, second);

            List<DimensionPortalAccessRuleAsset> matches =
                DimensionPortalRuleOwnership.FindAllForTemplate(
                    template, DimensionPortalAccessKind.PlacedPortal);
            Assert.That(matches.Count, Is.EqualTo(2));
            Assert.That(matches[0], Is.SameAs(first), "The owner is always first.");

            second.SetEnabled(false);
            matches = DimensionPortalRuleOwnership.FindAllForTemplate(
                template, DimensionPortalAccessKind.PlacedPortal);
            Assert.That(
                matches.Count,
                Is.EqualTo(1),
                "A rule left out of the build is not being ignored, it is not shipping.");
        }

        [Test]
        public void Ownership_ARuleLeadingSomewhereElseOwnsNothing()
        {
            // The case the Access card offers to repair: the rule exists and is switched on, but
            // its destination no longer matches the dimension, so the generator builds no portal.
            DimensionPortalAccessRuleAsset stray = CreateStarterRule(
                DimensionPortalAccessKind.PlacedPortal);
            DimensionTemplateAsset template = CreateTemplate(stray);
            template.ApplyCustomizerMetadata(
                "test:renamed-dimension",
                "Renamed",
                string.Empty,
                "test:pack",
                "Pack",
                "1.0.0",
                string.Empty,
                1);

            Assert.That(
                DimensionPortalRuleOwnership.FindForTemplate(
                    template, DimensionPortalAccessKind.PlacedPortal),
                Is.Null);
            Assert.That(
                DimensionPortalRuleOwnership.FindAnyOfKind(
                    template, DimensionPortalAccessKind.PlacedPortal),
                Is.SameAs(stray),
                "The card still has to show the rule, or there is nothing to repair.");
        }

        [Test]
        public void Ownership_TheReturnPortalIsMatchedOnTheDimensionItStandsIn()
        {
            DimensionPortalAccessRuleAsset returnRule = CreateStarterRule(
                DimensionPortalAccessKind.GeneratedReturnPortal);
            DimensionTemplateAsset template = CreateTemplate(returnRule);

            Assert.That(
                DimensionPortalRuleOwnership.MatchesFromDimension(
                    DimensionPortalAccessKind.GeneratedReturnPortal),
                Is.True);
            Assert.That(
                DimensionPortalRuleOwnership.FindForTemplate(
                    template, DimensionPortalAccessKind.GeneratedReturnPortal),
                Is.SameAs(returnRule));
            Assert.That(
                InvokeFindPortalRule(
                    template, DimensionPortalAccessKind.GeneratedReturnPortal, true),
                Is.SameAs(returnRule));
        }

        // ------------------------------------ the bench a portal is really crafted at --

        [Test]
        public void CraftingStation_BlankMeansTheWoodenWorkbench()
        {
            // The field has always promised this, and for a long time it was true only because
            // every recipe was hardcoded to that bench regardless of what the rule said.
            Assert.That(
                InvokeResolveCraftingStationArgument(string.Empty),
                Is.EqualTo("ObjectID.WoodenWorkBench"));
            Assert.That(
                InvokeResolveCraftingStationArgument("   "),
                Is.EqualTo("ObjectID.WoodenWorkBench"));
        }

        [Test]
        public void CraftingStation_OneOfTheGamesBenchesBecomesItsNumberWhileBuilding()
        {
            Assert.That(
                InvokeResolveCraftingStationArgument("IronWorkBench"),
                Is.EqualTo("ObjectID.IronWorkBench"));
        }

        [Test]
        public void CraftingStation_AnUnknownBenchIsLookedUpByNameInTheGame()
        {
            // A bench belonging to another mod has no number until the world loads, so the name
            // has to survive into the generated code instead of collapsing to a fallback.
            Assert.That(
                InvokeResolveCraftingStationArgument("SomeOtherMod:GildedBench"),
                Is.EqualTo("\"SomeOtherMod:GildedBench\""));
        }

        // ------------------------------- growing the placed portal in the game's world --

        [Test]
        public void WorldSpawn_NamedPlacesBecomeTheGamesOwnBiomes()
        {
            string arguments = InvokeBuildOverworldSpawnArguments(
                new[] { "Slime", "Desert" },
                3,
                250);

            Assert.That(arguments, Does.Contain("maxOccurrences: 3"));
            Assert.That(arguments, Does.Contain("Biome.Slime"));
            Assert.That(arguments, Does.Contain("Biome.Desert"));
            Assert.That(arguments, Does.Contain("minDistanceFromCoreInClassicWorlds: 250"));
        }

        [Test]
        public void WorldSpawn_NoValidPlaceGrowsNothingAtAll()
        {
            // A scene with no biomes is invisible to the game's placer, so registering one would
            // be dead weight in the table rather than a portal nobody found yet.
            Assert.That(
                InvokeBuildOverworldSpawnArguments(new[] { "MyMod:GlassCaverns" }, 1, 0),
                Is.Empty);
            Assert.That(
                InvokeBuildOverworldSpawnArguments(new string[0], 1, 0),
                Is.Empty);
        }

        [Test]
        public void WorldSpawn_CountsBelowTheirFloorAreLifted()
        {
            string arguments = InvokeBuildOverworldSpawnArguments(new[] { "Slime" }, 0, -50);

            Assert.That(arguments, Does.Contain("maxOccurrences: 1"));
            Assert.That(arguments, Does.Contain("minDistanceFromCoreInClassicWorlds: 0"));
        }

        private static string InvokeResolveCraftingStationArgument(string stationObjectId)
        {
            MethodInfo method = typeof(DimensionRuntimeConsumerBootstrapUtility).GetMethod(
                "ResolveCraftingStationArgument",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            Assert.That(method, Is.Not.Null, "ResolveCraftingStationArgument moved or was renamed.");
            return (string)method.Invoke(null, new object[] { stationObjectId, "a test portal" });
        }

        private static string InvokeBuildOverworldSpawnArguments(
            string[] biomeNames,
            int maxOccurrences,
            int minDistanceFromCore)
        {
            MethodInfo method = typeof(DimensionRuntimeConsumerBootstrapUtility).GetMethod(
                "BuildOverworldSpawnArguments",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(string[]), typeof(int), typeof(int) },
                null);
            Assert.That(method, Is.Not.Null, "BuildOverworldSpawnArguments moved or was renamed.");
            return (string)method.Invoke(
                null,
                new object[] { "a test portal", biomeNames, maxOccurrences, minDistanceFromCore });
        }

        private const string StarterDimensionId = "test:starter-dimension";

        private DimensionPortalAccessRuleAsset CreateStarterRule(DimensionPortalAccessKind kind)
        {
            DimensionPortalAccessRuleAsset rule =
                DimensionTemplateStarterFactory.CreatePortalAccessRule(
                    kind,
                    StarterDimensionId,
                    "Starter Dimension");
            cleanup.Add(rule);
            return rule;
        }

        private DimensionTemplateAsset CreateTemplate(
            params DimensionPortalAccessRuleAsset[] rules)
        {
            DimensionTemplateAsset template =
                ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            cleanup.Add(template);
            template.name = "OwnershipTestDimension";
            template.ApplyCustomizerMetadata(
                StarterDimensionId,
                "Starter Dimension",
                string.Empty,
                "test:pack",
                "Pack",
                "1.0.0",
                string.Empty,
                1);
            template.SetPortalAccessRules(rules);
            return template;
        }

        /// <summary>
        /// The generator's own lookup, so a test can prove the studio agrees with it rather than
        /// merely agreeing with a second copy of the same idea.
        /// </summary>
        private static DimensionPortalAccessRuleAsset InvokeFindPortalRule(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind kind,
            bool matchFromDimension)
        {
            MethodInfo method = typeof(DimensionRuntimeConsumerBootstrapUtility).GetMethod(
                "FindPortalRule",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "FindPortalRule moved or was renamed.");
            return (DimensionPortalAccessRuleAsset)method.Invoke(
                null,
                new object[] { template, kind, template.DimensionId, matchFromDimension });
        }

        private static void ReportSingleItem(
            string portalId,
            string itemName,
            int required,
            int offered,
            bool consumeOnTravel)
        {
            DimensionPortalOfferingLedger.Report(
                portalId,
                new Dictionary<string, DimensionPortalOfferingLedger.ItemState>
                {
                    {
                        itemName,
                        new DimensionPortalOfferingLedger.ItemState
                        {
                            Required = required,
                            Offered = offered,
                            ConsumeOnTravel = consumeOnTravel,
                        }
                    },
                });
        }

        private static DimensionTravelRequirementEvaluationContext BuildContext(
            string portalId,
            string subjectId,
            int requiredAmount,
            DimensionTravelRequirementKind kind)
        {
            // The same shape NullforgeDimensionService hands evaluators: the access context
            // carries the portal id, the requirement carries the item and amount.
            DimensionAccessContext travel = new DimensionAccessContext(
                Entity.Null,
                default(DimensionContext),
                default(DimensionDefinition),
                float2.zero,
                float2.zero,
                false,
                false,
                portalId,
                "test");
            DimensionTravelRequirementDefinition requirement =
                new DimensionTravelRequirementDefinition(
                    "test:req",
                    string.Empty,
                    portalId,
                    "test:dimension",
                    kind,
                    subjectId,
                    requiredAmount,
                    true,
                    string.Empty,
                    0,
                    true);
            return new DimensionTravelRequirementEvaluationContext(travel, requirement, "test");
        }

        /// <summary>
        /// The entity prefab root as generation really sees it. It deliberately carries no
        /// <see cref="DimensionPortal"/>: that component lives on the visual prefab, and a test
        /// root that owned one would hide exactly the mismatch these tests exist to catch.
        /// </summary>
        private GameObject CreatePortalRoot()
        {
            GameObject root = new GameObject("OfferingPortalTestRoot");
            cleanup.Add(root);
            return root;
        }

        private DimensionPortal CreateVisualPortal()
        {
            GameObject root = new GameObject("OfferingVisualTestRoot");
            cleanup.Add(root);
            return root.AddComponent<DimensionPortal>();
        }

        private static void InvokeApplyPortalOfferingLooks(
            DimensionPortal portal,
            DimensionPortalAccessRuleAsset rule,
            bool itemPortal)
        {
            MethodInfo method = typeof(DimensionRuntimeConsumerBootstrapUtility).GetMethod(
                "ApplyPortalOfferingLooks",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "ApplyPortalOfferingLooks moved or was renamed.");
            method.Invoke(null, new object[] { portal, rule, itemPortal });
        }

        private DimensionPortalAccessRuleAsset CreateRule(
            DimensionPortalActivationMode activationMode,
            params DimensionPortalRequiredItemTemplate[] requiredItems)
        {
            DimensionPortalAccessRuleAsset rule =
                ScriptableObject.CreateInstance<DimensionPortalAccessRuleAsset>();
            cleanup.Add(rule);
            rule.Configure(
                "test:rule",
                PortalId,
                "test:presentation",
                "Offering Test Portal",
                DimensionIds.Overworld,
                Vector2.zero,
                "test:dimension",
                Vector2.zero,
                DimensionPortalAccessKind.PlacedPortal,
                activationMode,
                0f,
                true,
                true,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                true,
                requiredItems);
            return rule;
        }

        /// <summary>
        /// EnsurePortalOffering is private because only prefab generation calls it, and its
        /// variant parameter is a private nested enum — reflection is the only road in that
        /// does not widen the production surface.
        /// </summary>
        private static void InvokeEnsurePortalOffering(
            GameObject root,
            string variantName,
            DimensionPortalAccessRuleAsset rule)
        {
            MethodInfo method = typeof(DimensionRuntimeConsumerBootstrapUtility).GetMethod(
                "EnsurePortalOffering",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "EnsurePortalOffering moved or was renamed.");

            Type variantType = method.GetParameters()[1].ParameterType;
            object variant = Enum.Parse(variantType, variantName);
            method.Invoke(null, new object[] { root, variant, rule });
        }
    }
}
#endif
