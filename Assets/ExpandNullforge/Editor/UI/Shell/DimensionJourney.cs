using ExpandNullforge.Api;
using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// One section of the framework, in the order a creator works them.
    /// </summary>
    internal readonly struct DimensionJourneyStage
    {
        internal DimensionJourneyStage(string sectionId, string name)
        {
            SectionId = sectionId;
            Name = name;
        }

        /// <summary>The existing panel this section shows, or empty for a section of its own.</summary>
        internal string SectionId { get; }

        internal string Name { get; }
    }

    /// <summary>
    /// The sections of the framework, in the order a dimension is actually built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is FIXED, and that is the whole point. The dashboard this replaces sorted its
    /// sections by how ready each one was, so the list reordered itself while a creator worked
    /// and "the third thing down" never stayed the third thing down. Navigation has to hold
    /// still to be navigation.
    /// </para>
    /// <para>
    /// The order itself is deliberate: the dimension exists first, then the portal that reaches
    /// it — because blocks are worth nothing if you cannot get through — then the ground, the
    /// biomes that name it, the map that arranges them, and the content that lives on top.
    /// World Generation composes everything built before it, and Review and Build closes.
    /// </para>
    /// </remarks>
    internal static class DimensionJourney
    {

        /// <summary>The Diagnostics page's section id, pinned at the rail's foot.</summary>
        internal const string DiagnosticsStageId = "diagnostics";

        private static readonly DimensionJourneyStage[] StageOrder =
        {
            new DimensionJourneyStage("dimension", "Dimension"),
            new DimensionJourneyStage("portals", "Portal Studio"),
            new DimensionJourneyStage("tilesets", "Tileset Studio"),
            new DimensionJourneyStage("biomes", "Biome Studio"),
            new DimensionJourneyStage("layout", "Layout Studio"),
            new DimensionJourneyStage("resources", "Item Studio"),
            new DimensionJourneyStage("nature", "Gardening Studio"),
            new DimensionJourneyStage("spawns", "Monster Studio"),
            new DimensionJourneyStage("scenes", "Dungeon Studio"),
            new DimensionJourneyStage("worldrules", "World Generation"),
            new DimensionJourneyStage("export", "Review and Build"),
        };

        internal static IReadOnlyList<DimensionJourneyStage> Stages
        {
            get { return StageOrder; }
        }

        internal static int Count
        {
            get { return StageOrder.Length; }
        }

        /// <summary>The stage index for a section id, or -1 when the section is not a stage.</summary>
        internal static int IndexOf(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return -1;
            }

            for (int i = 0; i < StageOrder.Length; i++)
            {
                if (string.Equals(StageOrder[i].SectionId, sectionId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static bool TryGetStage(string sectionId, out DimensionJourneyStage stage)
        {
            int index = IndexOf(sectionId);
            if (index < 0)
            {
                stage = default(DimensionJourneyStage);
                return false;
            }

            stage = StageOrder[index];
            return true;
        }

        /// <summary>
        /// Reads a stage's readiness out of the navigation model the framework already computes,
        /// so the rail agrees with the dashboard rather than judging readiness a second way.
        /// </summary>
        internal static DimensionAuthoringReadinessState ResolveState(
            DimensionTemplateCustomizerNavigationModel navigation,
            string sectionId)
        {
            if (navigation == null || navigation.Sections == null)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            for (int i = 0; i < navigation.Sections.Count; i++)
            {
                if (string.Equals(
                        navigation.Sections[i].SectionId,
                        sectionId,
                        System.StringComparison.Ordinal))
                {
                    return navigation.Sections[i].State;
                }
            }

            return DimensionAuthoringReadinessState.Missing;
        }

        /// <summary>
        /// Sections whose every field lives on a rebuilt page, so offering them again would
        /// only offer the same values in the old clothes.
        /// </summary>
        /// <remarks>
        /// Verified rather than assumed: the old terrain and generation pages both edited the
        /// same biome asset the Biome Studio edits, the old overview was a summary Home and
        /// Review and Build now carry between them, and the old placeables and bosses sections
        /// moved whole into Item Studio and Monster Studio as tabs. Nothing reachable was lost
        /// by dropping them; only a second door to the same room.
        /// </remarks>
        private static readonly string[] CoveredElsewhere =
        {
            "terrain",
            "generation",
            "overview",
            "placeables",
            "bosses",
        };

        /// <summary>
        /// Sections the framework knows about that neither the rail nor the pinned Diagnostics
        /// row can reach. Nothing may be silently unreachable, so a caller is expected to
        /// surface these somewhere rather than dropping them.
        /// </summary>
        internal static List<string> FindSectionsOutsideTheJourney(
            DimensionTemplateCustomizerNavigationModel navigation)
        {
            List<string> outside = new List<string>();
            if (navigation == null || navigation.Sections == null)
            {
                return outside;
            }

            for (int i = 0; i < navigation.Sections.Count; i++)
            {
                string sectionId = navigation.Sections[i].SectionId;
                if (IndexOf(sectionId) >= 0 ||
                    string.Equals(sectionId, DiagnosticsStageId, System.StringComparison.Ordinal) ||
                    System.Array.IndexOf(CoveredElsewhere, sectionId) >= 0)
                {
                    continue;
                }

                outside.Add(sectionId);
            }

            return outside;
        }
    }
}
