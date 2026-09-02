using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Conditions;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Every player-facing name and line this mod owns, gathered before anything is generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Until this class, only items, bosses, biome titles and named areas were
    /// written into the localization table, so a workbench, a chest, a decoration, a crop's seed,
    /// a mob, an animal or a critter reached the game with no row at all — and Core Keeper renders
    /// a missing term by printing the term itself. The player read "Items/MyMod_stoneBench" in a
    /// tooltip that the editor had promised was "what the player sees".
    /// </para>
    /// <para>
    /// ONE TABLE, ONE WRITER. A mod has a single <c>Localization.csv</c>. Rows are collected here
    /// and handed to <see cref="DimensionItemGenerator"/>, which merges them in the same pass as the
    /// item rows — two writers merging the same file each treat the other's rows as unowned
    /// leftovers, and a row for a deleted object then never gets cleaned up by anyone.
    /// </para>
    /// <para>
    /// WHAT IS DELIBERATELY LEFT OUT, and why each is not an omission:
    /// the growing plant (Core Keeper never names a plant in the ground — its own table has
    /// <c>Items/CarrockSeed</c> and <c>Items/Carrock</c> and no <c>Items/CarrockPlant</c>);
    /// shots and blasts (nothing can hold or hover one); and a crop version's seed and plant, which
    /// share the ordinary seed's object and therefore its name, exactly as the game's own golden
    /// crops do. Vehicles are deliberately NOT on this list — they are placeable, obtainable
    /// objects, so they are named like every other one.
    /// </para>
    /// </remarks>
    internal sealed class DimensionLocalizationPlan
    {
        public readonly List<DimensionLocalizationCsv.Row> Rows =
            new List<DimensionLocalizationCsv.Row>();

        /// <summary>
        /// Keys that are ours and must no longer exist — the colon form of a key the game's lookup
        /// rewrites before searching, and so can never resolve.
        /// </summary>
        public readonly List<string> RetiredKeys = new List<string>();

        /// <summary>Problems for the generate report, in the author's own vocabulary.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>
        /// Gathers every row for one template.
        /// </summary>
        /// <remarks>
        /// Must be called while the mod's conditions are claimed (inside a
        /// <see cref="DimensionConditionScope"/>): a condition's term is keyed by the number it was
        /// handed, and outside the scope no number has been handed out.
        /// </remarks>
        public static DimensionLocalizationPlan Build(
            DimensionTemplateAsset template,
            DimensionNamingContext naming)
        {
            DimensionLocalizationPlan plan = new DimensionLocalizationPlan();
            if (template == null)
            {
                return plan;
            }

            plan.AddWorkbenches(template.GlobalWorkbenches, naming);
            plan.AddContainers(template.GlobalContainers, naming);
            plan.AddWorldObjects(template.GlobalWorldObjects, naming);
            plan.AddPlants(template.GlobalPlants, naming);
            plan.AddMobs(template.GlobalMobs, naming);
            plan.AddAnimals(template.GlobalAnimals, naming);
            plan.AddCritters(template.GlobalCritters, naming);
            plan.AddConditions(template.GlobalConditions);
            plan.AddTalents(template.GlobalGameSetups);
            plan.AddVehicles(template.GlobalVehicles, naming);
            return plan;
        }

        /// <summary>
        /// The words on every talent square this mod invented a name for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A talent that reuses one of the game's own 96 names is left alone: that name already has
        /// wording in every language Core Keeper ships, and it is the whole point of the "change
        /// only the numbers" case. Everything else is a name the game has never heard of, and the
        /// square shows the raw key until a row exists.
        /// </para>
        /// <para>
        /// The rows are gathered from the same grouping the talent files are written from
        /// (<see cref="DimensionTalentsTemplate.Group"/>), so a skill name the game does not have
        /// produces no talent file and no row either — one rule, asked for twice.
        /// </para>
        /// </remarks>
        private void AddTalents(DimensionGameSetupAsset[] setups)
        {
            if (setups == null)
            {
                return;
            }

            HashSet<string> written = new HashSet<string>(System.StringComparer.Ordinal);
            for (int s = 0; s < setups.Length; s++)
            {
                DimensionGameSetupAsset setup = setups[s];

                // The same gate the world-rules generator applies (:123). A switched-off setup
                // writes no talent file, so a line for one of its talents would be a row nothing
                // ever reads.
                if (setup == null || !setup.Enabled || !setup.Talents.ChangesWhatTalentsGive)
                {
                    continue;
                }

                List<DimensionTalentGroup> groups =
                    DimensionTalentsTemplate.Group(setup.Talents.Talents, null);
                for (int g = 0; g < groups.Count; g++)
                {
                    DimensionTalent[] talents = groups[g].Talents;
                    for (int t = 0; t < talents.Length; t++)
                    {
                        DimensionTalent talent = talents[t];
                        if (string.IsNullOrEmpty(talent.TalentName) ||
                            talent.KeepsTheGamesOwnWording ||
                            !written.Add(talent.TalentName))
                        {
                            continue;
                        }

                        DimensionLocalizationCsv.AddTalentRow(
                            Rows, talent.TalentName, talent.ShownAs);
                    }
                }
            }
        }

        private void AddWorkbenches(DimensionWorkbenchAsset[] workbenches, DimensionNamingContext naming)
        {
            if (workbenches == null)
            {
                return;
            }

            for (int i = 0; i < workbenches.Length; i++)
            {
                DimensionWorkbenchAsset workbench = workbenches[i];
                if (workbench == null || !workbench.Enabled ||
                    string.IsNullOrEmpty(workbench.WorkbenchId))
                {
                    continue;
                }

                // A workbench that only groups recipes onto somebody else's object generates no
                // object of ours, so there is nothing of ours for a name to sit on.
                if (!workbench.GeneratesItsOwnObject)
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(workbench.WorkbenchId),
                    workbench.DisplayName,
                    workbench.Description,
                    workbench.WorkbenchId,
                    "crafting station");
            }
        }

        private void AddContainers(DimensionContainerAsset[] containers, DimensionNamingContext naming)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                DimensionContainerAsset container = containers[i];
                if (container == null || !container.Enabled ||
                    string.IsNullOrEmpty(container.ContainerId))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(container.ContainerId),
                    container.DisplayName,
                    container.Description,
                    container.ContainerId,
                    "container");
            }
        }

        private void AddWorldObjects(DimensionWorldObjectAsset[] worldObjects, DimensionNamingContext naming)
        {
            if (worldObjects == null)
            {
                return;
            }

            for (int i = 0; i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null || !worldObject.Enabled ||
                    string.IsNullOrEmpty(worldObject.ObjectIdentifier))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(worldObject.ObjectIdentifier),
                    worldObject.DisplayName,
                    worldObject.Description,
                    worldObject.ObjectIdentifier,
                    "object");
            }
        }

        /// <summary>
        /// The seed, which is the only part of a crop a player ever holds.
        /// </summary>
        /// <remarks>
        /// The harvest is an item of its own and is named with the items; the growing plant is
        /// never named by the game at all; and every better version of the crop sits on the same
        /// seed object at another variation, so it reads under the same name — which is what the
        /// game's own golden crops do, their table carrying one "Carrock Seed" between them.
        /// </remarks>
        private void AddPlants(DimensionPlantAsset[] plants, DimensionNamingContext naming)
        {
            if (plants == null)
            {
                return;
            }

            for (int i = 0; i < plants.Length; i++)
            {
                DimensionPlantAsset plant = plants[i];
                if (plant == null || !plant.Enabled || string.IsNullOrEmpty(plant.PlantId))
                {
                    continue;
                }

                // A plant that spreads on its own is never planted, so no seed object is built.
                if (!plant.IsPlanted)
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(plant.PlantId + DimensionPlantGenerator.SeedSuffix),
                    plant.SeedName,
                    plant.SeedDescription,
                    plant.PlantId + DimensionPlantGenerator.SeedSuffix,
                    "seed");
            }
        }

        private void AddMobs(DimensionMobAsset[] mobs, DimensionNamingContext naming)
        {
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(mob.MobId),
                    mob.DisplayName,
                    mob.Description,
                    mob.MobId,
                    "creature");

                // An elite is a second creature with its own object, so it needs its own rows —
                // without them the harder copy is the one thing in the mod still showing a raw id.
                DimensionEliteVariantTemplate elite = mob.EliteVariant;
                if (elite != null && elite.Enabled)
                {
                    AddObject(
                        naming.QualifyGenerated(DimensionEliteVariantTemplate.IdFor(mob.MobId)),
                        elite.DisplayNameFor(mob.DisplayName),
                        mob.Description,
                        DimensionEliteVariantTemplate.IdFor(mob.MobId),
                        "creature");
                }
            }
        }

        private void AddAnimals(DimensionAnimalAsset[] animals, DimensionNamingContext naming)
        {
            if (animals == null)
            {
                return;
            }

            for (int i = 0; i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null || !animal.Enabled || string.IsNullOrEmpty(animal.AnimalId))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(animal.AnimalId),
                    animal.DisplayName,
                    animal.Description,
                    animal.AnimalId,
                    "animal");
            }
        }

        private void AddCritters(DimensionCritterAsset[] critters, DimensionNamingContext naming)
        {
            if (critters == null)
            {
                return;
            }

            for (int i = 0; i < critters.Length; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null || !critter.Enabled || string.IsNullOrEmpty(critter.CritterId))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(critter.CritterId),
                    critter.DisplayName,
                    critter.Description,
                    critter.CritterId,
                    "critter");
            }
        }

        /// <summary>
        /// The line each custom stat effect shows, under the number the registry handed it.
        /// </summary>
        /// <remarks>
        /// The number is asked of <see cref="DimensionConditionRegistry"/> rather than recomputed,
        /// because it depends on the whole claimed set — the same reason the generate runs inside a
        /// <see cref="DimensionConditionScope"/>. A condition that answers -1 was never claimed, and
        /// writing a row for a number nobody holds would put a line on somebody else's buff.
        /// </remarks>
        private void AddConditions(DimensionConditionAsset[] conditions)
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                DimensionConditionAsset condition = conditions[i];
                if (condition == null || !condition.Enabled ||
                    string.IsNullOrEmpty(condition.ConditionName))
                {
                    continue;
                }

                int number = DimensionConditionRegistry.NumberFor(condition.ConditionName);
                if (number < 0)
                {
                    Warnings.Add(
                        "The condition '" + condition.ConditionName + "' was not claimed a number, " +
                        "so no line could be written for it and it will show as a blank buff. " +
                        "Generate from the dimension window, which claims the mod's conditions " +
                        "before it builds.");
                    continue;
                }

                DimensionLocalizationCsv.AddConditionRow(Rows, number, condition.TooltipLine);
            }
        }

        /// <summary>
        /// The name and tooltip a vehicle shows in a slot and where it stands.
        /// </summary>
        /// <remarks>
        /// A vehicle is <c>ObjectType.PlaceablePrefab</c> with an icon and placement, exactly
        /// as the game's own Boat and Minecart are, so it has the same two places for a name as every
        /// other placeable and is named the same way. Built as an object nobody can pick up or put
        /// down it would have no slot for a name to appear in, and no row could be written at all.
        /// </remarks>
        private void AddVehicles(DimensionVehicleAsset[] vehicles, DimensionNamingContext naming)
        {
            if (vehicles == null)
            {
                return;
            }

            for (int i = 0; i < vehicles.Length; i++)
            {
                DimensionVehicleAsset vehicle = vehicles[i];
                if (vehicle == null || !vehicle.Enabled || string.IsNullOrEmpty(vehicle.VehicleId))
                {
                    continue;
                }

                AddObject(
                    naming.QualifyGenerated(vehicle.VehicleId),
                    vehicle.DisplayName,
                    vehicle.Description,
                    vehicle.VehicleId,
                    "vehicle");
            }
        }

        /// <summary>
        /// The two rows any object the player can see the name of needs, plus the warning for one
        /// that was never named.
        /// </summary>
        /// <param name="fallbackName">
        /// The object's own id, used when nobody typed a name — a readable id in a tooltip is worse
        /// than a real name and better than a blank line, and the warning beside it names the fix.
        /// </param>
        private void AddObject(
            string objectName,
            string displayName,
            string description,
            string fallbackName,
            string whatItIs)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            string name = displayName;
            if (string.IsNullOrEmpty(name))
            {
                name = fallbackName;
                Warnings.Add(
                    "The " + whatItIs + " '" + fallbackName + "' has no name, so the player would " +
                    "read its id in the tooltip. Its id was written as its name; give it one to " +
                    "replace that.");
            }

            DimensionLocalizationCsv.AddItemRows(Rows, objectName, name, description, RetiredKeys);
        }
    }

    /// <summary>
    /// Checks, after a generate, that nothing was built which the player would meet unnamed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY IT READS PREFABS RATHER THAN ASSETS. The failure this catches is a new generator, or a
    /// new second object inside an existing one, that nobody remembered to name — and that is
    /// invisible to any check written against the authoring assets, because the authoring asset for
    /// the thing that went unnamed is exactly what the new code added. Every generator reports the
    /// prefabs it wrote, so walking those is the one list that cannot fall behind the code.
    /// </para>
    /// <para>
    /// The three ways an object legitimately carries no row of ours are each a rule of the game's,
    /// not a convenience: a name with no mod qualifier belongs to Core Keeper and Core Keeper has
    /// already named it (a custom ore vein is the game's own ore); an
    /// <c>ObjectType.NonObtainable</c> object can never be held or hovered, which is the game's own
    /// way of saying it has no tooltip; and a boss is named through <c>Names/</c> instead, which is
    /// where the game reads a boss's name from.
    /// </para>
    /// </remarks>
    internal static class DimensionLocalizationCoverage
    {
        /// <summary>One generated prefab, reduced to what deciding "should this be named" needs.</summary>
        internal readonly struct GeneratedObject
        {
            public GeneratedObject(string prefabPath, string objectName, ObjectType objectType)
            {
                PrefabPath = prefabPath ?? string.Empty;
                ObjectName = objectName ?? string.Empty;
                ObjectType = objectType;
            }

            public string PrefabPath { get; }

            public string ObjectName { get; }

            public ObjectType ObjectType { get; }
        }

        /// <summary>
        /// Reads back the objects a run wrote. Call it only after every generator's asset batch has
        /// closed — <c>AssetDatabase</c> answers with stale contents inside <c>StartAssetEditing</c>.
        /// </summary>
        public static List<GeneratedObject> ReadGeneratedObjects(IEnumerable<string> prefabPaths)
        {
            List<GeneratedObject> objects = new List<GeneratedObject>();
            if (prefabPaths == null)
            {
                return objects;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in prefabPaths)
            {
                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                ObjectAuthoring authoring = prefab.GetComponent<ObjectAuthoring>();
                if (authoring == null)
                {
                    continue;
                }

                objects.Add(new GeneratedObject(path, authoring.objectName, authoring.objectType));
            }

            return objects;
        }

        /// <summary>
        /// Adds one warning per generated object that would meet a player without a name.
        /// </summary>
        public static void Check(
            IEnumerable<GeneratedObject> objects,
            ICollection<string> writtenKeys,
            List<string> warnings)
        {
            if (objects == null || warnings == null)
            {
                return;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            if (writtenKeys != null)
            {
                foreach (string key in writtenKeys)
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            HashSet<string> alreadyReported = new HashSet<string>(StringComparer.Ordinal);
            foreach (GeneratedObject generated in objects)
            {
                if (string.IsNullOrEmpty(generated.ObjectName) ||
                    generated.ObjectType == ObjectType.NonObtainable ||
                    !DimensionObjectNamespace.IsQualified(generated.ObjectName))
                {
                    continue;
                }

                if (keys.Contains(DimensionLocalizationCsv.ItemKeyFor(generated.ObjectName)) ||
                    keys.Contains(DimensionLocalizationCsv.NameKeyFor(generated.ObjectName)))
                {
                    continue;
                }

                // Several prefabs can share one object — a crop's versions do — so the object is
                // what gets reported, not each file that carries it.
                if (!alreadyReported.Add(generated.ObjectName))
                {
                    continue;
                }

                warnings.Add(
                    "'" + generated.ObjectName + "' was generated with no name of its own (" +
                    generated.PrefabPath + "), so a player meeting it reads \"" +
                    DimensionLocalizationCsv.ItemKeyFor(generated.ObjectName) +
                    "\" where its name should be. Give whatever authored it a name, or build it as " +
                    "an object nobody can hold if it was never meant to be seen.");
            }
        }
    }
}
