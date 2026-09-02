using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Gives a pet a mod added the colours it can be recoloured into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A PET'S SKINS LIVE IN A TABLE, NOT ON THE PET. <c>PetInfosTable.petSkins</c> is a list of
    /// gradient maps per pet object, and three separate readers ask it the same question through
    /// one method, <c>GetPetSkinInfo(ObjectID)</c>: <c>PetConverter</c>, which turns the count into
    /// <c>PetCD.maxSkins</c> as the pet is converted
    /// (<c>ck-db\Pug.ECS.Conversion\PetConverter.cs:8</c>); <c>PetBase</c>, which applies the chosen
    /// gradient map to the pet's sprite; and the inventory and hover UI, which tint its icon to
    /// match. A pet a mod added answers null there, so it has always converted with a skin count of
    /// zero.
    /// </para>
    /// <para>
    /// THE ANSWER IS SUPPLIED, NOT THE TABLE EDITED, and here that is not just hygiene.
    /// <c>PetInfosTable.GetPetSkinInfo</c> builds a private <c>Dictionary</c> the first time it is
    /// asked and NEVER rebuilds it — <c>OnValidate</c> clears <c>petTalentsLookUp</c> and nothing
    /// clears <c>petSkinsLookUp</c>, not even in the editor
    /// (<c>ck-db\Pug.Base\PetInfosTable.cs:19-21,37-41</c>). So a row appended after the first
    /// question would be invisible for the rest of the session and there is no reflection-free way
    /// to clear it. Answering on the way past has no such window, and the game's own asset is left
    /// exactly as it was.
    /// </para>
    /// <para>
    /// NAMES ARE ANSWERED LATE AND NOT KEPT UNTIL THEY ANSWER. A pet this mod adds has no object
    /// number until its content has loaded, so a name that comes back empty is retried on the next
    /// question rather than remembered as "no such pet".
    /// </para>
    /// </remarks>
    public static class DimensionPetSkinRegistry
    {
        private sealed class Row
        {
            public string PetName;

            /// <summary>
            /// The same name with the mod in front of it, for a row that was written without one.
            /// </summary>
            /// <remarks>
            /// The generator stamps a creature's object name <c>naming.QualifyGenerated(id)</c>
            /// (<c>Editor/Generators/Creatures/DimensionCreatureGenerator.Art.cs</c>), and that
            /// qualified name is the
            /// only key <c>API.Authoring.GetObjectID</c> answers to. Every other consumer of a mob
            /// id runs it through <c>DimensionObjectNamespace.Qualify</c> at generate time; this
            /// one cannot, because it is read off the template at load. The new-asset wizard seeds
            /// the id already qualified, so the ordinary flow worked — but a creator who typed a
            /// plain <c>fluff</c>, which every other feature accepts, got a pet that still
            /// converted with no skins at all: exactly the bug this table was opened up to fix,
            /// while the patch roster reported the patch as having fired.
            /// </remarks>
            public string QualifiedPetName;

            public List<GradientMapDataBlock> Colours;
        }

        private static readonly List<Row> Rows = new List<Row>();

        /// <summary>Pets whose names have been answered, so the lookup is a number test per call.</summary>
        private static readonly Dictionary<int, PetInfosTable.PetSkinInfo> answered =
            new Dictionary<int, PetInfosTable.PetSkinInfo>();

        /// <summary>Whether anything at all was registered, so callers can skip the work.</summary>
        public static bool HasAny { get { return Rows.Count > 0; } }

        /// <summary>How many pets have colours of a mod's own.</summary>
        public static int Count { get { return Rows.Count; } }

        internal static int PendingCount { get { return Rows.Count; } }

        /// <summary>
        /// Gives one pet its colours. Registering the same pet twice replaces the earlier claim.
        /// </summary>
        /// <param name="petName">The mob's id, as the creator wrote it.</param>
        /// <param name="colours">The gradient maps it can be recoloured into.</param>
        /// <param name="modName">
        /// The mod's own name, so an id written without one still finds the object. Empty is
        /// allowed and means the id is taken exactly as written — see <see cref="Row.QualifiedPetName"/>.
        /// </param>
        public static void Register(
            string petName,
            GradientMapDataBlock[] colours,
            string modName = null)
        {
            if (string.IsNullOrEmpty(petName) || colours == null || colours.Length == 0)
            {
                return;
            }

            Row row = new Row
            {
                PetName = petName,
                QualifiedPetName =
                    string.IsNullOrEmpty(modName) || petName.IndexOf(':') >= 0
                        ? null
                        : modName + ":" + petName,
                Colours = new List<GradientMapDataBlock>(colours.Length)
            };

            for (int i = 0; i < colours.Length; i++)
            {
                if (colours[i] != null)
                {
                    row.Colours.Add(colours[i]);
                }
            }

            if (row.Colours.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                if (string.Equals(Rows[i].PetName, petName, System.StringComparison.Ordinal))
                {
                    Rows[i] = row;
                    answered.Clear();
                    return;
                }
            }

            Rows.Add(row);
            answered.Clear();
        }

        /// <summary>Forgets everything, for a mod being unloaded or an editor reload.</summary>
        public static void Clear()
        {
            Rows.Clear();
            answered.Clear();
        }

        /// <summary>
        /// Reads every pet's colours out of the mod's own shipped template.
        /// </summary>
        /// <remarks>
        /// Called from the mod's <c>ModObjectLoaded</c>, the same moment and for the same reason as
        /// the talent and skill pictures: a <c>GradientMapDataBlock</c> is a Unity object that only
        /// exists once the bundle is loaded, so it cannot be baked into a number.
        /// </remarks>
        public static void AttachFrom(
            DimensionTemplateAsset template,
            System.Action<string> report,
            string modName = null)
        {
            if (template == null)
            {
                return;
            }

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null)
                {
                    continue;
                }

                DimensionPetTemplate pet = mob.Pet;
                if (pet.Colours.Length == 0)
                {
                    continue;
                }

                if (!pet.IsAPet)
                {
                    if (report != null)
                    {
                        report(
                            "'" + mob.DisplayName + "' lists colours to be recoloured into without " +
                            "being a pet, so nothing will ever apply them.");
                    }

                    continue;
                }

                Register(mob.MobId, pet.Colours, modName);
            }
        }

        /// <summary>
        /// What one pet's colours are, or false when this mod says nothing about that pet.
        /// </summary>
        /// <remarks>
        /// The answer is only kept once the pet's name has been answered — see the class remarks.
        /// </remarks>
        internal static bool TryAnswer(
            ObjectID pet,
            System.Func<string, ObjectID> resolvePet,
            out PetInfosTable.PetSkinInfo info)
        {
            info = null;
            if (pet == ObjectID.None)
            {
                return false;
            }

            PetInfosTable.PetSkinInfo cached;
            if (answered.TryGetValue((int)pet, out cached))
            {
                info = cached;
                return true;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                Row row = Rows[i];
                ObjectID resolved = resolvePet == null ? ObjectID.None : resolvePet(row.PetName);
                if (resolved == ObjectID.None &&
                    resolvePet != null &&
                    !string.IsNullOrEmpty(row.QualifiedPetName))
                {
                    // The id as written found nothing, so it is asked again with the mod in front
                    // of it — which is the name the generator actually stamped on the object.
                    resolved = resolvePet(row.QualifiedPetName);
                }

                if (resolved == ObjectID.None || resolved != pet)
                {
                    continue;
                }

                PetInfosTable.PetSkinInfo built = new PetInfosTable.PetSkinInfo
                {
                    petId = resolved,
                    skins = new List<PetInfosTable.PetSkin>(row.Colours.Count)
                };

                for (int c = 0; c < row.Colours.Count; c++)
                {
                    // The game stores a skin as an ADDRESS, not as the object: PetSkin exposes only
                    // gradientMapRef, and its primaryGradientMap property is that ref resolved. A
                    // data block a mod ships is registered with ScriptableData by the loader, so
                    // its own address resolves the same way the game's own do.
                    built.skins.Add(new PetInfosTable.PetSkin
                    {
                        gradientMapRef = new DataBlockRef<GradientMapDataBlock>(
                            row.Colours[c].address)
                    });
                }

                answered[(int)pet] = built;
                info = built;
                return true;
            }

            return false;
        }

        /// <summary>Answers an object name, this mod's own included, once the mod is loaded.</summary>
        internal static ObjectID ResolvePetName(string name)
        {
            return DimensionObjectNames.Resolve(name);
        }
    }

    /// <summary>
    /// Answers what colours a pet comes in, where the game asks the question.
    /// </summary>
    /// <remarks>
    /// One postfix covers every reader: the converter that decides how many skins the pet has, the
    /// pet that applies the chosen one, and the two pieces of interface that tint its icon. See the
    /// registry's remarks for why the table itself is left alone.
    /// </remarks>
    [HarmonyPatch(typeof(PetInfosTable), "GetPetSkinInfo")]
    internal static class DimensionPetSkinPatch
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
        private static void After(ObjectID objectID, ref PetInfosTable.PetSkinInfo __result)
        {
            Fired++;

            // The game's own pets already answer, and a pet with an answer of its own is left with
            // it: this only fills the null the table returns for an object it has never heard of.
            if (__result != null || !DimensionPetSkinRegistry.HasAny)
            {
                return;
            }

            PetInfosTable.PetSkinInfo ours;
            if (DimensionPetSkinRegistry.TryAnswer(
                    objectID,
                    DimensionPetSkinRegistry.ResolvePetName,
                    out ours))
            {
                __result = ours;
            }
        }
    }
}
