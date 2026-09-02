using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// One row of the table: a component, what it needs beside it, and who fills it in.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
        /// <summary>
        /// What one authoring answer needs beside it, and what the author is told when we cannot
        /// supply it.
        /// </summary>
        private sealed class Companion
        {
            public Companion(
                Type present,
                string alsoNeeds,
                string vanillaPrefab,
                string readBy,
                Func<GameObject, bool> fillIn,
                string sayInstead)
                : this(present, alsoNeeds, vanillaPrefab, readBy, fillIn, null, sayInstead)
            {
            }

            /// <summary>
            /// The same, for a row whose fill only applies to some objects.
            /// </summary>
            public Companion(
                Type present,
                string alsoNeeds,
                string vanillaPrefab,
                string readBy,
                Func<GameObject, bool> fillIn,
                Func<GameObject, bool> onlyWhen,
                string sayInstead)
            {
                Present = present;
                AlsoNeeds = alsoNeeds;
                VanillaPrefab = vanillaPrefab;
                ReadBy = readBy;
                FillIn = fillIn;
                OnlyWhen = onlyWhen;
                SayInstead = sayInstead;
            }

            /// <summary>The authoring component the framework writes.</summary>
            public Type Present { get; private set; }

            /// <summary>The sibling authoring component the same system's query also demands.</summary>
            public string AlsoNeeds { get; private set; }

            /// <summary>Core Keeper's own object doing that job, which carries both.</summary>
            public string VanillaPrefab { get; private set; }

            /// <summary>The system whose query is the reason.</summary>
            public string ReadBy { get; private set; }

            /// <summary>
            /// Supplies the sibling. Null when only the author can answer it; otherwise returns
            /// false when it looked and decided it could not, in which case
            /// <see cref="SayInstead"/> is said.
            /// </summary>
            public Func<GameObject, bool> FillIn { get; private set; }

            /// <summary>
            /// Whether <see cref="FillIn"/> applies to this object at all. Null means always.
            /// </summary>
            /// <remarks>
            /// <para>
            /// IT IS WHAT MAKES "the row said it fills this in" A CLAIM THAT CAN BE CHECKED. The
            /// guard reads the object after the sweep and treats a gap left by a row that fills as
            /// broken. Written from <c>FillIn != null</c> alone that was wrong by construction: a
            /// door with nothing to use on it cannot be given a use trigger — the game's own
            /// converter reaches into the picture by index and throws — so its row declines and
            /// speaks, exactly as designed, and the guard called that a break.
            /// </para>
            /// <para>
            /// So the precondition is declared here, beside the fill, and it must be a pure read:
            /// the guard asks it while listing, and the listing is the half of the guard that is
            /// not allowed to change the object. A row whose fill can decline for any other reason
            /// does not belong in the fill column at all — its answer is a sentence.
            /// </para>
            /// </remarks>
            public Func<GameObject, bool> OnlyWhen { get; private set; }

            /// <summary>
            /// What the author is told when <see cref="FillIn"/> is null, or when
            /// <see cref="OnlyWhen"/> says this object is not one it can fill. Plain words, no
            /// component names: it has to be actionable by someone who has never opened Unity.
            /// </summary>
            public string SayInstead { get; private set; }
        }

        private static Companion[] rows;

        /// <summary>
        /// Every companion this framework knows about. Read by <see cref="CloseTheGaps"/> and by the
        /// guard test.
        /// </summary>
        private static Companion[] Rows
        {
            get
            {
                if (rows == null)
                {
                    rows = BuildRows();
                }

                return rows;
            }
        }
    }
}
