using System.Collections.Generic;
using HarmonyLib;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.WorldRules
{
    /// <summary>
    /// Changes what one of the game's eleven backgrounds starts a new character with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TABLE HAS NO NAME TO LOAD IT BY. <c>RolePerksTable</c> is the one of these six tables
    /// that does not sit in a <c>Resources</c> folder: it lives at
    /// <c>MonoBehaviour/RolePerksTable.asset</c> and reaches the game only as an inspector
    /// reference on five prefabs — the player, the three character screens, and the main manager.
    /// So there is nothing to <c>Resources.Load</c> and no converter to stand in front of.
    /// </para>
    /// <para>
    /// SO THE ANSWER IS INTERCEPTED, NOT THE TABLE EDITED. Every reader of that asset goes through
    /// one method, <c>RolePerksTable.GetPerks</c> — the player granting the kit
    /// (<c>ck-db\Pug.Other\PlayerController.cs:3205</c>) and the creation screen previewing it
    /// (<c>ck-db\Pug.Other\CharacterCustomizationOption_Selection.cs:99</c>). Answering there is
    /// the law the talent-icon patch already follows: it has no timing question in it, the game's
    /// own asset is never written to, and a background this mod did not claim keeps exactly what
    /// the game gives it.
    /// </para>
    /// <para>
    /// A TWELFTH BACKGROUND IS NOT ON OFFER. The creation carousel sizes itself from
    /// <c>Enum.GetNames(typeof(CharacterRole)).Length</c>, steps modulo that number, and indexes an
    /// eleven-entry picture list by position. A twelfth row could not be reached, and if it were it
    /// would throw. See <see cref="Authoring.DimensionBackgroundTemplate"/>.
    /// </para>
    /// <para>
    /// THE KIT REPLACES, IT DOES NOT ADD. The game stores a background's kit as one list and grants
    /// it whole, so there is no way to add one item and leave the rest alone without guessing at
    /// the rest. A background nobody names is not touched at all.
    /// </para>
    /// </remarks>
    public static class DimensionBackgroundRegistry
    {
        /// <summary>One thing a background starts a character with, in names rather than numbers.</summary>
        public struct KitItem
        {
            /// <summary>What it is — the game's own name, or one of this mod's items.</summary>
            public string ItemName;

            /// <summary>How many of it.</summary>
            public int Amount;

            /// <summary>Which variation of it.</summary>
            public int Variation;
        }

        private sealed class Row
        {
            public int Background;
            public string SkillName;
            public List<KitItem> StartsWith;
        }

        /// <summary>
        /// How many starter items the character-creation screen can write down.
        /// </summary>
        /// <remarks>
        /// Measured on <c>GameObject/CharacterCustomizationUI.prefab</c>: two <c>roleItemDescs</c>
        /// slots, indexed by the item's position with no bounds check
        /// (<c>ck-db\Pug.Other\CharacterCustomizationOption_Selection.cs:142</c>). A third item is
        /// an exception thrown as a player skims onto that background, so the third and anything
        /// after it is refused rather than shipped.
        /// </remarks>
        public const int MostItemsTheScreenCanShow = 2;

        // THERE IS DELIBERATELY NO `NomadBackground = 6` CONSTANT HERE, because nothing in the
        // tree would read it: the check that matters is DimensionBackgroundKit.NamesASkillNomadWillNeverGet,
        // which the template carries and the generator reports from. A public number nothing asks
        // for reads as a check being made somewhere, and there is none.

        private static readonly List<Row> Rows = new List<Row>();

        private static readonly Dictionary<int, RolePerksTable.Perks> answered =
            new Dictionary<int, RolePerksTable.Perks>();

        /// <summary>
        /// Names already complained about, so an item that has not loaded yet says so once.
        /// </summary>
        /// <remarks>
        /// The creation screen re-asks every time the player skims, so without this a misspelled
        /// item would write a line per keypress.
        /// </remarks>
        private static readonly HashSet<string> stillMissing =
            new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Queues one background. Call from the generated bootstrap; names resolve at load.</summary>
        public static void Register(
            int background,
            string skillName,
            string[] itemNames,
            int[] amounts,
            int[] variations)
        {
            if (background < 0)
            {
                return;
            }

            Row row = new Row
            {
                Background = background,
                SkillName = skillName ?? string.Empty,
                StartsWith = new List<KitItem>()
            };

            int count = itemNames == null ? 0 : itemNames.Length;
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(itemNames[i]))
                {
                    continue;
                }

                row.StartsWith.Add(new KitItem
                {
                    ItemName = itemNames[i],
                    Amount = amounts != null && i < amounts.Length && amounts[i] > 0 ? amounts[i] : 1,
                    Variation = variations != null && i < variations.Length && variations[i] > 0
                        ? variations[i]
                        : 0
                });
            }

            if (string.IsNullOrEmpty(row.SkillName) && row.StartsWith.Count == 0)
            {
                return;
            }

            // Naming the same background twice replaces the earlier row, so a mod reloaded in the
            // editor does not stack two kits onto one background.
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Background == background)
                {
                    Rows[i] = row;
                    answered.Clear();
                    stillMissing.Clear();
                    return;
                }
            }

            Rows.Add(row);
            answered.Clear();
            stillMissing.Clear();
        }

        /// <summary>Clears queued backgrounds (mod reload).</summary>
        public static void Clear()
        {
            Rows.Clear();
            answered.Clear();
            stillMissing.Clear();
        }

        /// <summary>True once at least one background has been changed.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>Whether this mod has anything to say about one background.</summary>
        public static bool Claims(int background)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Background == background)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How many of a kit's items the creation screen can actually write down.
        /// </summary>
        /// <remarks>
        /// Pure so it can be exercised without a running game, and used by both the runtime and the
        /// editor check so the two cannot disagree about where the ceiling is.
        /// </remarks>
        public static int ItemsThatWillFit(int listed)
        {
            if (listed < 0)
            {
                return 0;
            }

            return listed > MostItemsTheScreenCanShow ? MostItemsTheScreenCanShow : listed;
        }

        /// <summary>
        /// What one background should answer, or false when this mod says nothing about it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE ANSWER IS ONLY KEPT ONCE IT IS WHOLE. The character-creation screen is open long
        /// before a world exists, and a mod's own object has no number until its content has
        /// loaded, so the first time this is asked one of the mod's own items can still come back
        /// empty. Caching that answer would freeze a half-built kit for the rest of the session, so
        /// an answer with anything missing from it is rebuilt on the next ask instead of stored.
        /// </para>
        /// <para>
        /// A HALF NOBODY FILLED IN KEEPS THE GAME'S OWN, which is why the game's answer is passed
        /// in rather than ignored. The answer replaces Core Keeper's whole <c>Perks</c> struct, so
        /// building it from nothing made a row that changed only the bag also move that background
        /// to Mining — <c>default(SkillID)</c> is Mining, and neither branch below fires on an
        /// empty name. Chef given a new bag lost Cooking, and Chef given a new skill lost the
        /// cooking pot, both without a word. Starting from the game's own answer means a row edits
        /// the halves it names and leaves the rest exactly as the game had it, which is what the
        /// row-level promise on the template already says about a background nobody names.
        /// </para>
        /// </remarks>
        internal static bool TryAnswer(
            int background,
            RolePerksTable.Perks theGamesOwn,
            System.Func<string, ObjectID> resolveItem,
            System.Action<string> report,
            out RolePerksTable.Perks perks)
        {
            perks = default(RolePerksTable.Perks);

            RolePerksTable.Perks cached;
            if (answered.TryGetValue(background, out cached))
            {
                perks = cached;
                return true;
            }

            Row row = null;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Background == background)
                {
                    row = Rows[i];
                    break;
                }
            }

            if (row == null)
            {
                return false;
            }

            // The game's own list is copied rather than handed back: it belongs to RolePerksTable,
            // which is one asset shared by five prefabs and never reloaded, so adding to it would
            // outlive the world that wanted it.
            List<ObjectData> keptItems = new List<ObjectData>();
            for (int i = 0; theGamesOwn.starterItems != null && i < theGamesOwn.starterItems.Count; i++)
            {
                keptItems.Add(theGamesOwn.starterItems[i]);
            }

            RolePerksTable.Perks built = new RolePerksTable.Perks
            {
                role = (CharacterRole)background,
                starterSkill = theGamesOwn.starterSkill,
                starterItems = keptItems
            };

            SkillID skill;
            if (!string.IsNullOrEmpty(row.SkillName) &&
                System.Enum.TryParse(row.SkillName, false, out skill))
            {
                built.starterSkill = skill;
            }
            else if (!string.IsNullOrEmpty(row.SkillName) && report != null)
            {
                report(
                    "A background was set to start you in '" + row.SkillName +
                    "', which is not one of the game's twelve skills, so it starts you in " +
                    "Mining instead. The names are Mining, Running, Melee, Vitality, Crafting, " +
                    "Range, Gardening, Fishing, Cooking, Magic, Summoning and Explosives.");
            }

            // A row that lists nothing to start with keeps the game's bag; a row that lists
            // anything replaces the whole bag, because "two items instead of these two" is what
            // listing items means and there is no way to say "and also".
            int fits = ItemsThatWillFit(row.StartsWith.Count);
            if (fits > 0)
            {
                built.starterItems.Clear();
            }

            if (row.StartsWith.Count > fits && report != null)
            {
                report(
                    "A background was given " + row.StartsWith.Count +
                    " things to start with. The character-creation screen has two lines to write " +
                    "them on and no room for a third, so only the first two are given.");
            }

            bool everythingResolved = true;
            for (int i = 0; i < fits; i++)
            {
                KitItem item = row.StartsWith[i];
                ObjectID resolved = resolveItem == null
                    ? ObjectID.None
                    : resolveItem(item.ItemName);
                if (resolved == ObjectID.None)
                {
                    everythingResolved = false;
                    if (report != null && stillMissing.Add(item.ItemName))
                    {
                        report(
                            "A background was set to start you with '" + item.ItemName +
                            "', and nothing answers to that name. If it is one of your own items " +
                            "this can right itself once your content has loaded; if it is not, " +
                            "check the spelling against the item's own name.");
                    }

                    continue;
                }

                built.starterItems.Add(new ObjectData
                {
                    objectID = resolved,
                    amount = item.Amount,
                    variation = item.Variation
                });
            }

            // See the remarks: a half-built kit is answered but never kept.
            if (everythingResolved)
            {
                answered[background] = built;
            }

            perks = built;
            return true;
        }

        /// <summary>Answers an object name, this mod's own included, once the mod is loaded.</summary>
        internal static ObjectID ResolveItemName(string name)
        {
            return DimensionObjectNames.Resolve(name);
        }
    }

    /// <summary>
    /// Answers what a background starts a character with, where the game asks the question.
    /// </summary>
    /// <remarks>
    /// A postfix on the one accessor both readers go through, rather than a write into the game's
    /// asset. The asset is shared by five prefabs and never reloaded, so a write into it would
    /// outlive the world that wanted it; an answer supplied on the way past outlives nothing.
    /// </remarks>
    [HarmonyPatch(typeof(RolePerksTable), "GetPerks")]
    internal static class DimensionBackgroundPerksPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPostfix]
        private static void After(CharacterRole role, ref RolePerksTable.Perks __result)
        {
            Fired++;

            if (!DimensionBackgroundRegistry.HasAny)
            {
                return;
            }

            RolePerksTable.Perks ours;
            if (DimensionBackgroundRegistry.TryAnswer(
                    (int)role,
                    // The game's own answer goes in so a row that fills one half keeps the other.
                    __result,
                    DimensionBackgroundRegistry.ResolveItemName,
                    DimensionFrameworkLog.Warning,
                    out ours))
            {
                __result = ours;
            }
        }
    }
}
