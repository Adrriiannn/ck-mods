using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Turns an object name into the number the game knows it by, wherever that name came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE GAME'S OWN NAMES FIRST, AND THAT ORDER IS NOT A PREFERENCE. Core Keeper's runtime lookup
    /// (<c>API.Authoring.GetObjectID</c>) reads <c>ModAPIAuthoring.ObjectIDLookup</c>, which
    /// <c>RegisterAuthoringGameObject</c> fills as mods load. Outside a loaded game that dictionary
    /// is empty and every vanilla name comes back as <c>None</c>. Parsing the <c>ObjectID</c> enum
    /// first means this answers correctly in the editor, in a test, and in the game — a strict
    /// superset of what a bare <c>Enum.TryParse</c> answers, so nothing that resolved before can
    /// stop resolving.
    /// </para>
    /// <para>
    /// A mod's own object is the other way round: it has no enum member and only exists in that
    /// dictionary, under its qualified name (<c>MyMod:EmberBolt</c>). So the two halves cover
    /// exactly one case each, and neither shadows the other.
    /// </para>
    /// <para>
    /// NOTHING ELSE IN THE RUNTIME TURNS A NAME INTO AN OBJECT. The fish registry, the
    /// upgrade-cost registry, the plant converter and the portal id cache all ask here. The reason
    /// it is a rule and not a preference: <c>Enum.TryParse("None")</c> succeeds, so a copy that
    /// forgets the <c>!= ObjectID.None</c> test reports the absence of an object as an object, and
    /// four copies is four chances to forget it.
    /// </para>
    /// </remarks>
    public static class DimensionObjectNames
    {
        private static readonly HashSet<string> Warned =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The id behind a name, or <c>None</c> when nothing answers to it yet.</summary>
        public static ObjectID Resolve(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return ObjectID.None;
            }

            ObjectID vanilla;
            if (Enum.TryParse(objectName, false, out vanilla) && vanilla != ObjectID.None)
            {
                return vanilla;
            }

            // A plain static FIELD a loaded mod context assigns, so it is null in the editor and in
            // tests. Null means "nothing registered to look through", which is the ordinary editor
            // state rather than a failure, so it answers quietly.
            IAuthoring authoring = API.Authoring;
            return authoring == null ? ObjectID.None : authoring.GetObjectID(objectName);
        }

        /// <summary>
        /// The same answer, with one line in the log the first time a name comes back empty.
        /// </summary>
        /// <remarks>
        /// Kept apart from <see cref="Resolve"/> because most callers retry every tick while content
        /// is still loading, and a name that has not arrived yet is not a mistake. Only a caller that
        /// has decided the wait is over should say so.
        /// </remarks>
        public static ObjectID ResolveOrSayWhy(string objectName, string what)
        {
            ObjectID id = Resolve(objectName);
            if (id != ObjectID.None || string.IsNullOrEmpty(objectName))
            {
                return id;
            }

            if (Warned.Add(objectName))
            {
                DimensionFrameworkLog.Warning(
                    "A " + what + " names '" + objectName + "', which is not an " +
                    "object the game has. If it is one of yours, write it with your mod in front of " +
                    "it, like 'MyMod:Tomato', and generate again; until then that part does nothing.");
            }

            return id;
        }

        /// <summary>Forgets what has already been complained about. For tests and mod reloads.</summary>
        public static void ClearWarnings()
        {
            Warned.Clear();
        }
    }
}
