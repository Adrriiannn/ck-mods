#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Locks the promises the section rail makes: its sections stay in one fixed order, carry
    /// their decreed names, and everything the framework knows about is reachable. The dashboard
    /// this replaced sorted its sections by readiness, so the list reordered itself while a
    /// creator worked; these tests exist so that behaviour cannot creep back.
    /// </summary>
    internal sealed class DimensionJourneyTests
    {
        [Test]
        public void Stages_AreInTheDecreedOrder()
        {
            // The dimension exists first, then the portal that reaches it, then the ground, the
            // biomes, the map, and the content that lives on top. World Generation composes what
            // came before it; Review and Build closes. The array ending in "export" is the part
            // that matters most: finishing the last section has to mean the dimension is finished.
            string[] expected =
            {
                "dimension",
                "portals",
                "tilesets",
                "biomes",
                "layout",
                "resources",
                "nature",
                "spawns",
                "scenes",
                "worldrules",
                "export",
            };

            IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
            Assert.That(stages.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    stages[i].SectionId,
                    Is.EqualTo(expected[i]),
                    "Section " + i + " moved. The rail's order is a promise to the creator.");
            }
        }

        [Test]
        public void Stages_CarryTheirDecreedNames()
        {
            Assert.That(NameOf("dimension"), Is.EqualTo("Dimension"));
            Assert.That(NameOf("portals"), Is.EqualTo("Portal Studio"));
            Assert.That(NameOf("tilesets"), Is.EqualTo("Tileset Studio"));
            Assert.That(NameOf("biomes"), Is.EqualTo("Biome Studio"));
            Assert.That(NameOf("layout"), Is.EqualTo("Layout Studio"));
            Assert.That(NameOf("resources"), Is.EqualTo("Item Studio"));
            Assert.That(NameOf("nature"), Is.EqualTo("Gardening Studio"));
            Assert.That(NameOf("spawns"), Is.EqualTo("Monster Studio"));
            Assert.That(NameOf("scenes"), Is.EqualTo("Dungeon Studio"));
            Assert.That(NameOf("worldrules"), Is.EqualTo("World Generation"));
            Assert.That(NameOf("export"), Is.EqualTo("Review and Build"));

            IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                Assert.That(
                    stages[i].Name,
                    Does.Not.Contain("-"),
                    "Section names carry no hyphens.");
            }
        }

        [Test]
        public void PlaceablesAndBosses_AreNoLongerStages_ButStayCovered()
        {
            // Placeables was absorbed into Item Studio and Bosses into Monster Studio, so
            // neither may be a rail item — and neither may count as orphaned either, because
            // every one of their fields still has a page.
            Assert.That(DimensionJourney.IndexOf("placeables"), Is.EqualTo(-1));
            Assert.That(DimensionJourney.IndexOf("bosses"), Is.EqualTo(-1));

            DimensionTemplateCustomizerNavigationModel navigation = Navigation(
                Section("placeables"),
                Section("bosses"));
            List<string> outside = DimensionJourney.FindSectionsOutsideTheJourney(navigation);
            Assert.That(outside, Does.Not.Contain("placeables"));
            Assert.That(outside, Does.Not.Contain("bosses"));
        }

        [Test]
        public void EverySectionIdIsUniqueAndFindable()
        {
            HashSet<string> seen = new HashSet<string>();
            IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                Assert.That(
                    seen.Add(stages[i].SectionId),
                    Is.True,
                    "Two sections claim the id " + stages[i].SectionId + ".");
                Assert.That(DimensionJourney.IndexOf(stages[i].SectionId), Is.EqualTo(i));
            }

            Assert.That(DimensionJourney.IndexOf("not-a-section"), Is.EqualTo(-1));
            Assert.That(DimensionJourney.IndexOf(null), Is.EqualTo(-1));
        }

        [Test]
        public void Diagnostics_IsPinned_NotOrphaned()
        {
            // Diagnostics has no rail number: it is pinned at the rail's foot. It must therefore
            // never be reported as unreachable — and a genuinely unknown section still must be.
            DimensionTemplateCustomizerNavigationModel navigation = Navigation(
                Section("tilesets"),
                Section("overview"),
                Section("diagnostics"),
                Section("someday-a-new-section"));

            List<string> outside = DimensionJourney.FindSectionsOutsideTheJourney(navigation);

            Assert.That(
                outside,
                Does.Not.Contain("diagnostics"),
                "Diagnostics is reachable from its pinned row, so it is not outside.");
            Assert.That(
                outside,
                Does.Contain("someday-a-new-section"),
                "A section nothing reaches still has to be reported, or it ships orphaned.");
            Assert.That(
                outside,
                Does.Not.Contain("overview"),
                "Overview is covered by Home and by Review and Build.");
            Assert.That(
                outside,
                Does.Not.Contain("tilesets"),
                "A section that is a rail item is not also an extra.");
        }

        [Test]
        public void ResolveState_ReadsTheFrameworksOwnReadiness()
        {
            DimensionTemplateCustomizerNavigationModel navigation = Navigation(
                Section("tilesets", Api.DimensionAuthoringReadinessState.Ready),
                Section("biomes", Api.DimensionAuthoringReadinessState.Blocked));

            Assert.That(
                DimensionJourney.ResolveState(navigation, "tilesets"),
                Is.EqualTo(Api.DimensionAuthoringReadinessState.Ready));
            Assert.That(
                DimensionJourney.ResolveState(navigation, "biomes"),
                Is.EqualTo(Api.DimensionAuthoringReadinessState.Blocked));
            Assert.That(
                DimensionJourney.ResolveState(navigation, "portals"),
                Is.EqualTo(Api.DimensionAuthoringReadinessState.Missing),
                "A section the model does not mention has simply not been started.");
            Assert.That(
                DimensionJourney.ResolveState(null, "tilesets"),
                Is.EqualTo(Api.DimensionAuthoringReadinessState.Missing));
        }

        private static string NameOf(string sectionId)
        {
            DimensionJourneyStage stage;
            Assert.That(
                DimensionJourney.TryGetStage(sectionId, out stage),
                Is.True,
                "The rail lost its " + sectionId + " section.");
            return stage.Name;
        }

        private static DimensionTemplateCustomizerNavigationModel Navigation(
            params DimensionTemplateCustomizerSectionItem[] items)
        {
            List<DimensionTemplateCustomizerSectionItem> sections =
                new List<DimensionTemplateCustomizerSectionItem>(items);
            return new DimensionTemplateCustomizerNavigationModel(
                "test:dimension",
                "Test",
                sections.Count > 0 ? sections[0].SectionId : string.Empty,
                string.Empty,
                sections.Count,
                0,
                0,
                0,
                sections);
        }

        private static DimensionTemplateCustomizerSectionItem Section(
            string sectionId,
            Api.DimensionAuthoringReadinessState state =
                Api.DimensionAuthoringReadinessState.Partial)
        {
            return MakeSectionItem(sectionId, state);
        }

        // ---- the maturity chip, which is only honest if its ids resolve ----

        [Test]
        public void EveryStage_NamesACapabilityTheRegistryActuallyHas()
        {
            // A mistyped id is not an error anywhere — TryGet simply returns false and the chip
            // hides itself, so the section silently stops declaring how far it got. That is the
            // exact failure the registry was written to prevent, so it is a test.
            for (int i = 0; i < DimensionJourney.Stages.Count; i++)
            {
                string sectionId = DimensionJourney.Stages[i].SectionId;
                string capabilityId = DimensionStageMaturity.CapabilityForSection(sectionId);

                Assert.That(
                    capabilityId,
                    Is.Not.Empty,
                    "Stage '" + sectionId + "' declares no capability, so a creator opening it is " +
                    "told nothing about how finished it is.");

                Api.DimensionCapability capability;
                Assert.That(
                    Api.DimensionCapabilityRegistry.TryGet(capabilityId, out capability),
                    Is.True,
                    "Stage '" + sectionId + "' points at capability '" + capabilityId +
                    "', which the registry does not have.");
            }
        }

        [Test]
        public void TheDiagnosticsRow_DeclaresItsMaturityToo()
        {
            // Pinned at the rail's foot rather than among the stages, and just as capable of
            // being a page over a void.
            Api.DimensionCapability capability;
            Assert.That(
                DimensionStageMaturity.TryResolve(DimensionJourney.DiagnosticsStageId, out capability),
                Is.True);
        }

        private static DimensionTemplateCustomizerSectionItem MakeSectionItem(
            string sectionId,
            Api.DimensionAuthoringReadinessState state)
        {
            return new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Overview,
                state,
                sectionId,
                sectionId,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                0,
                false);
        }
    }
}
#endif
