using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What starts a fight, and the warnings for a creature that cannot have one.
    /// </summary>
    internal static partial class DimensionCreatureGenerator
    {
        private static List<ObjectCategoryTag> ParseTags(
            string[] names,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            List<ObjectCategoryTag> parsed = new List<ObjectCategoryTag>();
            for (int i = 0; i < names.Length; i++)
            {
                ObjectCategoryTag tag;
                if (Enum.TryParse(names[i], false, out tag))
                {
                    parsed.Add(tag);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' names category '" + names[i] +
                        "', which the game does not have. That part of the rule was left out.");
                }
            }

            return parsed;
        }

        /// <summary>The two categories every hostile creature in the game goes after.</summary>
        /// <remarks>
        /// Read straight off the game's own prefabs: Larva and Caveling both author
        /// <c>wantsToAttackTags = [Player, HostileCreature]</c> and nothing else. HostileCreature
        /// is what makes a charmed pet, a minion and a rival faction's mob fightable — leaving it
        /// out would produce a creature that hunts players and stands next to a summoned ally.
        /// </remarks>
        private static readonly ObjectCategoryTag[] FightsPlayersAndMonsters =
        {
            ObjectCategoryTag.Player,
            ObjectCategoryTag.HostileCreature
        };

        /// <summary>
        /// Turns the chosen temperament into the components that decide whether it fights.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This runs AFTER <see cref="ApplyPerception"/> and <see cref="ApplyChase"/> because it
        /// overwrites what both of them wrote. The temperament is the plain-language question; the
        /// faction and category tags under Attacks are the expert one, and an author who wants to
        /// answer the expert question chooses Custom, which leaves both attack tag lists exactly
        /// as typed.
        /// </para>
        /// <para>
        /// THREE vanilla levers, not two. Measured across all 83 of the game's creature prefabs
        /// that carry a chase, plus the code that reads them:
        /// </para>
        /// <para>
        /// 1. <c>wantsToAttackTags</c> gates chase-target selection —
        /// <c>ChaseStateRequest</c> will not pick ANY target that fails
        /// <c>WantsToAndCanAttack</c> (`ck-db\Pug.Other\ChaseStateRequest.cs:216`), and that check
        /// runs for the last attacker too. Every hostile prefab in the game authors exactly
        /// <c>[Player, HostileCreature]</c>; all twelve cattle prefabs author an EMPTY list, which
        /// is what makes a cow a cow.
        /// </para>
        /// <para>
        /// 2. <c>cantAttackTags</c> gates the swing itself. <c>MeleeAttackStateRequest</c> consults
        /// <c>WantsToAttack</c> only while the creature is already chasing a DIFFERENT target
        /// (`MeleeAttackStateRequest.cs:136-139`); otherwise only the cannot-list stands between it
        /// and anything adjacent. <c>RangeAttackStateRequest.cs:160</c> is the same shape. Vanilla
        /// cattle leave this empty and get away with it only because they carry no attack at all;
        /// a Passive creature the author gave an attack needs the list filled or it will still hit
        /// whoever brushes past it.
        /// </para>
        /// <para>
        /// 3. The creature's OWN <c>Cattle</c> category tag is what marks it harmless to everything
        /// else. Every one of the twelve cattle prefabs tags itself
        /// <c>HostileCreature + Cattle</c>; every hostile tags itself <c>HostileCreature</c> alone.
        /// <c>Cattle</c> is not decoration: all ten pet prefabs and all six minion prefabs list it
        /// in their own <c>cantAttackTags</c>, and the player's "kill everything" command skips any
        /// entity carrying it (`ck-db\Pug.Other\PlayerCommand\ServerSystem.cs:235`). Without it a
        /// Passive animal is butchered by the first tamed pet that walks past, which is not what
        /// anybody means by passive.
        /// </para>
        /// <para>
        /// Defensive's own lever is the fourth, and it is NOT written here — see
        /// <see cref="Creatures.DimensionHoldsFireAuthoring"/> for why zeroing the authored chase
        /// distance breaks pathfinding, and what is done instead.
        /// </para>
        /// <para>
        /// Hostile and Defensive both ENSURE a chase exists, because both of them promise pursuit
        /// and neither can keep that promise without one. They only ever add a chase where there is
        /// none: running last means a borrowed behaviour's own tuning is already on the object and
        /// is never overwritten. Passive never touches the chase — a cow walks to its feed trough
        /// with it.
        /// </para>
        /// </remarks>
        private static void ApplyTemperament(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            bool passive = request.Aggression == DimensionSpawnAggressionKind.Passive;
            bool defensive = request.Aggression == DimensionSpawnAggressionKind.Defensive;

            // Cleanup that has to run for EVERY temper, Custom included. A prefab regenerated
            // after its temper changed must not keep the marker or the identity tag the previous
            // temper left on it — a creature that is no longer Defensive but still holds its fire
            // is exactly the kind of stale prefab nobody thinks to look for.
            if (defensive)
            {
                EnsureComponent<Creatures.DimensionHoldsFireAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<Creatures.DimensionHoldsFireAuthoring>(root);
            }

            // Cattle is an identity tag the generator owns, not one of the two attack lists an
            // author types, so Custom gets it cleared like everything else rather than inheriting
            // a harmlessness it never asked for.
            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.Cattle, passive);

            if (request.Aggression == DimensionSpawnAggressionKind.Custom)
            {
                WarnAboutCustomThatCannotFight(root, request, report);
                return;
            }

            BehaviourTagsAuthoring tags = EnsureComponent<BehaviourTagsAuthoring>(root);
            if (tags.wantsToAttackTags == null)
            {
                tags.wantsToAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.cantAttackTags == null)
            {
                tags.cantAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.eatsTags == null)
            {
                tags.eatsTags = new List<ObjectCategoryTag>();
            }

            for (int i = 0; i < FightsPlayersAndMonsters.Length; i++)
            {
                ObjectCategoryTag tag = FightsPlayersAndMonsters[i];
                SetTag(tags.wantsToAttackTags, tag, !passive);
                SetTag(tags.cantAttackTags, tag, passive);
            }

            switch (request.Aggression)
            {
                case DimensionSpawnAggressionKind.Passive:
                    WarnAboutAPassiveFighter(root, request, report);
                    break;

                case DimensionSpawnAggressionKind.Defensive:
                    EnsureAChaseToPursueWith(root, request, report, "Defensive");
                    WarnAboutADefenderThatCannotHitBack(root, request, report);
                    break;

                case DimensionSpawnAggressionKind.Hostile:
                    EnsureAChaseToPursueWith(root, request, report, "Hostile");
                    break;
            }
        }

        /// <summary>
        /// Gives a temper that promises pursuit something to pursue with.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creature with no <c>ChaseStateAuthoring</c> has no chase state, and without a chase
        /// state <c>ChaseStateRequest.ShouldUpdate</c> returns false for it forever — it can only
        /// ever swing at whatever is already touching it. Both Hostile ("hunts players down") and
        /// Defensive ("comes after whoever hit it") are claims about following someone, so both
        /// need one. Before this the generator only WARNED, which left the two most-used tempers
        /// producing a creature that stands still.
        /// </para>
        /// <para>
        /// How far it can see is the right distance to borrow: it is the only pursuit number the
        /// author has already given, and it is what <c>NearbyEntitiesTrackerAuthoring</c> was set
        /// to, so the creature cannot be told to chase further than it can notice. On a Defensive
        /// creature the distance never gates aggro at all — the runtime marker zeroes that — but it
        /// still sizes the path search, so it must be a real number rather than zero.
        /// </para>
        /// <para>
        /// It never overwrites a distance that is already there. Running last is what makes that
        /// safe: a borrowed behaviour ships chase distances tuned to how its attack works, and they
        /// are already on the object by the time this sees it.
        /// </para>
        /// </remarks>
        private static ChaseStateAuthoring EnsureAChaseToPursueWith(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report,
            string temperName)
        {
            float sightRadius = request.Combat != null ? request.Combat.DetectionRadius : 0f;
            ChaseStateAuthoring chase = root.GetComponent<ChaseStateAuthoring>();

            if (chase != null && chase.chaseAtDistance > 0f)
            {
                return chase;
            }

            if (sightRadius <= 0f)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is " + temperName + " but cannot see anything: " +
                    "its detection radius is zero and no chase distance was given, so it will " +
                    "never follow anyone and can only hit whatever is already within reach of " +
                    "its attack. Give it a detection radius under Stats.");
                return chase;
            }

            if (chase == null)
            {
                chase = EnsureComponent<ChaseStateAuthoring>(root);

                // Matching what the game's own creatures carry. Left at zero a chase enters the
                // state and then stands there, because the speed it moves at is this multiplied
                // by the creature's movement speed (`ChaseStateUtility.CalculateMovementSpeed`).
                chase.moveSpeedMultiplier = 1f;
            }

            chase.chaseAtDistance = sightRadius;
            return chase;
        }

        /// <summary>Says so when Custom has left a creature unable to pick a target at all.</summary>
        /// <remarks>
        /// The expert escape hatch is worth keeping, but its most likely outcome is silence: an
        /// author who chooses Custom and types nothing gets an empty <c>wantsToAttackTags</c>, and
        /// an empty wants list fails <c>WantsToAndCanAttack</c> for everything, so no chase target
        /// is ever picked. The field's own tooltip used to promise the faction would carry it,
        /// which is false — the faction check runs in ADDITION to the tag check
        /// (`ChaseStateRequest.cs:216`), never instead of it.
        /// </remarks>
        private static void WarnAboutCustomThatCannotFight(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            BehaviourTagsAuthoring tags = root.GetComponent<BehaviourTagsAuthoring>();
            bool wantsSomething = tags != null && tags.wantsToAttackTags != null &&
                                  tags.wantsToAttackTags.Count > 0;
            if (wantsSomething)
            {
                return;
            }

            bool armed = root.GetComponent<MeleeAttackStateAuthoring>() != null ||
                         root.GetComponent<RangeAttackStateAuthoring>() != null;
            if (!armed)
            {
                return;
            }

            report.Warnings.Add(
                "'" + request.DisplayName + "' has its temperament set to Custom and no attack " +
                "categories typed, so it will never choose anyone to chase — its faction cannot " +
                "carry that on its own. Type the categories it should go after under Attacks, or " +
                "choose Hostile, Defensive or Passive instead.");
        }

        /// <summary>Says so when a passive creature was given an attack it can never use.</summary>
        private static void WarnAboutAPassiveFighter(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (root.GetComponent<MeleeAttackStateAuthoring>() == null &&
                root.GetComponent<RangeAttackStateAuthoring>() == null)
            {
                return;
            }

            report.Warnings.Add(
                "'" + request.DisplayName + "' is Passive and also has an attack, which it will " +
                "never use — Passive tells it it may not hit players or other creatures. Set it " +
                "to Defensive if it should fight back when something hits it.");
        }

        /// <summary>Says so when a defensive creature has nothing to defend itself with.</summary>
        /// <remarks>
        /// <para>
        /// Being unarmed is the only way left to be a toothless defender, and it is the one an
        /// author cannot see in the prefab. A Defensive creature with no attack is a Passive one
        /// that has been told to retaliate: it acquires the person who hit it, walks to them, and
        /// stands there.
        /// </para>
        /// <para>
        /// Having no chase is no longer one of the ways, because <see cref="ApplyTemperament"/>
        /// now gives a defender one. Neither is pathfinding: the earlier implementation baked the
        /// chase distance to zero, and <c>PathFindingConversion.CreatePathfindingEntity</c> sizes
        /// the path search from that same number, so a defender that insisted on a path got a
        /// zero-radius search and could never move. That is exactly why the zero moved off the
        /// prefab and onto <see cref="Creatures.DimensionHoldsFireSystem"/>, which writes only
        /// <c>ChaseStateCD.chaseAtDistanceSq</c> and leaves the path search its real radius.
        /// Pathfinding and Defensive are now a legal combination, so nothing is said about it.
        /// </para>
        /// </remarks>
        private static void WarnAboutADefenderThatCannotHitBack(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (root.GetComponent<MeleeAttackStateAuthoring>() == null &&
                root.GetComponent<RangeAttackStateAuthoring>() == null)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is Defensive but has no attack, so nothing it " +
                    "does when hit will reach anyone. Give it an attack under Attacks, or set it " +
                    "to Passive.");
            }

            // Nothing is said here about a missing chase. The only way to reach this without one
            // is a detection radius of zero, and EnsureAChaseToPursueWith has already said exactly
            // that, naming the same fix — two warnings for one cause teaches authors to skim them.

            // The whole temper hangs off LastAttackerCD, and EnemyConverter is the only thing that
            // adds it. The player's own attack path only ever SETS that component where it already
            // exists — `ecb.SetComponent<LastAttackerCD>` guarded by `HasComponent` at
            // `ck-db\Pug.Other\EntityUtility.cs:843` — so a creature without the enemy tag never
            // learns who hit it and can never answer. Nothing in the studio can currently turn the
            // tag off, but this is the one combination that would fail completely and silently.
            if (!request.IsEnemy)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is Defensive but does not carry the game's " +
                    "enemy tag, so it never records who hit it and will never answer anyone. " +
                    "Defensive needs that tag; set it to Passive instead.");
            }
        }

        /// <summary>Adds or removes one category tag, without ever listing it twice.</summary>
        private static void SetTag(List<ObjectCategoryTag> tags, ObjectCategoryTag tag, bool present)
        {
            bool has = tags.Contains(tag);
            if (present && !has)
            {
                tags.Add(tag);
            }
            else if (!present && has)
            {
                tags.RemoveAll(delegate(ObjectCategoryTag t) { return t == tag; });
            }
        }
    }
}
