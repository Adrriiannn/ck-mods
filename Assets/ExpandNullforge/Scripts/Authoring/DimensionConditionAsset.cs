using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A stat effect of your own — a buff, a curse, a poisoning nobody has seen before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CONDITION IS A NAMED THING THAT SITS ON SOMEONE AND DOES SOMETHING. Core Keeper ships 356
    /// of them — the fed bonus, being poisoned, the speed from a slime floor — in a list 357 long,
    /// the first slot of which means "nothing". This makes one more, with its own name, its own
    /// picture, its own rules about stacking and running out.
    /// </para>
    /// <para>
    /// WHAT IT DOES COMES FROM THE GAME'S LIST. Underneath, every condition applies one of about a
    /// hundred and forty-five <em>effects</em> the game already implements — more mining speed, a
    /// heal over time, more melee damage, a slowdown. You pick which one, and you decide by how
    /// much, for how long, and on what terms. What you cannot do here is invent a new kind of
    /// effect: that is the game's own code, and it is the line between this and writing C#.
    /// </para>
    /// <para>
    /// THE NUMBER IT GETS IS WORKED OUT FROM ITS NAME, in alphabetical order among this mod's own
    /// conditions. Renaming one, or adding one alphabetically before it, changes those numbers —
    /// which only matters for a buff that was already ticking on somebody when the mod was updated
    /// mid-game.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Condition")]
    public sealed class DimensionConditionAsset : ScriptableObject
    {
        [Tooltip("A short name for it. Everything else about it is looked up by this.")]
        [SerializeField] private string conditionName = "mod:condition";

        [Tooltip("What it is called where a player sees it.")]
        [SerializeField] private string displayName = "New Condition";

        [Tooltip("The line a player reads about it, with {0} where its number goes — \"{0}% mining damage\". Leave empty for the number followed by the name. There is no second line: the game gives a condition one piece of text and this is it.")]
        [SerializeField] private string tooltipLine = string.Empty;

        [Tooltip("Whether it exists at all.")]
        [SerializeField] private bool enabled = true;

        [Header("What it actually does")]
        [Tooltip("Which of the game's own effects it applies. This is the part the game implements.")]
        [SerializeField] private ConditionEffect effect = ConditionEffect.MovementSpeed;

        [Header("How it behaves")]
        [Tooltip("It is a bad thing, so the game shows it as one.")]
        [SerializeField] private bool isBad;

        [Tooltip("It never runs out on its own — something has to take it away.")]
        [SerializeField] private bool lastsForever;

        [Tooltip("A second helping adds to the first rather than replacing it.")]
        [SerializeField] private bool addsToItself;

        [Tooltip("Only one of it at a time, whoever put it there.")]
        [SerializeField] private bool onlyOneAtATime;

        [Tooltip("Whatever carries it passes it on to anything it shoots.")]
        [SerializeField] private bool passedOnByShots;

        [Tooltip("A stronger one replaces a weaker one that is still running.")]
        [SerializeField] private bool aStrongerOneWins;

        [Header("How it reads")]
        [Tooltip("The little picture for it in the row of buffs. Without one it never appears there at all — the game draws only the buffs that have a picture.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Show it as 3.4 rather than 34. Use it for anything counted in tenths.")]
        [SerializeField] private bool showsADecimal;

        [Tooltip("Drop the + in front of the number.")]
        [SerializeField] private bool hidesThePlusSign;

        [Tooltip("Keep it off the list of stats on an item that grants it.")]
        [SerializeField] private bool hidesTheStatLine;

        [Tooltip("Borrow another effect's wording instead of writing your own. Type one of the game's own effect names, or one of this dimension's. Leave empty to use the line above.")]
        [SerializeField] private string readsLikeThisOne = string.Empty;

        public string ConditionName
        {
            get { return string.IsNullOrEmpty(conditionName) ? string.Empty : conditionName; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? ConditionName : displayName; }
        }

        /// <summary>
        /// The whole of what a player ever reads about this condition.
        /// </summary>
        /// <remarks>
        /// Core Keeper hands the formatted value in as <c>{0}</c> and offers no description term
        /// beside it, so a line written as a sentence — "{0}% mining damage" — is the game's own
        /// shape and the only one that shows the number. The fallback puts the number in front of
        /// the name so a condition nobody wrote a line for still reads as something.
        /// </remarks>
        public string TooltipLine
        {
            get
            {
                if (!string.IsNullOrEmpty(tooltipLine))
                {
                    return tooltipLine;
                }

                return "{0} " + DisplayName;
            }
        }

        public bool Enabled { get { return enabled; } }

        public ConditionEffect Effect { get { return effect; } }

        public bool IsBad { get { return isBad; } }

        public bool LastsForever { get { return lastsForever; } }

        public bool AddsToItself { get { return addsToItself; } }

        public bool OnlyOneAtATime { get { return onlyOneAtATime; } }

        public bool PassedOnByShots { get { return passedOnByShots; } }

        public bool AStrongerOneWins { get { return aStrongerOneWins; } }

        public Sprite Icon { get { return icon; } }

        public bool ShowsADecimal { get { return showsADecimal; } }

        public bool HidesThePlusSign { get { return hidesThePlusSign; } }

        public bool HidesTheStatLine { get { return hidesTheStatLine; } }

        public string ReadsLikeThisOne
        {
            get { return string.IsNullOrEmpty(readsLikeThisOne) ? string.Empty : readsLikeThisOne; }
        }

        /// <summary>
        /// It has no picture, so it will never show in the row of buffs above the hotbar.
        /// </summary>
        /// <remarks>
        /// Right for a permanent effect that is only ever read off an item's stat list, and a
        /// mistake for a buff meant to be seen ticking down. Worth saying either way, because the
        /// failure is a buff that is working and invisible.
        /// </remarks>
        public bool NeverShowsInTheBuffRow
        {
            get { return enabled && icon == null; }
        }

        /// <summary>It applies nothing, so it would sit on someone doing exactly that.</summary>
        public bool DoesNothing
        {
            get { return enabled && effect == ConditionEffect.None; }
        }

        /// <summary>
        /// It never runs out and nothing stacks it, which is only right for something meant to be
        /// taken away by hand.
        /// </summary>
        public bool LastsForeverAndCannotBeStacked
        {
            get { return enabled && lastsForever && onlyOneAtATime; }
        }
    }
}
