using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>One effect an item grants, and how strong it is.</summary>
    /// <remarks>
    /// Maps onto Core Keeper's <c>EquipmentCondition</c>. The name is a <c>ConditionID</c> — the
    /// game's own list of every buff and debuff it has, from <c>HealthAddition</c> and
    /// <c>MiningIncrease</c> to <c>CritChance</c> and <c>LifeOnHit</c>. Naming one that does not exist
    /// is reported rather than silently dropped, because an item that grants nothing looks identical
    /// to one that grants something until a player equips it.
    /// </remarks>
    [Serializable]
    public sealed class DimensionItemEffect
    {
        [Tooltip("Which effect. A ConditionID name — MiningIncrease, ArmorIncrease, CritChance, LifeOnHit, and so on.")]
        [SerializeField] private string effectId = string.Empty;

        [Tooltip("How much of it.")]
        [SerializeField] private int value = 1;

        [Tooltip("Scales the value. Leave at 1 unless you want the effect to grow with something.")]
        [Min(0f)]
        [SerializeField] private float valueMultiplier = 1f;

        [Tooltip("How long it lasts, in seconds. Only read when the item is eaten.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        public string EffectId
        {
            get { return effectId ?? string.Empty; }
        }

        public int Value
        {
            get { return value; }
        }

        public float Seconds { get { return seconds < 0f ? 0f : seconds; } }

        public float ValueMultiplier
        {
            get { return valueMultiplier < 0f ? 0f : valueMultiplier; }
        }

        /// <summary>Whether this entry names an effect at all.</summary>
        public bool NamesAnEffect
        {
            get { return !string.IsNullOrEmpty(EffectId); }
        }
    }

    /// <summary>
    /// What an item does for whoever has it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Composable rather than an asset of its own, for the same reason wiring is: granting effects is
    /// something an item DOES alongside being whatever else it is. A helmet is still a helmet, a
    /// torch is still a torch.
    /// </para>
    /// <para>
    /// <b>THE LEVEL TRAP, AGAIN.</b> <c>GivesConditionsWhenEquippedAuthoring</c> has
    /// <c>dontCalculateValuesFromLevel</c>, and it behaves exactly like <c>HealthAuthoring</c>'s
    /// equivalent: leave it false with an <c>AreaLevelAuthoring</c> present and the game recomputes
    /// every value from the level curve, so an authored +5 mining silently becomes whatever the curve
    /// says. <see cref="ValuesAreExactlyAsTyped"/> is the author's answer to that, and it defaults to
    /// keeping their numbers.
    /// </para>
    /// <para>
    /// Equipped and eaten are separate lists because Core Keeper keeps them on separate components —
    /// <c>GivesConditionsWhenEquipped</c> and <c>GivesConditionsWhenConsumed</c>. A thing can be both;
    /// most are one or the other.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionItemEffectsTemplate
    {
        [Header("While worn or held")]
        [Tooltip("What it grants while equipped.")]
        [SerializeField] private DimensionItemEffect[] whileEquipped = new DimensionItemEffect[0];

        [Tooltip("It also grants those while merely held in hand, not just while worn.")]
        [SerializeField] private bool alsoWhileJustHeld;

        [Tooltip("It counts as armour, which is what makes the game treat its effects as protection.")]
        [SerializeField] private bool countsAsArmor;

        [Tooltip("Keep the numbers exactly as typed. Untick to let Core Keeper scale them by area level, as vanilla gear does.")]
        [SerializeField] private bool valuesAreExactlyAsTyped = true;

        [Header("When eaten")]
        [Tooltip("What it grants when consumed.")]
        [SerializeField] private DimensionItemEffect[] whenEaten = new DimensionItemEffect[0];

        [Header("Cooldown")]
        // Kept only to migrate the assets that carry it. There was a second seconds-before-you-can-
        // use-it-again field here as well as the one on the item itself, and this was the one that
        // reached the game: the effects pass ran last and threw the item's number away. Two fields
        // meaning one thing, and the visible one losing. The item's own "Handheld cooldown in
        // seconds" is now the only place the number is typed, and DimensionItemAsset.CooldownSeconds
        // reads whatever is stored here when that field is blank, so nothing written before this
        // loses its number.
        [HideInInspector]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds;

        [Tooltip("Puts this item on a shared timer, so using one makes the others wait too. " +
            "Three to choose from: HealingPotion and ManaPotion are the game's two potion timers, " +
            "and SlotType shares with everything in the same slot. Blank means SlotType too — " +
            "the game has no timer an item keeps to itself, so there is nothing to leave it as. " +
            "The game has a fourth, ObjectID, that a mod cannot use: it is only ever given to the " +
            "Cupid Bow and the game throws on anything else. Anything it does not know is " +
            "reported when you generate.")]
        [SerializeField] private string sharedCooldownId = string.Empty;

        /// <summary>Effects granted while equipped, with the blank entries dropped.</summary>
        public DimensionItemEffect[] WhileEquipped
        {
            get { return Compact(whileEquipped); }
        }

        /// <summary>Effects granted when eaten, with the blank entries dropped.</summary>
        public DimensionItemEffect[] WhenEaten
        {
            get { return Compact(whenEaten); }
        }

        public bool AlsoWhileJustHeld
        {
            get { return alsoWhileJustHeld; }
        }

        public bool CountsAsArmor
        {
            get { return countsAsArmor; }
        }

        /// <summary>Whether the author's numbers survive rather than being derived from the level.</summary>
        public bool ValuesAreExactlyAsTyped
        {
            get { return valuesAreExactlyAsTyped; }
        }

        /// <summary>
        /// The number an asset written before the cooldown became one field still holds here.
        /// </summary>
        /// <remarks>
        /// Not a control any more, and not drawn. <c>DimensionItemAsset.CooldownSeconds</c> reads it
        /// when the item's own cooldown field is blank, so an asset that kept its number here keeps
        /// its behaviour; anything new is typed on the item.
        /// </remarks>
        public float LegacyCooldownSeconds
        {
            get { return cooldownSeconds < 0f ? 0f : cooldownSeconds; }
        }

        public string SharedCooldownId
        {
            get { return sharedCooldownId ?? string.Empty; }
        }

        /// <summary>Whether it shares its cooldown with a named group, the way potions do.</summary>
        public bool SharesACooldown
        {
            get { return !string.IsNullOrEmpty(SharedCooldownId); }
        }

        /// <summary>Whether it grants anything at all.</summary>
        public bool GrantsAnything
        {
            get { return WhileEquipped.Length > 0 || WhenEaten.Length > 0; }
        }

        /// <summary>Whether it was marked as armour but grants nothing while worn.</summary>
        /// <remarks>
        /// Armour that protects against nothing still equips, still occupies the slot, and still reads
        /// as a piece of gear — it simply does nothing, which no player would think to check.
        /// </remarks>
        public bool IsArmorThatProtectsNothing
        {
            get { return countsAsArmor && WhileEquipped.Length == 0; }
        }

        /// <summary>Whether it says effects apply while held but has no effects to apply.</summary>
        public bool HeldEffectsWithNothingToGrant
        {
            get { return alsoWhileJustHeld && WhileEquipped.Length == 0; }
        }

        private static DimensionItemEffect[] Compact(DimensionItemEffect[] source)
        {
            DimensionItemEffect[] all = source ?? new DimensionItemEffect[0];
            List<DimensionItemEffect> kept = new List<DimensionItemEffect>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].NamesAnEffect)
                {
                    kept.Add(all[i]);
                }
            }

            return kept.ToArray();
        }
    }
}
