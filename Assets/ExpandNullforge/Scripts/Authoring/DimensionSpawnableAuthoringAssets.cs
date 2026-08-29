using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

// The four spawnable ScriptableObject asset classes (Animal/Critter/Mob/Boss) moved to their
// own same-named files: Unity only binds .asset files to the MonoScript matching the filename.
// Only enums, plain serializable templates, and utilities may stay here.
namespace ExpandNullforge.Authoring
{
    public enum DimensionSpawnableKind
    {
        Animal = 0,
        Critter = 1,
        Mob = 2,
        Boss = 3
    }

    /// <summary>
    /// How a creature feels about players.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper has no aggression setting. Whether a creature comes after a player is decided
    /// by four things, three of which the generator writes onto the prefab and one of which is
    /// written on the server's first tick. All four were measured against the game's own prefabs
    /// before being used — see <c>DimensionCreatureGenerator.ApplyTemperament</c> for the full
    /// census and the file:line for each.
    /// </para>
    /// <para>
    /// <c>wantsToAttackTags</c> decides who is even a candidate:
    /// <c>ChaseStateRequest</c> tests it through <c>BehaviourTagsCD.WantsToAndCanAttack</c> before
    /// it will pick a chase target at all (`ck-db\Pug.Other\ChaseStateRequest.cs:216`), and it
    /// does so for the last attacker too, so a creature that wants nothing can never retaliate.
    /// <c>cantAttackTags</c> decides who it may swing at, and is the only one of the two consulted
    /// when the target is already adjacent (`MeleeAttackStateRequest.cs:136-139`,
    /// `RangeAttackStateRequest.cs:160`). The creature's own <c>Cattle</c> category tag decides
    /// whether everything ELSE treats it as prey. And <c>chaseAtDistanceSq</c> is the range the
    /// candidate check is measured against — except for whoever hit the creature last, where it is
    /// forced to 400, twenty tiles squared (`ChaseStateRequest.cs:224-228`), and held there for the
    /// whole chase (`ChaseStateSystem.cs:384-386`).
    /// </para>
    /// <para>
    /// Measured over all 83 of the game's creature prefabs that carry a chase: every hostile
    /// authors <c>wantsToAttackTags = [Player, HostileCreature]</c> and tags itself
    /// <c>HostileCreature</c>; all twelve cattle prefabs author an EMPTY wants list, carry no
    /// attack at all, and tag themselves <c>HostileCreature + Cattle</c>. Both kinds carry
    /// <c>EnemyAuthoring</c>, so "passive" is never expressed by dropping the enemy tag — that tag
    /// is what brings <c>LastAttackerCD</c>, without which nothing can ever fight back.
    /// </para>
    /// <para>
    /// No vanilla creature is Defensive; none ships a zero chase distance. Defensive is the
    /// framework's own combination of vanilla parts, not a borrowed one, which is why the mechanism
    /// is documented at length on <see cref="Creatures.DimensionHoldsFireAuthoring"/>.
    /// </para>
    /// <para>
    /// There is deliberately no Boss value here. Bossness is what a
    /// <see cref="DimensionBossAsset"/> IS, never a mob's temper — the retired value (3) made
    /// nothing a boss and taught authors otherwise. Old assets that saved it are clamped back
    /// to Hostile by <see cref="DimensionMobAsset"/>.
    /// </para>
    /// </remarks>
    public enum DimensionSpawnAggressionKind
    {
        /// <summary>
        /// It never attacks anyone, and nothing treats it as prey.
        /// </summary>
        /// <remarks>
        /// Players and other creatures are taken out of <c>wantsToAttackTags</c> and put into
        /// <c>cantAttackTags</c>, so it neither chooses a target nor swings at one that walks into
        /// it, and it is tagged <c>Cattle</c>, which every pet and minion in the game lists in its
        /// own cannot-attack list and which the player's kill-everything command skips. Its chase
        /// is left alone: a cow walks to its feed trough with one.
        /// </remarks>
        Passive = 0,

        /// <summary>
        /// It ignores everyone until it is hit, then it comes after whoever hit it.
        /// </summary>
        /// <remarks>
        /// It keeps a real chase distance so its path search is sized properly, and
        /// <see cref="Creatures.DimensionHoldsFireSystem"/> zeroes the aggro range alone on the
        /// server. Nothing then passes the proximity check, but the last attacker is measured
        /// against a forced twenty tiles, for the ten seconds <c>LastAttackerCD</c> remembers them
        /// and refreshed by every further hit.
        /// </remarks>
        Defensive = 1,

        /// <summary>
        /// It comes after any player it can see, and after whoever hits it from further off.
        /// </summary>
        /// <remarks>
        /// The last-attacker override is not exclusive to Defensive — a Hostile creature shot from
        /// beyond its chase distance still answers, because the same forced twenty tiles applies.
        /// Hostile simply also notices people who have not touched it.
        /// </remarks>
        Hostile = 2,

        /// <summary>
        /// Leave both attack category lists exactly as they were typed under Attacks.
        /// </summary>
        /// <remarks>
        /// The expert escape hatch, and reachable: the Attacks section is drawn field by field, so
        /// <c>wantsToAttackTags</c> and <c>cantAttackTags</c> are real controls an author can fill
        /// in with any of the game's forty-four category tags — hunting only Ore, or only a rival
        /// faction, is something the three plain tempers cannot express. It is kept for that and
        /// warns when it has been chosen and left empty, because an empty wants list means the
        /// creature chooses nobody rather than falling back on its faction.
        /// </remarks>
        Custom = 100
    }

    /// <summary>
    /// What a creature looks like.
    /// </summary>
    /// <remarks>
    /// The six naming strings that used to live here — prefab, sprite, icon, material, animation
    /// set and variant — were removed: they reached nothing but their own asset-reference rows,
    /// and the clip list below is what actually draws the creature.
    /// </remarks>
    [Serializable]
    public sealed class DimensionSpawnableVisualTemplate
    {
        [Tooltip("One still picture of it, used for its floating name plate and nothing else. " +
                 "What it looks like in the world comes from the clips below.")]
        [SerializeField] private Sprite bodySprite;

        [Tooltip("What it is seen doing: one row of pictures per thing, and how each one plays.")]
        [SerializeField] private DimensionCreatureAnimationTemplate animation =
            new DimensionCreatureAnimationTemplate();

        /// <summary>What it is seen doing, and how each clip plays.</summary>
        public DimensionCreatureAnimationTemplate Animation
        {
            get { return animation ?? new DimensionCreatureAnimationTemplate(); }
        }

        /// <summary>The actual sprite the generated view renders, or null when none is chosen.</summary>
        public Sprite BodySprite
        {
            get { return bodySprite; }
        }
    }

    [Serializable]
    public sealed class DimensionSpawnableAudioTemplate
    {
        [DimensionSoundName]
        [SerializeField] private string spawnSoundId = string.Empty;
        [DimensionSoundName]
        [SerializeField] private string idleSoundId = string.Empty;
        [DimensionSoundName]
        [SerializeField] private string aggroSoundId = string.Empty;
        [DimensionSoundName]
        [SerializeField] private string hitSoundId = string.Empty;
        [DimensionSoundName]
        [SerializeField] private string deathSoundId = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string SpawnSoundId
        {
            get { return spawnSoundId ?? string.Empty; }
        }

        public string IdleSoundId
        {
            get { return idleSoundId ?? string.Empty; }
        }

        public string AggroSoundId
        {
            get { return aggroSoundId ?? string.Empty; }
        }

        public string HitSoundId
        {
            get { return hitSoundId ?? string.Empty; }
        }

        public string DeathSoundId
        {
            get { return deathSoundId ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string displayName,
            bool enabled,
            List<DimensionAssetReferenceDefinition> references)
        {
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "spawn-sound", "Spawn Sound", SpawnSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "idle-sound", "Idle Sound", IdleSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "aggro-sound", "Aggro Sound", AggroSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "hit-sound", "Hit Sound", HitSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "death-sound", "Death Sound", DeathSoundId, enabled);
        }

        private void AddAudioReference(
            List<DimensionAssetReferenceDefinition> references,
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string displayName,
            string suffix,
            string label,
            string resourceKey,
            bool enabled)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                suffix,
                displayName + " " + label,
                DimensionAssetReferenceKind.Audio,
                resourceKey,
                string.Empty,
                50,
                enabled,
                Notes);
        }
    }

    [Serializable]
    public sealed class DimensionBossPhaseTemplate
    {
        [SerializeField] private string phaseId = "phase";
        [SerializeField] private string displayName = "Phase";
        [SerializeField] private float healthThreshold = 0.5f;
        [SerializeField] private string musicCueId = string.Empty;

        [Tooltip("What happens when the boss falls past this threshold.")]
        [SerializeField] private DimensionBossPhaseActionKind action = DimensionBossPhaseActionKind.None;

        [Tooltip("What the action acts with: a creature to summon, or one of the game's conditions.")]
        [SerializeField] private string actionTarget = string.Empty;

        [Tooltip("How many: creatures summoned, condition stacks, or health restored.")]
        [SerializeField] private int actionAmount = 1;

        [Tooltip("How long a condition lasts. 0 uses the condition's own duration.")]
        [SerializeField] private float actionDuration;

        [Tooltip("How far from the boss the action reaches.")]
        [SerializeField] private float radius = 6f;

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        /// <summary>What happens when the boss falls past this phase's threshold.</summary>
        public DimensionBossPhaseActionKind Action
        {
            get { return action; }
        }

        /// <summary>The creature to summon or the condition to apply, depending on the action.</summary>
        public string ActionTarget
        {
            get { return actionTarget ?? string.Empty; }
        }

        /// <summary>Creatures summoned, condition stacks, or health restored.</summary>
        public int ActionAmount
        {
            get { return actionAmount < 1 ? 1 : actionAmount; }
        }

        /// <summary>How long a condition lasts; zero uses the condition's own duration.</summary>
        public float ActionDuration
        {
            get { return actionDuration < 0f ? 0f : actionDuration; }
        }

        /// <summary>How far from the boss the action reaches.</summary>
        public float Radius
        {
            get { return radius < 0f ? 0f : radius; }
        }

        public string PhaseId
        {
            get { return phaseId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public float HealthThreshold
        {
            get { return Mathf.Clamp01(healthThreshold); }
        }

        public string MusicCueId
        {
            get { return musicCueId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }

    internal static class DimensionSpawnableAssetReferenceUtility
    {
        public static void AddSpawnableAssetReferences(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string spawnableId,
            string displayName,
            string objectId,
            bool enabled,
            DimensionSpawnableAudioTemplate audio,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                spawnableId,
                "object",
                displayName + " Object",
                DimensionAssetReferenceKind.Object,
                objectId,
                string.Empty,
                0,
                enabled,
                string.Empty);

            // No visual rows any more. What a creature looks like is a clip list and a sprite
            // asset the generator writes, not a name another system could look up, so there is
            // nothing here for an asset reference to point at.

            if (audio != null)
            {
                audio.AddAssetReferencesTo(
                    contentPackId,
                    dimensionId,
                    zoneId,
                    spawnableId,
                    displayName,
                    enabled,
                    references);
            }
        }
    }

    internal static class DimensionAuthoringAssetReferenceUtility
    {
        public static void AddReference(
            List<DimensionAssetReferenceDefinition> references,
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string suffix,
            string displayName,
            DimensionAssetReferenceKind kind,
            string resourceKey,
            string variantId,
            int priority,
            bool enabled,
            string notes)
        {
            if (references == null ||
                string.IsNullOrEmpty(contentPackId) ||
                string.IsNullOrEmpty(ownerId) ||
                string.IsNullOrEmpty(resourceKey))
            {
                return;
            }

            references.Add(new DimensionAssetReferenceDefinition(
                ownerId + "." + suffix,
                contentPackId,
                displayName,
                kind,
                resourceKey,
                dimensionId,
                zoneId,
                variantId,
                priority,
                enabled,
                notes));
        }
    }

    public static class DimensionSpawnableAuthoringUtility
    {
        public static string ResolveId(DimensionAnimalAsset asset)
        {
            return asset == null ? string.Empty : asset.AnimalId;
        }

        public static string ResolveId(DimensionCritterAsset asset)
        {
            return asset == null ? string.Empty : asset.CritterId;
        }

        public static string ResolveId(DimensionMobAsset asset)
        {
            return asset == null ? string.Empty : asset.MobId;
        }

        public static string ResolveId(DimensionBossAsset asset)
        {
            return asset == null ? string.Empty : asset.BossId;
        }

        public static string ResolveLootTableId(DimensionLootTableAsset asset)
        {
            return asset == null ? string.Empty : asset.LootTableId;
        }
    }
}
