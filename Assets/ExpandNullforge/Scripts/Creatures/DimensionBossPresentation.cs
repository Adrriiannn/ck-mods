using System.Collections.Generic;
using ExpandNullforge.Authoring;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>One boss's face to the player: its map pin, its floating name, its body.</summary>
    public sealed class DimensionBossPresentationDefinition
    {
        public DimensionBossPresentationDefinition(
            string bossObjectName,
            string rawBossId,
            string nameTerm,
            bool showsOnTheMap,
            string hoverTerm = null)
        {
            BossObjectName = bossObjectName ?? string.Empty;
            RawBossId = rawBossId ?? string.Empty;
            NameTerm = nameTerm ?? string.Empty;
            ShowsOnTheMap = showsOnTheMap;
            HoverTerm = string.IsNullOrEmpty(hoverTerm) ? NameTerm : hoverTerm;
        }

        /// <summary>The mod-qualified object name — what resolves to an ObjectID at runtime.</summary>
        public readonly string BossObjectName;

        /// <summary>The raw authored id, used to find this boss's asset inside its template.</summary>
        public readonly string RawBossId;

        /// <summary>The localization term of the boss's name ("Names/…").</summary>
        public readonly string NameTerm;

        public readonly bool ShowsOnTheMap;

        /// <summary>The map hover's term — the name term unless the pin authored its own.</summary>
        public readonly string HoverTerm;

        /// <summary>The map icons and body sprite, attached once the mod's assets load.</summary>
        public Sprite LargeMapIcon;

        public Sprite MiniMapIcon;

        public Sprite BodySprite;
    }

    /// <summary>
    /// Every registered boss's presentation, fed in two halves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO HALVES BECAUSE THE DATA LIVES IN TWO PLACES. The names and terms are literals the
    /// generated bootstrap bakes at build time (<see cref="Register"/>). The sprites are Unity
    /// objects that only exist once the mod's bundle loads, so the bootstrap attaches them from
    /// the shipped template when its manifest arrives (<see cref="AttachIcons"/>). Neither half
    /// alone can render a pin; together they can.
    /// </para>
    /// <para>
    /// Read by the map hook (pin rows into <c>MapUI</c>) and by the boss view (name term and
    /// body sprite at occupy).
    /// </para>
    /// </remarks>
    public static class DimensionBossPresentationRegistry
    {
        private static readonly List<DimensionBossPresentationDefinition> Definitions =
            new List<DimensionBossPresentationDefinition>();

        private static readonly Dictionary<ObjectID, DimensionBossPresentationDefinition> ById =
            new Dictionary<ObjectID, DimensionBossPresentationDefinition>();

        public static IReadOnlyList<DimensionBossPresentationDefinition> All
        {
            get { return Definitions; }
        }

        public static void Register(DimensionBossPresentationDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.BossObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].BossObjectName,
                        definition.BossObjectName,
                        System.StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    ById.Clear();
                    return;
                }
            }

            Definitions.Add(definition);
            ById.Clear();
        }

        /// <summary>
        /// Attaches the sprite half from the mod's shipped template, matched by raw boss id.
        /// </summary>
        public static void AttachIcons(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int b = 0; b < bosses.Length; b++)
            {
                DimensionBossAsset boss = bosses[b];
                if (boss == null)
                {
                    continue;
                }

                for (int i = 0; i < Definitions.Count; i++)
                {
                    if (!string.Equals(Definitions[i].RawBossId, boss.BossId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Definitions[i].LargeMapIcon = boss.MapPin.LargeMapIcon;
                    Definitions[i].MiniMapIcon = boss.MapPin.MiniMapIcon;
                    Definitions[i].BodySprite = boss.Visual.BodySprite;
                    break;
                }
            }
        }

        /// <summary>The presentation for a boss's runtime id, or null for anything unregistered.</summary>
        public static DimensionBossPresentationDefinition For(ObjectID objectID)
        {
            if (objectID == ObjectID.None)
            {
                return null;
            }

            if (ById.Count == 0 && Definitions.Count > 0)
            {
                for (int i = 0; i < Definitions.Count; i++)
                {
                    ObjectID id = API.Authoring.GetObjectID(Definitions[i].BossObjectName);
                    if (id != ObjectID.None)
                    {
                        ById[id] = Definitions[i];
                    }
                }
            }

            DimensionBossPresentationDefinition definition;
            return ById.TryGetValue(objectID, out definition) ? definition : null;
        }

        public static void Clear()
        {
            Definitions.Clear();
            ById.Clear();
        }
    }
}
