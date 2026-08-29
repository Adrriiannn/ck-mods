using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Every id a template will turn into a real object, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE POINT IS THAT THERE IS ONE. Deciding "is this name one of ours" is done twice per
    /// generate — once by the generators, when they choose between baking a number and leaving the
    /// name for the runtime, and once by the bootstrap emitter, when it writes the row that turns
    /// the name back into a number. Two independent walks of the same template drifting apart is
    /// the one structural risk in the whole design: the generator defers a reference the emitter
    /// never registers, and the field stays <c>None</c> forever with nothing said about it.
    /// </para>
    /// <para>
    /// So both sides call this, and a test asserts they agree.
    /// </para>
    /// </remarks>
    internal static class DimensionGeneratedObjectIds
    {
        /// <summary>Every local id this template generates an object under.</summary>
        /// <remarks>
        /// SWITCHED-OFF ASSETS ARE NOT HERE. A creator can untick anything in the dashboard, and
        /// every generator skips what is unticked — so a switched-off projectile never becomes an
        /// object and nothing can ever answer to its name. Counting it as ours would make every
        /// reference to it defer quietly, write a row, and leave the game retrying a name that is
        /// never coming. <see cref="SwitchedOff"/> is the same walk over the ones that are off, so
        /// a reference to one can be told apart from a typo and said out loud.
        /// </remarks>
        public static List<string> Collect(DimensionTemplateAsset template)
        {
            return Walk(template, true);
        }

        /// <summary>Every local id this template would generate if it were not switched off.</summary>
        public static List<string> SwitchedOff(DimensionTemplateAsset template)
        {
            return Walk(template, false);
        }

        private static List<string> Walk(DimensionTemplateAsset template, bool wantEnabled)
        {
            List<string> ids = new List<string>();
            if (template == null)
            {
                return ids;
            }

            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                if (items[i] != null && items[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, items[i].ItemId);
                }
            }

            // A tileset's block items become real item assets only at generate time, so their
            // derived ids belong here or every reference to one reads as a name nobody owns.
            //
            // IT READS THE TILESET'S OWN SWITCH, like every other branch. It used to skip the
            // SwitchedOff walk entirely and never look at Enabled, so an unticked tileset's block
            // landed in Collect: naming.Owns said yes, NamesSomethingSwitchedOff could not fire
            // (it needs !Owns), the reference deferred, a row was written, and the game spent the
            // session retrying a name that was never coming. The item generator has always gated
            // block generation on tileset.Enabled, so this is the same question asked in the same
            // place.
            DimensionTilesetAsset[] tilesets = template.Tilesets;
            for (int i = 0; tilesets != null && i < tilesets.Length; i++)
            {
                if (tilesets[i] == null || !tilesets[i].GenerateBlock ||
                    tilesets[i].Enabled != wantEnabled)
                {
                    continue;
                }

                Add(ids, seen, tilesets[i].GroundBlockItemId);
                Add(ids, seen, tilesets[i].WallBlockItemId);
            }

            DimensionWorkbenchAsset[] workbenches = template.GlobalWorkbenches;
            for (int i = 0; workbenches != null && i < workbenches.Length; i++)
            {
                if (workbenches[i] != null && workbenches[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, workbenches[i].WorkbenchId);
                    Add(ids, seen, workbenches[i].ObjectId);
                }
            }

            DimensionContainerAsset[] containers = template.GlobalContainers;
            for (int i = 0; containers != null && i < containers.Length; i++)
            {
                if (containers[i] != null && containers[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, containers[i].ContainerId);
                }
            }

            DimensionWorldObjectAsset[] worldObjects = template.GlobalWorldObjects;
            for (int i = 0; worldObjects != null && i < worldObjects.Length; i++)
            {
                if (worldObjects[i] != null && worldObjects[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, worldObjects[i].ObjectIdentifier);
                }
            }

            DimensionVehicleAsset[] vehicles = template.GlobalVehicles;
            for (int i = 0; vehicles != null && i < vehicles.Length; i++)
            {
                if (vehicles[i] != null && vehicles[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, vehicles[i].VehicleId);
                }
            }

            DimensionProjectileAsset[] projectiles = template.GlobalProjectiles;
            for (int i = 0; projectiles != null && i < projectiles.Length; i++)
            {
                if (projectiles[i] != null && projectiles[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, projectiles[i].ProjectileId);
                }
            }

            DimensionExplosionAsset[] explosions = template.GlobalExplosions;
            for (int i = 0; explosions != null && i < explosions.Length; i++)
            {
                if (explosions[i] != null && explosions[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, explosions[i].ExplosionId);
                }
            }

            DimensionPlantAsset[] plants = template.GlobalPlants;
            for (int i = 0; plants != null && i < plants.Length; i++)
            {
                if (plants[i] != null && plants[i].Enabled == wantEnabled)
                {
                    Add(ids, seen, plants[i].PlantId);
                }
            }

            AddCreatures(ids, seen, template.GlobalMobs, wantEnabled);
            AddCreatures(ids, seen, template.GlobalAnimals, wantEnabled);
            AddCreatures(ids, seen, template.GlobalCritters, wantEnabled);
            AddCreatures(ids, seen, template.GlobalBosses, wantEnabled);

            // An elite is a second generated creature under its own id, and it was in nobody's
            // list. So a scene placing one, or an item dropping from one, read as a typo, and the
            // elite's own shot and shed had no row and stayed None forever with nothing said. The
            // dashboard's drops check already treats elite ids as real; this is the same walk.
            DimensionMobAsset[] eliteOwners = template.GlobalMobs;
            for (int i = 0; eliteOwners != null && i < eliteOwners.Length; i++)
            {
                DimensionMobAsset mob = eliteOwners[i];
                if (mob == null || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                DimensionEliteVariantTemplate elite = mob.EliteVariant;
                bool eliteIsOn = elite != null && elite.Enabled && mob.Enabled;
                if (eliteIsOn == wantEnabled)
                {
                    // An unticked elite lands in the switched-off walk rather than nowhere, so a
                    // reference to it gets "one of yours but switched off" instead of the sentence
                    // for a misspelling.
                    Add(ids, seen, DimensionEliteVariantTemplate.IdFor(mob.MobId));
                }
            }

            return ids;
        }

        private static void AddCreatures<T>(
            List<string> ids, HashSet<string> seen, T[] creatures, bool wantEnabled)
            where T : UnityEngine.Object
        {
            for (int i = 0; creatures != null && i < creatures.Length; i++)
            {
                T creature = creatures[i];
                if (creature == null)
                {
                    continue;
                }

                // The four creature classes share the field but not a base type carrying it, so it
                // is read by name — the same way DimensionContentIdUniverse reads it. The same is
                // true of the switch that turns one off.
                System.Reflection.PropertyInfo enabled = creature.GetType().GetProperty(
                    "Enabled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (enabled != null &&
                    !object.Equals(enabled.GetValue(creature, null), wantEnabled))
                {
                    continue;
                }

                // The id the creature is GENERATED under, which is the one anything naming it
                // will type. Each of the four carries its own — mobId, bossId, animalId, critterId
                // — and the generator qualifies that one (DimensionCreatureGenerator.cs:483). They
                // also carry a separate "objectId", seeded to a different string entirely when the
                // asset is created ("mod:mob1" against "mod:mob1Object"), so reading that one made
                // every reference to this mod's own creatures look like a reference to nothing.
                Add(ids, seen, CreatureIdOf(creature, "mobId"));
                Add(ids, seen, CreatureIdOf(creature, "bossId"));
                Add(ids, seen, CreatureIdOf(creature, "animalId"));
                Add(ids, seen, CreatureIdOf(creature, "critterId"));
            }
        }

        /// <summary>
        /// One creature asset's own id field, when it has that one.
        /// </summary>
        /// <remarks>
        /// Reflection because the four creature assets share no base type, and a name that does not
        /// exist on this one simply answers nothing.
        /// </remarks>
        private static string CreatureIdOf(UnityEngine.Object creature, string fieldName)
        {
            System.Reflection.FieldInfo field = creature.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return field == null ? null : field.GetValue(creature) as string;
        }

        private static void Add(List<string> ids, HashSet<string> seen, string id)
        {
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
            {
                ids.Add(id);
            }
        }
    }

    /// <summary>
    /// Decides, for one authored reference, whether the generator bakes a number or leaves the
    /// name for the game to resolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THREE ANSWERS, NOT TWO, and that is the whole reason this exists rather than a bare
    /// <c>Func&lt;string, ObjectID&gt;</c>:
    /// </para>
    /// <para>
    /// <b>One of the GAME's objects</b> — the number is known here and now, so it is baked and
    /// nothing else happens.
    /// </para>
    /// <para>
    /// <b>One of the MOD's own</b> — there is no number yet; a mod's object ids are handed out
    /// while the game loads. <c>None</c> is baked, the authoring component is <em>kept</em>, and the
    /// bootstrap emits a row that fills the field in at load. The generator must not report this as
    /// a mistake and must not strip anything: a stripped component is a field the hydration system
    /// can never write into, and it reports an unfixable problem on a perfectly good item.
    /// </para>
    /// <para>
    /// <b>Neither</b> — a typo, or a name from a mod that is not installed. <c>None</c> is baked and
    /// the caller reports exactly the sentence it reports today. That sentence is the only thing
    /// standing between a creator and an item that silently does nothing.
    /// </para>
    /// </remarks>
    internal sealed class DimensionObjectBinder
    {
        private readonly DimensionNamingContext naming;
        private readonly HashSet<string> switchedOff;

        /// <summary>
        /// A binder that takes its switched-off set from the naming context it is given.
        /// </summary>
        /// <remarks>
        /// This used to pass null, so every generator's binder could only tell "ours" from "not a
        /// name at all" — and a reference to the mod's own unticked projectile got the sentence for
        /// a misspelling. The context carries the set now, so the generators and the bootstrap
        /// emitter say the same thing about the same reference.
        /// </remarks>
        public DimensionObjectBinder(DimensionNamingContext naming)
            : this(naming, naming.SwitchedOffIds)
        {
        }

        /// <summary>
        /// A binder that can also tell a reference to a switched-off asset from a misspelling.
        /// </summary>
        /// <remarks>
        /// A creator unticks a projectile to take it out of the build for a while and leaves the
        /// bow pointing at it. Without this the reference reads as a name nobody owns, which is the
        /// sentence for a typo and sends them hunting for a spelling mistake that is not there.
        /// </remarks>
        public DimensionObjectBinder(
            DimensionNamingContext naming,
            IEnumerable<string> switchedOffIds)
        {
            this.naming = naming;
            switchedOff = new HashSet<string>(System.StringComparer.Ordinal);
            if (switchedOffIds == null)
            {
                return;
            }

            foreach (string id in switchedOffIds)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    switchedOff.Add(DimensionObjectNamespace.LocalIdOf(id));
                }
            }
        }

        /// <summary>
        /// True when the name belongs to one of this mod's own assets that is switched off, so no
        /// object will ever answer to it.
        /// </summary>
        public bool NamesSomethingSwitchedOff(string targetId)
        {
            return !string.IsNullOrEmpty(targetId) &&
                Vanilla(targetId) == ObjectID.None &&
                !naming.Owns(targetId) &&
                switchedOff.Contains(DimensionObjectNamespace.LocalIdOf(targetId));
        }

        /// <summary>The sentence for a reference to a switched-off asset, or null when it is not one.</summary>
        public string ExplainIfSwitchedOff(string what, string targetId)
        {
            if (!NamesSomethingSwitchedOff(targetId))
            {
                return null;
            }

            return what + " '" + targetId + "', which is one of yours but is switched off, so " +
                "nothing in the game answers to it. Tick it back on, or point at something else, " +
                "and generate again.";
        }

        /// <summary>The naming context this binder decides ownership with.</summary>
        public DimensionNamingContext Naming
        {
            get { return naming; }
        }

        /// <summary>
        /// True when the reference is FINE — baked as a vanilla number, or left for the runtime.
        /// False only when the name matches nothing at all.
        /// </summary>
        public bool TryBind(string targetId, out ObjectID baked)
        {
            baked = Vanilla(targetId);
            if (baked != ObjectID.None)
            {
                return true;
            }

            if (string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            return naming.Owns(targetId);
        }

        /// <summary>
        /// True when this name has no number yet and one of this mod's own objects answers to it.
        /// </summary>
        /// <remarks>
        /// This is the predicate the generators hand to the spine so it keeps the authoring
        /// component alive, and the one the bootstrap emitter uses to decide which rows to write.
        /// One predicate, so the two can never disagree about which references were deferred.
        /// </remarks>
        public bool IsDeferred(string targetId)
        {
            return Vanilla(targetId) == ObjectID.None &&
                !string.IsNullOrEmpty(targetId) &&
                naming.Owns(targetId);
        }

        /// <summary>The qualified name a deferred reference is registered under.</summary>
        public string Qualify(string targetId)
        {
            return naming.QualifyReference(targetId);
        }

        /// <summary>
        /// A drop-in for the <c>Func&lt;string, ObjectID&gt;</c> the generators already pass around.
        /// </summary>
        /// <remarks>
        /// Answers exactly what the generators' own helpers answered — the game's number for one of
        /// the game's names, <c>None</c> otherwise. What changed is not this function, it is that
        /// the caller now also gets to ask <see cref="IsDeferred"/> about the <c>None</c>.
        /// </remarks>
        public System.Func<string, ObjectID> Resolver
        {
            get { return Vanilla; }
        }

        /// <summary>The predicate the spine's keep-the-component branches read.</summary>
        public System.Func<string, bool> DeferredPredicate
        {
            get { return IsDeferred; }
        }

        /// <summary>
        /// The game's own number for one of the game's own names, and nothing else.
        /// </summary>
        /// <remarks>
        /// Parsed off the enum rather than looked up through <c>API.Authoring.GetObjectID</c>,
        /// because this runs at GENERATION time and that lookup is a runtime dictionary — empty in
        /// the editor, where it answers <c>None</c> for every one of the game's own names.
        /// <c>!= None</c> is checked because <c>Enum.TryParse("None")</c> succeeds.
        /// </remarks>
        public static ObjectID Vanilla(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return ObjectID.None;
            }

            ObjectID parsed;
            return System.Enum.TryParse(targetId, false, out parsed) && parsed != ObjectID.None
                ? parsed
                : ObjectID.None;
        }

        /// <summary>
        /// Whether a name could only ever be resolved once the game is running — no ownership
        /// knowledge involved.
        /// </summary>
        /// <remarks>
        /// Used where a template is not in reach, notably the drop router: a drop whose item name is
        /// not one of the game's cannot be written onto the prefab as custom loot at all, whether or
        /// not this particular template owns it.
        /// </remarks>
        public static bool CouldOnlyResolveAtRuntime(string targetId)
        {
            return !string.IsNullOrEmpty(targetId) && Vanilla(targetId) == ObjectID.None;
        }
    }
}
