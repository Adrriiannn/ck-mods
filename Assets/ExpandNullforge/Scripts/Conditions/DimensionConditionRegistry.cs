using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Conditions
{
    /// <summary>
    /// The stat effects a mod invented, and the numbers Core Keeper will know them by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CORE KEEPER KEEPS ITS CONDITIONS IN A NUMBERED LIST. Everything that can be on you — a buff, a
    /// poisoning, a speed boost — is an entry in one array, addressed by its number. The array is
    /// 357 long, and 356 of those are real conditions; number 0 is "nothing". A mod that wants one
    /// of its own needs a number nobody else is using and the array grown to reach it. This is that
    /// number.
    /// </para>
    /// <para>
    /// THE NUMBERS ARE HANDED OUT IN NAME ORDER, not in the order the mod happens to load, because
    /// bundle load order is not something a mod controls. The same set of conditions therefore
    /// always produces the same numbers. <em>Adding</em> one shifts the numbers of everything
    /// alphabetically after it — which matters only for a buff that was already ticking on a player
    /// when the mod was updated mid-save, and conditions are short-lived by design.
    /// </para>
    /// <para>
    /// WHAT A CUSTOM CONDITION CAN AND CANNOT BE. It can be a new named thing that applies any of
    /// the ~145 effects the game already implements — more mining speed, a heal over time, a
    /// slowdown — with your own value, duration, stacking and icon. It cannot be a new <em>kind</em>
    /// of effect, because an effect is implemented by the game's own systems reading a fixed list.
    /// That is the line between this and writing C#.
    /// </para>
    /// </remarks>
    public static class DimensionConditionRegistry
    {
        /// <summary>
        /// The first number free for a mod to use. Everything below it is Core Keeper's own.
        /// </summary>
        /// <remarks>
        /// Read from the game's own enum rather than written down, so a Core Keeper update that adds
        /// conditions moves this on its own instead of silently colliding with the new ones.
        /// </remarks>
        public static readonly int FirstFreeNumber = (int)ConditionID.MAX_VALUES;

        /// <summary>Every condition a mod has claimed, by the name it claimed it under.</summary>
        private static readonly Dictionary<string, DimensionCustomCondition> claimed =
            new Dictionary<string, DimensionCustomCondition>();

        /// <summary>The numbers handed out, worked out once and then held.</summary>
        private static Dictionary<string, int> numbers;

        /// <summary>
        /// The same set again, this time addressable by the number rather than by the name.
        /// </summary>
        /// <remarks>
        /// Held rather than rebuilt because the buff bar asks for a condition's look-up to eighteen
        /// times a frame, twice over. Invalidated wherever <see cref="numbers"/> is.
        /// </remarks>
        private static List<DimensionCustomCondition> byNumber;

        /// <summary>Whether anything at all was claimed, so callers can skip the work entirely.</summary>
        public static bool HasAny
        {
            get { return claimed.Count > 0; }
        }

        /// <summary>How many were claimed.</summary>
        public static int Count
        {
            get { return claimed.Count; }
        }

        /// <summary>
        /// Claims a number for a condition of a mod's own.
        /// </summary>
        /// <remarks>
        /// Claiming the same name twice replaces the earlier claim rather than adding a second, so a
        /// mod reloaded in the editor does not accumulate duplicates of its own conditions.
        /// </remarks>
        /// <returns>False when the name is unusable, and the claim was refused.</returns>
        public static bool Claim(DimensionCustomCondition condition)
        {
            if (condition == null || string.IsNullOrEmpty(condition.Name))
            {
                DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                    "A custom condition with no name was ignored. Without a name " +
                    "there is nothing to address it by.");
                return false;
            }

            claimed[condition.Name] = condition;

            // The numbers depend on the whole set, so a new claim invalidates them all.
            numbers = null;
            byNumber = null;
            return true;
        }

        /// <summary>Forgets everything, for a mod being unloaded or an editor reload.</summary>
        public static void Clear()
        {
            claimed.Clear();
            numbers = null;
            byNumber = null;
        }

        /// <summary>
        /// The number Core Keeper will know a condition by, or -1 when nothing claimed that name.
        /// </summary>
        public static int NumberFor(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }

            EnsureNumbers();

            int number;
            return numbers.TryGetValue(name, out number) ? number : -1;
        }

        /// <summary>The same number, as the type the game's own components take.</summary>
        public static ConditionID IdFor(string name)
        {
            int number = NumberFor(name);
            return number < 0 ? ConditionID.None : (ConditionID)number;
        }

        /// <summary>
        /// Every claimed condition in the order their numbers were handed out, so the caller
        /// building the game's table can walk it start to finish.
        /// </summary>
        public static List<DimensionCustomCondition> InNumberOrder()
        {
            EnsureNumbers();

            List<string> names = new List<string>(claimed.Keys);
            names.Sort(System.StringComparer.Ordinal);

            List<DimensionCustomCondition> ordered =
                new List<DimensionCustomCondition>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                ordered.Add(claimed[names[i]]);
            }

            return ordered;
        }

        /// <summary>The size the game's condition array has to be to hold all of this.</summary>
        public static int RequiredTableSize
        {
            get { return FirstFreeNumber + claimed.Count; }
        }

        /// <summary>
        /// The condition holding one of the numbers this registry handed out.
        /// </summary>
        /// <remarks>
        /// False for anything below <see cref="FirstFreeNumber"/> (that is Core Keeper's own) and
        /// for a number above everything claimed — which happens when a save still carries a buff
        /// from a build of the mod that had more conditions in it than this one does.
        /// </remarks>
        public static bool TryGetByNumber(int number, out DimensionCustomCondition condition)
        {
            condition = null;
            int index = number - FirstFreeNumber;
            if (index < 0)
            {
                return false;
            }

            EnsureNumbers();
            if (index >= byNumber.Count)
            {
                return false;
            }

            condition = byNumber[index];
            return condition != null;
        }

        /// <summary>
        /// The number a name means, whether it is one of Core Keeper's own or one of this mod's.
        /// </summary>
        /// <remarks>
        /// The game's own names win, which is the same order
        /// <c>DimensionObjectSpine.TryResolveCondition</c> answers in — a mod cannot shadow a
        /// vanilla effect by naming its own the same thing.
        /// </remarks>
        public static ConditionID ResolveByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ConditionID.None;
            }

            ConditionID vanilla;
            if (System.Enum.TryParse(name, false, out vanilla) && vanilla != ConditionID.None)
            {
                return vanilla;
            }

            return IdFor(name);
        }

        /// <summary>Works out the numbers once, in name order, and holds them.</summary>
        private static void EnsureNumbers()
        {
            if (numbers != null && byNumber != null)
            {
                return;
            }

            List<string> names = new List<string>(claimed.Keys);
            names.Sort(System.StringComparer.Ordinal);

            numbers = new Dictionary<string, int>(names.Count);
            byNumber = new List<DimensionCustomCondition>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                numbers[names[i]] = FirstFreeNumber + i;
                byNumber.Add(claimed[names[i]]);
            }
        }
    }

    /// <summary>One stat effect a mod invented, ready to be written into the game's own table.</summary>
    public sealed class DimensionCustomCondition
    {
        public DimensionCustomCondition(string name)
        {
            Name = name ?? string.Empty;
        }

        /// <summary>What the mod calls it. This is what its number is worked out from.</summary>
        public string Name { get; private set; }

        /// <summary>Which of the game's own effects it applies.</summary>
        public ConditionEffect Effect { get; set; }

        /// <summary>A second copy adds to the first rather than replacing it.</summary>
        public bool AddsToItself { get; set; }

        /// <summary>It never runs out on its own.</summary>
        public bool LastsForever { get; set; }

        /// <summary>It is a bad thing, so the game shows it as one.</summary>
        public bool IsBad { get; set; }

        /// <summary>Only one of it at a time, whoever put it there.</summary>
        public bool OnlyOneAtATime { get; set; }

        /// <summary>Something this is on passes it to whatever it shoots.</summary>
        public bool PassedOnByShots { get; set; }

        /// <summary>A stronger one replaces a weaker one that is still running.</summary>
        public bool AStrongerOneWins { get; set; }

        /// <summary>
        /// The little picture in the row of buffs. Without one the buff never appears there.
        /// </summary>
        /// <remarks>
        /// Not a nicety: <c>ConditionsContainerUI.UpdateConditions</c> counts and then draws only
        /// the entries whose <c>icon</c> is not null, so a condition with no picture is live on the
        /// player and invisible.
        /// </remarks>
        public Sprite Icon { get; set; }

        /// <summary>Shows 34 as 3.4.</summary>
        public bool ShowsADecimal { get; set; }

        /// <summary>Drops the + in front of the number.</summary>
        public bool HidesThePlusSign { get; set; }

        /// <summary>Keeps it off the list of stats on an item.</summary>
        public bool HidesTheStatLine { get; set; }

        /// <summary>
        /// The name of another effect whose wording this one borrows, or empty for its own.
        /// </summary>
        /// <remarks>
        /// Held as a name rather than a number because the effect being borrowed from may be one
        /// of this mod's own, and that one's number is not known until the whole set is claimed.
        /// Resolved when the game asks, not when the claim is made.
        /// </remarks>
        public string ReadsLikeThisOne { get; set; }

        /// <summary>
        /// One authored condition, turned into the record the game's tables are filled from.
        /// </summary>
        /// <remarks>
        /// THE ONLY PLACE THAT KNOWS THE FIELD LIST. Both the running game (as the mod's assets
        /// load) and the editor (before a generate, so names resolve to the same numbers) need this
        /// record, and they used to build it separately with two hand-written field lists. A field
        /// added to one and not the other is silent — the generated content gets one set of rules
        /// and the running game another — so there is one list and it is here.
        /// </remarks>
        public static DimensionCustomCondition From(DimensionConditionAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            return new DimensionCustomCondition(asset.ConditionName)
            {
                Effect = asset.Effect,
                IsBad = asset.IsBad,
                LastsForever = asset.LastsForever,
                AddsToItself = asset.AddsToItself,
                OnlyOneAtATime = asset.OnlyOneAtATime,
                PassedOnByShots = asset.PassedOnByShots,
                AStrongerOneWins = asset.AStrongerOneWins,
                Icon = asset.Icon,
                ShowsADecimal = asset.ShowsADecimal,
                HidesThePlusSign = asset.HidesThePlusSign,
                HidesTheStatLine = asset.HidesTheStatLine,
                ReadsLikeThisOne = asset.ReadsLikeThisOne
            };
        }
    }
}
