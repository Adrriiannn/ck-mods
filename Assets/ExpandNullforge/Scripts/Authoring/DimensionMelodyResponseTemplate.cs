using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An object that reacts to a melody played near it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper's ocarina system. A player plays a tune; anything within earshot that is
    /// listening for that tune hums back, and then changes — opens, weakens, turns into something
    /// else, or gives up its contents. It is the game's whole puzzle vocabulary and the framework
    /// had never touched it.
    /// </para>
    /// <para>
    /// MEASURED ACROSS THE 25 VANILLA PREFABS THAT CARRY IT, and they are unanimous:
    /// <c>hearRange</c> 5, <c>humCooldown</c> 0, <c>newVariation</c> 1, <c>weakenWhenAffected</c>
    /// true, <c>changeObjectID</c> false, <c>removeMelodyListener</c> false,
    /// <c>removeOldColliders</c> false. Every one of them is the same behaviour: play the right tune
    /// within five tiles and the thing softens and switches to its second look. Those are the
    /// defaults here, so an author who fills in only the melody gets exactly the vanilla object.
    /// </para>
    /// <para>
    /// THE TWO WAYS IT CHANGES ARE DIFFERENT, and picking the wrong one is the trap. Switching
    /// VARIATION keeps the same object — same id, same components, same contents — and just shows a
    /// different look. Switching OBJECT replaces it with a different object entirely, which is what
    /// <c>removeOldColliders</c> exists for: the old object's colliders would otherwise be left
    /// behind in the world with nothing attached to them.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMelodyResponseTemplate
    {
        [Header("What it listens for")]
        [Tooltip("Which melodies it reacts to, by the game's own names. Empty means it never reacts.")]
        [SerializeField] private string[] melodies = new string[0];

        [Tooltip("How far away it can hear a melody, in tiles. Every vanilla listener uses 5.")]
        [Min(0f)]
        [SerializeField] private float hearingRange = 5f;

        [Tooltip("How long before it will hum back again. 0 lets it answer every time.")]
        [Min(0f)]
        [SerializeField] private float humCooldown;

        [Tooltip("It starts already listening rather than waiting to be switched on.")]
        [SerializeField] private bool startsListening = true;

        [Header("What happens to it")]
        [Tooltip("It becomes weaker once the right melody is played. Every vanilla listener does.")]
        [SerializeField] private bool weakensWhenItHears = true;

        [Tooltip("Which look it switches to. Vanilla uses 1 — the second look.")]
        [Min(0)]
        [SerializeField] private int becomesVariation = 1;

        [Tooltip("It stops listening afterwards, so the melody only works on it once.")]
        [SerializeField] private bool onlyRespondsOnce;

        [Header("Turning into something else")]
        [Tooltip("It becomes a different object entirely rather than just changing its look.")]
        [SerializeField] private bool becomesADifferentObject;

        [Tooltip("What it becomes. One of the game's objects, or one of yours.")]
        [SerializeField] private string becomesObjectId = string.Empty;

        [Tooltip("Its old colliders are cleared away as it changes. Needed when it changes object.")]
        [SerializeField] private bool clearsItsOldColliders = true;

        [Header("What it gives")]
        [Tooltip("A loot table it rolls when it reacts. Blank for none.")]
        [SerializeField] private string lootTableId = string.Empty;

        [Tooltip("Exactly what it holds after it changes.")]
        [SerializeField] private DimensionMelodyReward[] contents = new DimensionMelodyReward[0];

        public string[] Melodies { get { return melodies ?? new string[0]; } }

        public float HearingRange { get { return hearingRange < 0f ? 0f : hearingRange; } }

        public float HumCooldown { get { return humCooldown < 0f ? 0f : humCooldown; } }

        public bool StartsListening { get { return startsListening; } }

        public bool WeakensWhenItHears { get { return weakensWhenItHears; } }

        public int BecomesVariation { get { return becomesVariation < 0 ? 0 : becomesVariation; } }

        public bool OnlyRespondsOnce { get { return onlyRespondsOnce; } }

        public bool BecomesADifferentObject { get { return becomesADifferentObject; } }

        public string BecomesObjectId { get { return becomesObjectId ?? string.Empty; } }

        public bool ClearsItsOldColliders { get { return clearsItsOldColliders; } }

        public string LootTableId { get { return lootTableId ?? string.Empty; } }

        public DimensionMelodyReward[] Contents
        {
            get { return contents ?? new DimensionMelodyReward[0]; }
        }

        /// <summary>Whether the author filled any of this in.</summary>
        /// <remarks>
        /// All-or-nothing, like pursuit: the component is only attached when a melody was named, so
        /// an untouched section never turns an ordinary object into a silent listener.
        /// </remarks>
        public bool HasAnySetting { get { return Melodies.Length > 0; } }

        /// <summary>Whether it is set to become an object without naming which.</summary>
        public bool BecomesNothing
        {
            get { return becomesADifferentObject && string.IsNullOrEmpty(BecomesObjectId); }
        }

        /// <summary>Whether it names what to become without being told to become anything.</summary>
        public bool ObjectChangeWillBeIgnored
        {
            get { return !becomesADifferentObject && !string.IsNullOrEmpty(BecomesObjectId); }
        }

        /// <summary>
        /// Whether it changes into a different object while leaving its old colliders behind.
        /// </summary>
        /// <remarks>
        /// Worth saying because the symptom is baffling: the object visibly changes and then an
        /// invisible wall stays exactly where the old one stood.
        /// </remarks>
        public bool LeavesItsOldCollidersBehind
        {
            get { return becomesADifferentObject && !clearsItsOldColliders; }
        }
    }

    /// <summary>One thing an object holds after a melody changes it.</summary>
    [Serializable]
    public struct DimensionMelodyReward
    {
        [Tooltip("What it holds.")]
        [SerializeField] private string objectId;

        [Tooltip("How many. Ignored for anything that does not stack — the game uses that item's own amount.")]
        [Min(0)]
        [SerializeField] private int amount;

        [Tooltip("Which look of it.")]
        [Min(0)]
        [SerializeField] private int variation;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Amount { get { return amount < 0 ? 0 : amount; } }

        public int Variation { get { return variation < 0 ? 0 : variation; } }
    }
}
