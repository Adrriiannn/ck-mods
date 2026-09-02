using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>Whether a stored name carried its mod qualifier or not.</summary>
    internal enum DimensionIdForm
    {
        /// <summary><c>EmberBolt</c>.</summary>
        Local = 0,

        /// <summary><c>MyMod:EmberBolt</c>.</summary>
        Qualified = 1
    }

    /// <summary>What a place holds: the name typed out, or the thing itself.</summary>
    internal enum DimensionIdHold
    {
        /// <summary>A string field with the name written into it.</summary>
        TheName = 0,

        /// <summary>
        /// A slot holding the asset itself. There is no name in it, so a rename has nothing to
        /// rewrite there — but it is still a place that points at the thing, and still a place a
        /// delete leaves holding nothing.
        /// </summary>
        TheThingItself = 1
    }

    /// <summary>One place a name was found: which asset, which field, and what was written there.</summary>
    internal readonly struct DimensionIdReference
    {
        internal DimensionIdReference(
            Object asset,
            string propertyPath,
            string fieldName,
            string rawValue,
            DimensionIdForm form,
            bool outsideThisPack,
            bool isAName,
            DimensionIdHold hold = DimensionIdHold.TheName,
            string becomesLocal = null,
            string builtFrom = null)
        {
            Asset = asset;
            PropertyPath = propertyPath;
            FieldName = fieldName;
            RawValue = rawValue;
            Form = form;
            OutsideThisPack = outsideThisPack;
            IsAName = isAName;
            Hold = hold;
            BecomesLocal = becomesLocal ?? string.Empty;
            BuiltFrom = builtFrom ?? string.Empty;
            ReadsAs = hold == DimensionIdHold.TheName
                ? DimensionIdentityCatalog.ReadsAs(propertyPath)
                : string.Empty;
        }

        /// <summary>
        /// The asset holding it. Always the ScriptableObject, never a nested block: a nested block
        /// cannot be selected, pinged or written through, and the path carries the rest.
        /// </summary>
        internal Object Asset { get; }

        /// <summary>The serialized path, e.g. <c>rooms.Array.data[2].objectId</c>.</summary>
        internal string PropertyPath { get; }

        /// <summary>The same path in the words the page shows.</summary>
        internal string FieldName { get; }

        /// <summary>Exactly what is stored, qualifier and all.</summary>
        internal string RawValue { get; }

        /// <summary>Which form it was written in, so a rewrite can put it back the same way.</summary>
        internal DimensionIdForm Form { get; }

        /// <summary>True when the asset belongs to a different dimension in this Unity project.</summary>
        internal bool OutsideThisPack { get; }

        /// <summary>True when this field is one of its own asset's names rather than a pointer.</summary>
        internal bool IsAName { get; }

        /// <summary>Whether the name is written here, or the thing itself is held here.</summary>
        internal DimensionIdHold Hold { get; }

        /// <summary>
        /// The local id this place should be rewritten to, when that is not simply the new name —
        /// a plant's "EmberSeed" becoming "AshSeed". Empty for an ordinary pointer.
        /// </summary>
        internal string BecomesLocal { get; }

        /// <summary>
        /// What this value was built out of, in the creator's words — "the seed built from this
        /// plant's id" — or empty when the value is the name itself.
        /// </summary>
        internal string BuiltFrom { get; }

        /// <summary>
        /// The kind of thing this field's NAME reads as, or empty when it says nothing. A hint used
        /// only to leave a row unticked, never to tick one on. See
        /// <see cref="DimensionIdentityCatalog.ReadsAs"/>.
        /// </summary>
        internal string ReadsAs { get; }
    }

    /// <summary>
    /// One other thing in the pack that answers to the same name, and what kind it is.
    /// </summary>
    internal readonly struct DimensionIdClaimant
    {
        internal DimensionIdClaimant(Object asset, string thing, string label)
        {
            Asset = asset;
            Thing = thing;
            Label = label;
        }

        /// <summary>The asset that also carries this name.</summary>
        internal Object Asset { get; }

        /// <summary>The creator's word for it: "loot table", "item".</summary>
        internal string Thing { get; }

        /// <summary>What that name is called on its own page.</summary>
        internal string Label { get; }
    }

    /// <summary>
    /// Everywhere a name is written, found by matching the value rather than by knowing what any
    /// field means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO SWEEPS, BECAUSE THERE ARE TWO KINDS OF EDGE. Nearly every pointer in this framework is
    /// a plain serialized string, and <see cref="Find"/> is for those. Twelve are not: a creature
    /// holds its loot table, a biome holds its scenes, a dungeon holds its rooms — those hold the
    /// asset, and <see cref="FindHolders"/> is for those. This class used to have only the first,
    /// so a loot table three monsters drop answered "Nothing uses this yet".
    /// </para>
    /// <para>
    /// WHY THE STRING SWEEP DOES NOT NEED TO KNOW WHAT A FIELD MEANS, and why that is the point.
    /// A serialized id is a plain string, so Unity's own "find references" is blind to every one
    /// of them. A sweep over
    /// <c>SerializedObject.GetIterator()</c> with <c>Next(true)</c> matches on the VALUE. It does
    /// not need to know that <c>DimensionNestBrood.objectId</c> means a creature. It over-reports
    /// where two kinds of thing share a literal string; it CANNOT under-report. That trade is the
    /// whole design: a creator gets a list to read and tick, and a list that is honestly too long
    /// is worth incomparably more than a list that is quietly too short. What that trade needs, and
    /// did not have, is that the over-report does NOT arrive with the ticks already in — see
    /// <see cref="OtherThingsCalled"/> and <c>DimensionIdRenamePlan.StartsTicked</c>.
    /// </para>
    /// <para>
    /// <c>Next(true)</c> AND NOT <c>NextVisible</c>. A hidden field is still shipped state, and
    /// this framework has real ones holding real references:
    /// <c>DimensionTilesetAsset.oreScatterItemIds</c> is a <c>[HideInInspector] string[]</c> naming
    /// items, and it is what a block scatters as ore. <c>NextVisible</c> walks straight past it, so
    /// a rename driven by that walk would leave the block scattering an item nobody makes any more.
    /// </para>
    /// <para>
    /// MATCHING IS ON THE LOCAL FORM AND REWRITING PRESERVES THE FORM.
    /// <c>DimensionNamingContext.Owns</c> makes <c>MyMod:EmberBolt</c> and <c>EmberBolt</c> reach
    /// the same asset, so both have to be found; but a field written unqualified must stay
    /// unqualified, or the project ends up half-fixed in a way that only shows up in game.
    /// </para>
    /// <para>
    /// EDITOR ONLY. Nothing here ships and nothing generated calls it.
    /// </para>
    /// </remarks>
    internal static class DimensionIdReferences
    {
        /// <summary>
        /// Fields that hold prose rather than a name, and are never rewritten.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Small and named one by one on purpose. Anything left off it is over-reported, which a
        /// tick list survives; anything wrongly ON it would be silently missed, which is the one
        /// failure this whole class exists to prevent. So it holds only fields a creator writes
        /// sentences into.
        /// </para>
        /// <para>
        /// <c>blockName</c> USED TO BE ON IT and is not any more. The reasoning was that it is a
        /// block's own identity and nothing points at a block by it — both true — but a field on
        /// this list is invisible to the sweep in BOTH directions, so the re-sweep that checks a
        /// rename finished could never see a block's own name and would report a half-written block
        /// rename as a clean one. It is an identity, and the catalog already says so, so it is
        /// found like every other identity and marked rather than hidden.
        /// </para>
        /// </remarks>
        private static readonly string[] ProseFields =
        {
            "displayName",
            "description",
            "notes",
            "promptText",
            "textItComesWith",
            "goldenName",
            "contentPackDisplayName",
            "contentPackAuthor",
            "lastBuildMessage",
            "publishedUtc",
            "fingerprint"
        };

        /// <summary>How deep the walk from one dimension to its assets is allowed to go.</summary>
        /// <remarks>
        /// A guard, not a limit anything reaches: the deepest real chain is a dimension to a biome
        /// to a scene to its pool, four hops. <c>grep SerializeReference</c> over Scripts returns
        /// nothing, so a cycle among the nested blocks is impossible today; this stops a future one
        /// from hanging the editor rather than saving any work now.
        /// </remarks>
        private const int MaxHops = 10;

        /// <summary>True when this path names a field that holds prose rather than a pointer.</summary>
        internal static bool IsProse(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return true;
            }

            string leaf = LeafOf(propertyPath);
            for (int i = 0; i < ProseFields.Length; i++)
            {
                if (string.Equals(leaf, ProseFields[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The last real field name in a serialized path, with array plumbing folded away.</summary>
        internal static string LeafOf(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return string.Empty;
            }

            string folded = propertyPath.Replace(".Array.data[", "[");
            int dot = folded.LastIndexOf('.');
            string leaf = dot < 0 ? folded : folded.Substring(dot + 1);
            int bracket = leaf.IndexOf('[');
            return bracket < 0 ? leaf : leaf.Substring(0, bracket);
        }

        /// <summary>
        /// Every authored asset one dimension owns, following both the lists it holds them in and
        /// the typed references between them.
        /// </summary>
        /// <remarks>
        /// Walked rather than listed. Naming the twenty-four containment arrays by hand would need
        /// editing every time the authoring layer grows an array, and forgetting one would make the
        /// sweep quietly incomplete — the one failure mode that must not exist here. Every
        /// reference to an authoring ScriptableObject is followed once, which is exactly the
        /// containment closure plus the typed references, with no list to keep up to date.
        /// </remarks>
        internal static List<Object> AssetsOf(DimensionTemplateAsset template)
        {
            List<Object> assets = new List<Object>();
            if (template == null)
            {
                return assets;
            }

            HashSet<int> seen = new HashSet<int>();
            Queue<Object> pending = new Queue<Object>();
            Queue<int> hops = new Queue<int>();
            pending.Enqueue(template);
            hops.Enqueue(0);
            seen.Add(template.GetInstanceID());

            while (pending.Count > 0)
            {
                Object current = pending.Dequeue();
                int hop = hops.Dequeue();
                assets.Add(current);
                if (hop >= MaxHops)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(current);
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object referenced = iterator.objectReferenceValue;
                    if (!IsAuthoredAsset(referenced) || !seen.Add(referenced.GetInstanceID()))
                    {
                        continue;
                    }

                    pending.Enqueue(referenced);
                    hops.Enqueue(hop + 1);
                }
            }

            return assets;
        }

        /// <summary>
        /// A ScriptableObject this framework authors, as opposed to a sprite, a texture or the
        /// MonoScript every serialized object starts with.
        /// </summary>
        internal static bool IsAuthoredAsset(Object candidate)
        {
            if (candidate == null || !(candidate is ScriptableObject))
            {
                return false;
            }

            string space = candidate.GetType().Namespace;
            return space != null && space.StartsWith("ExpandNullforge", StringComparison.Ordinal);
        }

        /// <summary>
        /// Every place <paramref name="id"/> is written across this dimension, and optionally across
        /// every other dimension in the project.
        /// </summary>
        /// <remarks>
        /// The cross-project sweep is behind a flag and never the default. It loads every dimension
        /// asset on disk, and rewriting another pack's content is a bigger act than rewriting your
        /// own — so it is offered, reported under its own heading, and not done unless asked for.
        /// </remarks>
        internal static List<DimensionIdReference> Find(
            DimensionTemplateAsset template,
            string id,
            bool alsoOutsideThisPack)
        {
            List<DimensionIdReference> found = new List<DimensionIdReference>();
            if (template == null || string.IsNullOrEmpty(id))
            {
                return found;
            }

            string wanted = DimensionObjectNamespace.LocalIdOf(id);
            if (wanted.Length == 0)
            {
                return found;
            }

            HashSet<int> swept = new HashSet<int>();
            List<Object> mine = AssetsOf(template);
            for (int i = 0; i < mine.Count; i++)
            {
                swept.Add(mine[i].GetInstanceID());
                FindIn(mine[i], wanted, false, found);
            }

            if (!alsoOutsideThisPack)
            {
                return found;
            }

            string[] guids = AssetDatabase.FindAssets("t:DimensionTemplateAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                DimensionTemplateAsset other =
                    AssetDatabase.LoadAssetAtPath<DimensionTemplateAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                if (other == null || other == template)
                {
                    continue;
                }

                List<Object> theirs = AssetsOf(other);
                for (int a = 0; a < theirs.Count; a++)
                {
                    if (!swept.Add(theirs[a].GetInstanceID()))
                    {
                        continue;
                    }

                    FindIn(theirs[a], wanted, true, found);
                }
            }

            return found;
        }

        /// <summary>
        /// Every place a SECOND id built out of this one is written, and what each should become.
        /// </summary>
        /// <remarks>
        /// The same sweep as <see cref="Find"/>, over a name the creator never typed on the asset:
        /// a plant called "Ember" is generated as "EmberPlant" and "EmberSeed", and those are the
        /// strings a recipe holds. The rows come back carrying the value they should be rewritten
        /// to, because "the new name" is not it — "EmberSeed" has to become "AshSeed", not "Ash".
        /// A row that is one of the plant's own names is dropped: an identity is never derived.
        /// </remarks>
        internal static List<DimensionIdReference> FindDerived(
            DimensionTemplateAsset template,
            string derivedOldId,
            string derivedNewId,
            string builtFrom,
            bool alsoOutsideThisPack)
        {
            List<DimensionIdReference> rewritten = new List<DimensionIdReference>();
            if (string.IsNullOrEmpty(derivedOldId) || string.IsNullOrEmpty(derivedNewId) ||
                string.Equals(derivedOldId, derivedNewId, StringComparison.Ordinal))
            {
                return rewritten;
            }

            string becomes = DimensionObjectNamespace.LocalIdOf(derivedNewId);
            List<DimensionIdReference> found = Find(template, derivedOldId, alsoOutsideThisPack);
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].IsAName)
                {
                    continue;
                }

                rewritten.Add(new DimensionIdReference(
                    found[i].Asset,
                    found[i].PropertyPath,
                    found[i].FieldName,
                    found[i].RawValue,
                    found[i].Form,
                    found[i].OutsideThisPack,
                    false,
                    DimensionIdHold.TheName,
                    becomes,
                    builtFrom));
            }

            return rewritten;
        }

        /// <summary>
        /// Every slot that holds <paramref name="target"/> itself rather than naming it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY THIS IS A SECOND SWEEP AND NOT A BRANCH IN THE FIRST. The first one asks "where is
        /// this string written", which is a question about a value. This one asks "where is this
        /// asset held", which is a question about an object and cannot be answered from an id: two
        /// packs may both have a loot table called "Ember" and only one of them is this one. So it
        /// takes the asset.
        /// </para>
        /// <para>
        /// WHAT IT IS FOR. There are 39 serialized object-reference fields in the authoring layer.
        /// 27 are the dimension's own containment. The other 12 are asset to asset — a creature's
        /// <c>lootTable</c>, a biome's <c>scenePool</c>, a dungeon's <c>rooms</c> and
        /// <c>roomFillings</c>, a named area's <c>blocks</c>, a workbench's <c>recipes</c>, an
        /// elite's <c>lootTable</c> — and eleven of those point at a thing with a name a creator
        /// can rename. The string sweep is blind to every one of them, so a loot table three
        /// monsters drop reported "Nothing uses this yet" and deleting it left three Missing slots.
        /// </para>
        /// <para>
        /// A RENAME REWRITES NONE OF THESE. There is no name in the slot to rewrite: the slot goes
        /// on holding the same asset whatever it is called. They are listed so the answer to "what
        /// points at this" is true, and they are cleared by a delete, which is the one operation
        /// that leaves them holding nothing.
        /// </para>
        /// </remarks>
        /// <param name="notTheListItLivesIn">
        /// The dimension array this kind of thing hangs off, left out of the answer. It is where
        /// the thing LIVES rather than a place that points at it, every delete takes it out of
        /// there whichever answer is given, and leaving it in would mean "Nothing points at it"
        /// could never be said about anything.
        /// </param>
        internal static List<DimensionIdReference> FindHolders(
            DimensionTemplateAsset template,
            Object target,
            bool alsoOutsideThisPack,
            string notTheListItLivesIn = null)
        {
            List<DimensionIdReference> found = new List<DimensionIdReference>();
            if (template == null || target == null)
            {
                return found;
            }

            string home = notTheListItLivesIn ?? string.Empty;

            HashSet<int> swept = new HashSet<int>();
            List<Object> mine = AssetsOf(template);
            for (int i = 0; i < mine.Count; i++)
            {
                swept.Add(mine[i].GetInstanceID());
                FindHoldersIn(mine[i], target, false, found);
            }

            if (home.Length > 0)
            {
                for (int i = found.Count - 1; i >= 0; i--)
                {
                    if (found[i].Asset == template &&
                        found[i].PropertyPath.StartsWith(home, StringComparison.Ordinal))
                    {
                        found.RemoveAt(i);
                    }
                }
            }

            if (!alsoOutsideThisPack)
            {
                return found;
            }

            string[] guids = AssetDatabase.FindAssets("t:DimensionTemplateAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                DimensionTemplateAsset other =
                    AssetDatabase.LoadAssetAtPath<DimensionTemplateAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                if (other == null || other == template)
                {
                    continue;
                }

                List<Object> theirs = AssetsOf(other);
                for (int a = 0; a < theirs.Count; a++)
                {
                    if (!swept.Add(theirs[a].GetInstanceID()))
                    {
                        continue;
                    }

                    FindHoldersIn(theirs[a], target, true, found);
                }
            }

            return found;
        }

        /// <summary>Every slot on one asset that holds another asset.</summary>
        internal static void FindHoldersIn(
            Object asset,
            Object target,
            bool outsideThisPack,
            List<DimensionIdReference> into)
        {
            if (asset == null || target == null || into == null || asset == target)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                    iterator.objectReferenceValue != target)
                {
                    continue;
                }

                string path = iterator.propertyPath;
                into.Add(new DimensionIdReference(
                    asset,
                    path,
                    Readable(serialized, path),
                    target.name,
                    DimensionIdForm.Local,
                    outsideThisPack,
                    false,
                    DimensionIdHold.TheThingItself));
            }
        }

        /// <summary>
        /// The other things in this pack that answer to the same name, in the creator's words.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS WHAT MAKES THE OVER-REPORT SAFE RATHER THAN ARMED. The sweep matches on the
        /// value and cannot tell an item called "Ember" from a loot table called "Ember" — which is
        /// legal, because the two are in different namespaces and nothing collides. The design took
        /// that as an honest trade on the grounds that a tick list makes it visible; it does not,
        /// if the ticks are already in. So the ticks ask this first: when nothing else in the pack
        /// answers to the name, every pointer to it means this thing and the list behaves as it
        /// always did. When something else does, the rows say so and start off.
        /// </para>
        /// <para>
        /// Only OTHER assets count. A second name on the same asset is already marked
        /// <see cref="DimensionIdReference.IsAName"/> and already unticked, and a creature whose
        /// <c>objectId</c> matches its <c>mobId</c> is one thing with two names, not two things.
        /// </para>
        /// </remarks>
        internal static List<DimensionIdClaimant> OtherThingsCalled(
            DimensionTemplateAsset template,
            string id,
            Object exceptThisAsset)
        {
            List<DimensionIdClaimant> found = new List<DimensionIdClaimant>();
            if (template == null || string.IsNullOrEmpty(id))
            {
                return found;
            }

            string wanted = DimensionObjectNamespace.LocalIdOf(id);
            if (wanted.Length == 0)
            {
                return found;
            }

            List<Object> mine = AssetsOf(template);
            for (int i = 0; i < mine.Count; i++)
            {
                Object asset = mine[i];
                if (asset == exceptThisAsset)
                {
                    continue;
                }

                List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(asset);
                for (int n = 0; n < names.Count; n++)
                {
                    SerializedProperty property =
                        new SerializedObject(asset).FindProperty(names[n].Property);
                    if (property == null ||
                        property.propertyType != SerializedPropertyType.String)
                    {
                        continue;
                    }

                    if (string.Equals(
                            DimensionObjectNamespace.LocalIdOf(property.stringValue ?? string.Empty),
                            wanted,
                            StringComparison.Ordinal))
                    {
                        found.Add(new DimensionIdClaimant(asset, names[n].Thing, names[n].Label));
                    }
                }
            }

            return found;
        }

        /// <summary>Every place one asset writes the name, prose fields excepted.</summary>
        internal static void FindIn(
            Object asset,
            string wantedLocalId,
            bool outsideThisPack,
            List<DimensionIdReference> into)
        {
            if (asset == null || string.IsNullOrEmpty(wantedLocalId) || into == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.String)
                {
                    continue;
                }

                string raw = iterator.stringValue;
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                if (!string.Equals(
                        DimensionObjectNamespace.LocalIdOf(raw),
                        wantedLocalId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string path = iterator.propertyPath;
                if (IsProse(path))
                {
                    continue;
                }

                into.Add(new DimensionIdReference(
                    asset,
                    path,
                    Readable(serialized, path),
                    raw,
                    DimensionObjectNamespace.IsQualified(raw)
                        ? DimensionIdForm.Qualified
                        : DimensionIdForm.Local,
                    outsideThisPack,
                    DimensionIdentityCatalog.IsAName(asset, path)));
            }
        }

        /// <summary>
        /// A serialized path in the words the page shows, e.g. "Ore scatter item ids 2".
        /// </summary>
        /// <remarks>
        /// Built from the properties' own display names rather than from the raw path, because the
        /// raw path is what nobody should ever have to read. Only the last two steps are kept: the
        /// full chain on a deeply nested field reads as a stack trace, and the asset's own name is
        /// already on the row beside it.
        /// </remarks>
        internal static string Readable(SerializedObject serialized, string propertyPath)
        {
            if (serialized == null || string.IsNullOrEmpty(propertyPath))
            {
                return propertyPath ?? string.Empty;
            }

            string[] segments = propertyPath.Replace(".Array.data[", "[").Split('.');
            List<string> words = new List<string>();
            string walked = string.Empty;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                int bracket = segment.IndexOf('[');
                string name = bracket < 0 ? segment : segment.Substring(0, bracket);
                walked = walked.Length == 0 ? name : walked + "." + name;
                SerializedProperty property = serialized.FindProperty(walked);
                string shown = property == null
                    ? ObjectNames.NicifyVariableName(name)
                    : property.displayName;

                if (bracket >= 0)
                {
                    int close = segment.IndexOf(']', bracket);
                    string index = close > bracket
                        ? segment.Substring(bracket + 1, close - bracket - 1)
                        : string.Empty;
                    int parsed;
                    if (int.TryParse(index, out parsed))
                    {
                        shown = shown + " " + (parsed + 1);
                    }

                    walked = walked + ".Array.data[" + index + "]";
                }

                words.Add(shown);
            }

            int from = words.Count > 2 ? words.Count - 2 : 0;
            string readable = string.Empty;
            for (int i = from; i < words.Count; i++)
            {
                readable = readable.Length == 0 ? words[i] : readable + ", " + words[i];
            }

            return readable;
        }
    }
}
