using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
// BindProperty is an extension on UnityEditor.UIElements, not UnityEditor. Every other page in
// this folder that binds a control imports it; this file bound one without it and did not compile.
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The lists behind the fields that ask for a name the game already knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY ONE OF THESE FIELDS USED TO BE A BLANK BOX. The stat effect a condition applies, the
    /// sound a creature makes, the puff of dust a plant throws off, the skill a talent belongs to:
    /// each is one value out of a list Core Keeper holds, and the list was in a decompile. This is
    /// where the lists live, and every field that needs one points at the same list, so a value the
    /// picker offers is a value the generator resolves.
    /// </para>
    /// <para>
    /// THE GROUPINGS ARE OURS AND THE GAME HAS NONE. Core Keeper's effects are one flat enum in the
    /// order they were added. The headings below are the framework's editorial layer over that, and
    /// they are guesses about what a creator is looking for — which is exactly why the picker always
    /// offers "Show every one, in one flat list". Nobody is ever prevented from reaching a value
    /// because a heading here was chosen badly.
    /// </para>
    /// </remarks>
    internal static class DimensionFieldChoosers
    {
        // ------------------------------------------------------------- the rows ---

        /// <summary>Which of the game's 136 usable stat effects this condition applies.</summary>
        internal static VisualElement ConditionEffect(DimensionFieldChooserContext context)
        {
            return EnumChooser(
                context,
                "Pick an effect",
                "This is the part Core Keeper implements. You choose which of its effects your " +
                "condition applies; how much, for how long and on what terms are yours.",
                EffectEntries);
        }

        /// <summary>One of the 1,413 sounds the game ships, by the name it stores it under.</summary>
        internal static VisualElement SoundName(DimensionFieldChooserContext context)
        {
            return StringChooser(
                context,
                "Pick a sound",
                "These are the names Core Keeper's own sounds go by. A sound your mod ships has a " +
                "name of its own and is typed in rather than picked. This list cannot play a " +
                "sound: the name and the audio file are addressed two different ways, and only " +
                "the file can be played.",
                "No sound",
                SoundEntries);
        }

        /// <summary>One of the 426 puffs of dust and sparks the game can throw off.</summary>
        internal static VisualElement PuffName(DimensionFieldChooserContext context)
        {
            return StringChooser(
                context,
                "Pick a puff",
                "A puff is the little burst of dust, leaves or sparks the game throws off. These " +
                "are all of Core Keeper's own; a mod cannot add one.",
                "No puff",
                PuffEntries);
        }

        /// <summary>One of the twelve skills, rather than a name typed from memory.</summary>
        internal static VisualElement SkillName(DimensionFieldChooserContext context)
        {
            return StringChooser(
                context,
                "Pick a skill",
                "The twelve skills are the game's. A talent named for anything else is left out " +
                "of the build with a warning, rather than written into a table with no room for it.",
                string.Empty,
                SkillEntries);
        }

        /// <summary>
        /// A stat effect by name: Core Keeper's own first, then the ones this dimension invents.
        /// </summary>
        /// <remarks>
        /// Built from the same two sources <c>DimensionObjectSpine.TryResolveCondition</c> answers
        /// from, and in the same order, so a value this picker offers is a value the generator
        /// resolves. Building the picker anywhere else is how the two drift.
        /// </remarks>
        internal static VisualElement ConditionName(DimensionFieldChooserContext context)
        {
            DimensionTemplateAsset template = context == null ? null : context.Template;
            return StringChooser(
                context,
                "Pick an effect",
                "Core Keeper's own effects, and the ones this dimension invents. Typing a name " +
                "that is neither is reported when you generate.",
                "No effect",
                () => ConditionEntries(template));
        }

        /// <summary>
        /// A talent name: the game's own 96 when you only mean to change the numbers.
        /// </summary>
        internal static VisualElement TalentName(DimensionFieldChooserContext context)
        {
            return StringChooser(
                context,
                "Pick a talent name",
                "Reusing one of the game's own talent names keeps its wording and its picture in " +
                "every language, and changes only the numbers. Type anything else and the talent " +
                "becomes yours, with a line of your own written for it.",
                string.Empty,
                TalentEntries);
        }

        // ---------------------------------------------------------- the controls ---

        /// <summary>
        /// A bound text box with a button beside it, because typing has to keep working.
        /// </summary>
        /// <remarks>
        /// A mod that ships a sound of its own has a name no list here can know. Replacing the box
        /// with a dropdown would take that away, so the box stays and the button is an offer.
        /// </remarks>
        private static VisualElement StringChooser(
            DimensionFieldChooserContext context,
            string headline,
            string help,
            string emptyValueLabel,
            Func<IReadOnlyList<DimensionNamePickerWindow.Entry>> entries)
        {
            if (context == null ||
                context.Property == null ||
                context.Property.propertyType != SerializedPropertyType.String)
            {
                return null;
            }

            SerializedProperty property = context.Property;
            Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            TextField field = new TextField();
            field.style.flexGrow = 1f;
            field.BindProperty(property);
            row.Add(field);

            Button browse = new Button(() =>
            {
                DimensionNamePickerWindow.Open(
                    headline,
                    help,
                    emptyValueLabel,
                    entries(),
                    ReadString(target, path),
                    picked => WriteString(target, path, picked));
            })
            {
                text = "Browse"
            };
            browse.style.flexShrink = 0f;
            row.Add(browse);
            return row;
        }

        /// <summary>
        /// A button showing the value, because an enum cannot hold anything off the list anyway.
        /// </summary>
        private static VisualElement EnumChooser(
            DimensionFieldChooserContext context,
            string headline,
            string help,
            Func<IReadOnlyList<DimensionNamePickerWindow.Entry>> entries)
        {
            if (context == null ||
                context.Property == null ||
                context.Property.propertyType != SerializedPropertyType.Enum)
            {
                return null;
            }

            SerializedProperty property = context.Property;
            Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;

            Button button = new Button { text = ReadEnumName(target, path) };
            button.style.flexGrow = 1f;
            button.clicked += () =>
            {
                DimensionNamePickerWindow.Open(
                    headline,
                    help,
                    string.Empty,
                    entries(),
                    ReadEnumName(target, path),
                    picked =>
                    {
                        WriteEnumName(target, path, picked);
                        button.text = ReadEnumName(target, path);
                    });
            };

            // The value can also change from underneath — an undo, or the raw field in "Everything
            // else" — and a button that keeps showing the old name would be lying.
            button.schedule.Execute(() => button.text = ReadEnumName(target, path))
                .Every(250);
            return button;
        }

        // ------------------------------------------------------------ the values ---

        private static string ReadString(Object target, string path)
        {
            if (target == null)
            {
                return string.Empty;
            }

            SerializedProperty property = new SerializedObject(target).FindProperty(path);
            return property == null ? string.Empty : property.stringValue ?? string.Empty;
        }

        private static void WriteString(Object target, string path, string value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedProperties();
        }

        private static string ReadEnumName(Object target, string path)
        {
            if (target == null)
            {
                return string.Empty;
            }

            SerializedProperty property = new SerializedObject(target).FindProperty(path);
            if (property == null ||
                property.enumValueIndex < 0 ||
                property.enumValueIndex >= property.enumNames.Length)
            {
                return string.Empty;
            }

            return property.enumNames[property.enumValueIndex];
        }

        /// <summary>
        /// Writes an enum by NAME rather than by number.
        /// </summary>
        /// <remarks>
        /// <c>enumValueIndex</c> is a position in the list of names, not the value behind it. They
        /// happen to agree for every enum here, and writing by name means they do not have to.
        /// </remarks>
        private static void WriteEnumName(Object target, string path, string name)
        {
            if (target == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                return;
            }

            string[] names = property.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    property.enumValueIndex = i;
                    serialized.ApplyModifiedProperties();
                    return;
                }
            }
        }

        // ------------------------------------------------------------- the lists ---

        /// <summary>
        /// Which heading each stat effect sits under. First match wins, and anything that matches
        /// nothing lands in "Rare and situational" — which is a heading, not a verdict.
        /// </summary>
        private static readonly string[][] EffectGroups =
        {
            new[] { "Magic, pets and minions", "Mana", "Magic", "Minion", "Pet", "Charm", "Barrier", "Summon" },
            new[] { "Digging and gathering", "Mining", "Fishing", "Harvest", "Digging", "Ore", "LootFromWall", "Seed", "Plant" },
            new[] { "Food and hunger", "Hunger", "Cooked", "Food", "Sleep" },
            new[] { "Staying alive", "Health", "Armor", "Heal", "Dodge", "Immune", "Vulnerable", "DamageTaken", "Protective", "Regen" },
            new[] { "Getting about", "Movement", "Slippery", "Snare", "Slime" },
            new[] { "Damage you deal", "Damage", "Crit", "AttackSpeed", "Thorns", "Explosive", "Burning", "Poison", "Stun", "Knockback", "Beam", "Projectile" },
        };

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> EffectEntries()
        {
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>();
            string[] names = Enum.GetNames(typeof(ConditionEffect));
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];

                // Placeholders the game reserved and never implemented, and the end marker. Left
                // out of the offer; a value already set to one is still shown in the field, so
                // opening the picker never quietly changes an asset.
                if (name == "MAX_VALUES" || name.IndexOf("Unused", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                entries.Add(new DimensionNamePickerWindow.Entry(
                    name,
                    name == "None" ? "Nothing at all" : GroupForEffect(name),
                    string.Empty));
            }

            return entries;
        }

        private static string GroupForEffect(string name)
        {
            for (int g = 0; g < EffectGroups.Length; g++)
            {
                string[] rule = EffectGroups[g];
                for (int k = 1; k < rule.Length; k++)
                {
                    if (name.IndexOf(rule[k], StringComparison.Ordinal) >= 0)
                    {
                        return rule[0];
                    }
                }
            }

            return "Rare and situational";
        }

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> SoundEntries()
        {
            string[] all = DimensionSoundNames.All;
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                entries.Add(new DimensionNamePickerWindow.Entry(
                    all[i], LeadingToken(all[i]), string.Empty));
            }

            return entries;
        }

        /// <summary>
        /// The run of letters a sound name starts with, which is how the shipped names are already
        /// grouped in practice — every hydra boss sound begins "hydraBoss".
        /// </summary>
        private static string LeadingToken(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Everything else";
            }

            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                {
                    return name.Substring(0, i);
                }
            }

            return name;
        }

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> PuffEntries()
        {
            string[] names = Enum.GetNames(typeof(PuffID));
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                entries.Add(new DimensionNamePickerWindow.Entry(
                    names[i], LeadingToken(names[i]), string.Empty));
            }

            return entries;
        }

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> SkillEntries()
        {
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>();
            string[] names = Enum.GetNames(typeof(SkillID));
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == "NUM_SKILLS")
                {
                    continue;
                }

                entries.Add(new DimensionNamePickerWindow.Entry(
                    names[i], "The twelve skills", string.Empty));
            }

            return entries;
        }

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> TalentEntries()
        {
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>();
            string[] skills = Enum.GetNames(typeof(SkillID));
            for (int s = 0; s < skills.Length; s++)
            {
                if (skills[s] == "NUM_SKILLS")
                {
                    continue;
                }

                IReadOnlyList<string> names =
                    DimensionVanillaTalentNames.ForSkill((SkillID)s);
                for (int i = 0; i < names.Count; i++)
                {
                    entries.Add(new DimensionNamePickerWindow.Entry(
                        names[i],
                        skills[s],
                        "talent " + (i + 1)));
                }
            }

            return entries;
        }

        internal static IReadOnlyList<DimensionNamePickerWindow.Entry> ConditionEntries(
            DimensionTemplateAsset template)
        {
            List<DimensionNamePickerWindow.Entry> entries =
                new List<DimensionNamePickerWindow.Entry>();

            string[] names = Enum.GetNames(typeof(ConditionID));
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == "MAX_VALUES" || names[i] == "None")
                {
                    continue;
                }

                entries.Add(new DimensionNamePickerWindow.Entry(
                    names[i], "Core Keeper's own", string.Empty));
            }

            // A drawer sitting inside an array of structs has no way of knowing which dimension it
            // belongs to, so when nobody could say, every condition asset in the project is
            // offered. Wider than the dimension, but a name from the wrong dimension is reported
            // at generate time — and a blank box was wider still.
            DimensionConditionAsset[] ours = template == null
                ? EveryConditionAssetInTheProject()
                : template.GlobalConditions;
            if (ours != null)
            {
                for (int i = 0; i < ours.Length; i++)
                {
                    DimensionConditionAsset asset = ours[i];
                    if (asset == null || string.IsNullOrEmpty(asset.ConditionName))
                    {
                        continue;
                    }

                    entries.Add(new DimensionNamePickerWindow.Entry(
                        asset.ConditionName,
                        "This dimension's own",
                        asset.Enabled ? asset.DisplayName : asset.DisplayName + " (switched off)"));
                }
            }

            return entries;
        }

        private static DimensionConditionAsset[] EveryConditionAssetInTheProject()
        {
            string[] guids = AssetDatabase.FindAssets("t:DimensionConditionAsset");
            List<DimensionConditionAsset> found =
                new List<DimensionConditionAsset>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                DimensionConditionAsset asset =
                    AssetDatabase.LoadAssetAtPath<DimensionConditionAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null)
                {
                    found.Add(asset);
                }
            }

            return found.ToArray();
        }
    }
}
