using ExpandNullforge.Authoring;
using UnityEditor;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pours one of the game's own attacks into a creature's ordinary fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE PRESET IS POURED IN, NOT LINKED. Every value lands in the field it belongs to, where an
    /// author can see it and change it. Nothing on the creature records which preset it came from,
    /// because a creature that remembered would have to be re-poured whenever the table changed, and
    /// an author who edited three numbers would find them silently reverted.
    /// </para>
    /// <para>
    /// EVERY VALUE IS TEXT, because the table is generated from a document rather than typed by
    /// hand. Turning that text into a field's real type is this class's job, and a value that will
    /// not convert is reported rather than dropped — a preset that silently skips its damage number
    /// is worse than one that says it could not read it.
    /// </para>
    /// </remarks>
    public static class DimensionBorrowedAttackUtility
    {
        /// <summary>
        /// Writes a preset into a creature's combat block.
        /// </summary>
        /// <param name="combat">The <c>combat</c> property on a creature asset.</param>
        /// <param name="preset">The attack being borrowed.</param>
        /// <param name="report">Somewhere to say what could not be written.</param>
        /// <returns>How many values landed.</returns>
        public static int Apply(
            SerializedProperty combat,
            DimensionBorrowedAttacks.Preset preset,
            System.Action<string> report)
        {
            if (combat == null || preset == null || preset.Values == null)
            {
                return 0;
            }

            // WHAT THE HARVEST COULD NOT CARRY, added back before anything is written. A prefab
            // records a pointer at another object as a file id, which means nothing outside the
            // file it was written in, so the harvest drops every one — and for the ten things in
            // the game that blow up, that dropped the explosion itself. The catalog names them.
            DimensionBorrowedAttacks.Value[] values = preset.Values;
            DimensionBorrowedAttacks.Value[] finishing =
                DimensionBorrowedAttackCatalog.FinishingValues(preset);
            if (finishing != null && finishing.Length > 0)
            {
                DimensionBorrowedAttacks.Value[] both =
                    new DimensionBorrowedAttacks.Value[values.Length + finishing.Length];
                System.Array.Copy(values, both, values.Length);
                System.Array.Copy(finishing, 0, both, values.Length, finishing.Length);
                values = both;
            }

            // A charge, jump or explosion is an entry in the creature's ability list rather than
            // fields flat on the combat block, so borrowing one APPENDS an entry — a creature that
            // already charges and borrows a leap ends up doing both, which is what borrowing means.
            SerializedProperty ability = null;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Path.StartsWith("@ability.", System.StringComparison.Ordinal))
                {
                    SerializedProperty list = combat.FindPropertyRelative("abilities");
                    if (list != null && list.isArray)
                    {
                        list.arraySize++;
                        ability = list.GetArrayElementAtIndex(list.arraySize - 1);
                    }

                    break;
                }
            }

            int written = 0;
            for (int i = 0; i < values.Length; i++)
            {
                DimensionBorrowedAttacks.Value value = values[i];
                SerializedProperty field;
                if (value.Path.StartsWith("@ability.", System.StringComparison.Ordinal))
                {
                    field = ability == null
                        ? null
                        : ability.FindPropertyRelative(value.Path.Substring(9));
                }
                else
                {
                    field = combat.FindPropertyRelative(value.Path);
                }
                if (field == null)
                {
                    if (report != null)
                    {
                        report(
                            "'" + preset.Name + "' carries a value for '" + value.Path +
                            "', which this version of the framework no longer asks about, so that " +
                            "part of the attack is left at its default.");
                    }

                    continue;
                }

                if (Write(field, value.Text))
                {
                    written++;
                }
                else if (report != null)
                {
                    report(
                        "'" + preset.Name + "' has '" + value.Text + "' for '" + value.Path +
                        "', which does not fit that field, so it is left at its default.");
                }
            }

            TurnTheAttackOn(combat, preset.Kind);

            return written;
        }

        /// <summary>
        /// Makes the creature actually have the attack it just borrowed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creature answers one question about what it does in a fight — nothing, melee, ranged,
        /// or both — and the generator only writes the attack components that answer allows. Pouring
        /// a ranged attack into a melee-only creature would fill fields nothing reads, so the answer
        /// is widened rather than replaced: a melee creature that borrows a shot ends up doing both.
        /// </para>
        /// <para>
        /// Two sorts are gated by a switch of their own instead: the sweeping ray, and wandering.
        /// Both are turned on with the values, for the same reason — a switch left off means the
        /// generator writes no component at all, and a move that lands its numbers into a creature
        /// that will never use them is the quietest way for this to fail.
        /// </para>
        /// </remarks>
        private static void TurnTheAttackOn(SerializedProperty combat, string kind)
        {
            // A ray is gated by its own toggle rather than the attack kind.
            if (kind == "Ray")
            {
                SerializedProperty sweeps =
                    combat.FindPropertyRelative("moreCombat.sweepsARay");
                if (sweeps != null && sweeps.propertyType == SerializedPropertyType.Boolean)
                {
                    sweeps.boolValue = true;
                }

                return;
            }

            // A wander is gated the same way the ray is — by a switch of its own rather than by
            // the attack kind. The generator only writes the random walk when idle movement says
            // "wander nearby", so pouring seven wander numbers into a creature set to stand still
            // writes seven numbers nothing will ever read and says it worked. The
            // switch is turned on with them, which is what taking a wander means.
            if (kind == "Wander")
            {
                SerializedProperty idle = combat.FindPropertyRelative("idleMovement");
                if (idle != null && idle.propertyType == SerializedPropertyType.Enum)
                {
                    idle.enumValueIndex = (int)DimensionCreatureIdleMovement.WanderNearby;
                }

                return;
            }

            // Only the two flat attacks answer the attack-kind question. A chase, sound or
            // ability is carried entirely by its own values, so widening the attack kind because
            // one was borrowed would give the creature a swing it never asked for.
            if (kind != "Melee" && kind != "Ranged")
            {
                return;
            }

            SerializedProperty attackKind = combat.FindPropertyRelative("attackKind");
            if (attackKind == null || attackKind.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            DimensionCreatureAttackKind current =
                (DimensionCreatureAttackKind)attackKind.enumValueIndex;
            DimensionCreatureAttackKind borrowed = kind == "Ranged"
                ? DimensionCreatureAttackKind.Ranged
                : DimensionCreatureAttackKind.Melee;

            attackKind.enumValueIndex =
                (int)(DimensionCreatureAttackKind)((int)current | (int)borrowed);
        }

        /// <summary>Puts one piece of text into one field, whatever type that field is.</summary>
        private static bool Write(SerializedProperty field, string text)
        {
            string value = text ?? string.Empty;

            switch (field.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    float parsed;
                    if (!float.TryParse(
                            value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        return false;
                    }

                    field.floatValue = parsed;
                    return true;
                }

                case SerializedPropertyType.Integer:
                {
                    // The vault writes whole numbers plainly, but a float that happens to be whole
                    // reaches an int field as "2" or "2.0" depending on the prefab.
                    float parsed;
                    if (!float.TryParse(
                            value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        return false;
                    }

                    field.intValue = UnityEngine.Mathf.RoundToInt(parsed);
                    return true;
                }

                case SerializedPropertyType.Boolean:
                {
                    field.boolValue = value == "1" || value.ToLowerInvariant() == "true";
                    return true;
                }

                case SerializedPropertyType.String:
                {
                    // A sound travels as the hash the prefab stored, because the document the
                    // table is generated from never knew the name. The name list lives here in
                    // C#, so this is where the hash becomes readable again — or fails to, for
                    // the handful of sounds whose name survives nowhere.
                    if (value.StartsWith("#sfx:", System.StringComparison.Ordinal))
                    {
                        int hash;
                        if (!int.TryParse(
                                value.Substring(5),
                                System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out hash))
                        {
                            return false;
                        }

                        string name = DimensionSoundNames.NameForHash(hash);
                        if (name == null)
                        {
                            // No shipped sound hashes to this number, so there is no name to
                            // write. Reported rather than silently blank.
                            return false;
                        }

                        field.stringValue = name;
                        return true;
                    }

                    field.stringValue = value;
                    return true;
                }

                case SerializedPropertyType.Enum:
                {
                    string[] names = field.enumNames;
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i] == value)
                        {
                            field.enumValueIndex = i;
                            return true;
                        }
                    }

                    // Some are authored as the raw number the game stores.
                    int index;
                    if (int.TryParse(value, out index) && index >= 0 && index < names.Length)
                    {
                        field.enumValueIndex = index;
                        return true;
                    }

                    return false;
                }

                default:
                    return false;
            }
        }
    }
}
