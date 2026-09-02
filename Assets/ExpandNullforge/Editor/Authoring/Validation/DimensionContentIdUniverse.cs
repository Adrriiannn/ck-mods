using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Every id the pack defines and every vanilla name it may legally borrow, built once per
    /// validation pass so each reference check is one set lookup.
    /// </summary>
    public sealed class DimensionContentIdUniverse
    {
        private static HashSet<string> vanillaObjects;
        private static HashSet<string> vanillaLootTables;
        private static HashSet<string> vanillaBiomes;

        private readonly HashSet<string> itemIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> lootTableIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> creatureIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> objectIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> sceneIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> biomeIds = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly List<SceneTemplateAsset> allScenes = new List<SceneTemplateAsset>();

        public readonly HashSet<string> AuthoredWorkbenchIds =
            new HashSet<string>(System.StringComparer.Ordinal);

        public readonly HashSet<string> AuthoredPassIds =
            new HashSet<string>(System.StringComparer.Ordinal);

        public readonly Dictionary<string, List<string>> DefinitionsById =
            new Dictionary<string, List<string>>(System.StringComparer.Ordinal);

        public IReadOnlyList<SceneTemplateAsset> AllScenes
        {
            get { return allScenes; }
        }

        public static DimensionContentIdUniverse Build(DimensionTemplateAsset template)
        {
            EnsureVanillaSets();
            DimensionContentIdUniverse universe = new DimensionContentIdUniverse();

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    universe.Define(universe.itemIds, items[i].ItemId, "item");
                }
            }

            // Tileset block items become real item assets only at generate time, so their
            // derived ids count as items here or every block reference reads as dangling.
            DimensionTilesetAsset[] tilesets = template.Tilesets;
            for (int i = 0; i < tilesets.Length; i++)
            {
                if (tilesets[i] == null || !tilesets[i].GenerateBlock)
                {
                    continue;
                }

                universe.itemIds.Add(tilesets[i].GroundBlockItemId);
                universe.itemIds.Add(tilesets[i].WallBlockItemId);
            }

            DimensionRecipeAsset[] recipes = template.GlobalRecipes;
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] != null)
                {
                    universe.Define(null, recipes[i].RecipeId, "recipe");
                }
            }

            DimensionWorkbenchAsset[] workbenches = template.GlobalWorkbenches;
            for (int i = 0; i < workbenches.Length; i++)
            {
                if (workbenches[i] == null)
                {
                    continue;
                }

                universe.Define(universe.AuthoredWorkbenchIds, workbenches[i].WorkbenchId, "workbench");
                if (!string.IsNullOrEmpty(workbenches[i].ObjectId))
                {
                    universe.AuthoredWorkbenchIds.Add(workbenches[i].ObjectId);
                    universe.objectIds.Add(workbenches[i].ObjectId);
                }
            }

            DimensionLootTableAsset[] lootTables = template.GlobalLootTables;
            for (int i = 0; i < lootTables.Length; i++)
            {
                if (lootTables[i] != null)
                {
                    universe.Define(universe.lootTableIds, lootTables[i].LootTableId, "loot table");
                }
            }

            AddCreatures(universe, template.GlobalMobs);
            AddCreatures(universe, template.GlobalAnimals);
            AddCreatures(universe, template.GlobalCritters);
            AddCreatures(universe, template.GlobalBosses);

            DimensionContainerAsset[] containers = template.GlobalContainers;
            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i] != null)
                {
                    universe.Define(universe.objectIds, containers[i].ContainerId, "chest");
                }
            }

            DimensionWorldObjectAsset[] worldObjects = template.GlobalWorldObjects;
            for (int i = 0; i < worldObjects.Length; i++)
            {
                if (worldObjects[i] != null)
                {
                    universe.Define(universe.objectIds, worldObjects[i].ObjectIdentifier, "object");
                }
            }

            SceneTemplateAsset[] scenes = template.GlobalScenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null)
                {
                    universe.Define(universe.sceneIds, scenes[i].SceneId, "place");
                    universe.allScenes.Add(scenes[i]);
                }
            }

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] == null)
                {
                    continue;
                }

                universe.Define(universe.biomeIds, biomes[i].BiomeId, "biome");
                SceneTemplateAsset[] pool = biomes[i].ScenePool;
                for (int j = 0; j < pool.Length; j++)
                {
                    if (pool[j] != null && universe.sceneIds.Add(pool[j].SceneId))
                    {
                        universe.allScenes.Add(pool[j]);
                    }
                }
            }

            GenerationPassTemplateAsset[] passes = template.GlobalGenerationPasses;
            for (int i = 0; i < passes.Length; i++)
            {
                if (passes[i] != null)
                {
                    universe.AuthoredPassIds.Add(passes[i].PassId);
                }
            }

            return universe;
        }

        /// <summary>
        /// Collects the object id off every creature in one of the four creature arrays.
        /// </summary>
        /// <remarks>
        /// Reading the serialized <c>objectId</c> field by name is the obvious way, because the four
        /// creature classes share the field without sharing a base type that carries it. The mod
        /// sandbox denies <c>System.Reflection</c> outright, and the field is not the only way in: all
        /// four already expose <c>ObjectId</c>, so <see cref="IDimensionCreatureAsset"/> names that
        /// and the read is an ordinary property call. Same value, same creatures, one constraint
        /// instead of a lookup that could miss silently.
        /// </remarks>
        private static void AddCreatures<T>(DimensionContentIdUniverse universe, T[] creatures)
            where T : Object, IDimensionCreatureAsset
        {
            for (int i = 0; i < creatures.Length; i++)
            {
                T creature = creatures[i];
                if (creature == null)
                {
                    continue;
                }

                string objectId = creature.ObjectId;
                if (!string.IsNullOrEmpty(objectId))
                {
                    universe.Define(universe.creatureIds, objectId, "creature");
                }
            }
        }

        private void Define(HashSet<string> kindSet, string id, string kind)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            kindSet?.Add(id);
            if (!DefinitionsById.TryGetValue(id, out List<string> owners))
            {
                owners = new List<string>();
                DefinitionsById[id] = owners;
            }

            owners.Add(kind);
        }

        private static void EnsureVanillaSets()
        {
            if (vanillaObjects != null)
            {
                return;
            }

            vanillaObjects = new HashSet<string>(
                System.Enum.GetNames(typeof(ObjectID)),
                System.StringComparer.Ordinal);
            vanillaObjects.Remove("None");
            vanillaLootTables = new HashSet<string>(
                System.Enum.GetNames(typeof(LootTableID)),
                System.StringComparer.Ordinal);
            vanillaBiomes = new HashSet<string>(
                System.Enum.GetNames(typeof(Biome)),
                System.StringComparer.Ordinal);
        }

        public bool IsVanillaObject(string id)
        {
            return !string.IsNullOrEmpty(id) && vanillaObjects.Contains(id);
        }

        /// <summary>
        /// True when this name belongs to something the mod itself makes, rather than to one of the
        /// game's objects.
        /// </summary>
        /// <remarks>
        /// The mod's own list is asked FIRST, and it has to be: a creator may name an item exactly
        /// as the game names one of its own, and in that case the generator qualifies the name and
        /// ships the mod's object. The emitter that registers recipes decides ownership the same
        /// way, so asking "is it vanilla" here instead would let a recipe validate clean and then
        /// be registered against a different object.
        /// </remarks>
        public bool IsOneOfYourOwn(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (itemIds.Contains(id) ||
                    AuthoredWorkbenchIds.Contains(id) ||
                    creatureIds.Contains(id));
        }

        public bool ResolvesAsItem(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (itemIds.Contains(id) || vanillaObjects.Contains(id));
        }

        public bool ResolvesAsLootTable(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (lootTableIds.Contains(id) || vanillaLootTables.Contains(id));
        }

        public bool ResolvesAsBiome(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (biomeIds.Contains(id) || vanillaBiomes.Contains(id));
        }

        public bool ResolvesAsObjectOrItem(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   (objectIds.Contains(id) || itemIds.Contains(id) ||
                    vanillaObjects.Contains(id));
        }

        public bool ResolvesDropSource(DimensionDropSourceKind kind, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            switch (kind)
            {
                case DimensionDropSourceKind.Creature:
                    return creatureIds.Contains(id) || vanillaObjects.Contains(id);
                default:
                    return objectIds.Contains(id) || itemIds.Contains(id) ||
                           sceneIds.Contains(id) || vanillaObjects.Contains(id);
            }
        }

        public bool LootTableReferencedByCreature(
            DimensionTemplateAsset template,
            DimensionLootTableAsset lootTable)
        {
            // Critters are not asked. They have no loot table field at all, so the field lookup this
            // replaced always came back null for them and the answer was always no.
            return CreatureReferences(template.GlobalMobs, lootTable) ||
                   CreatureReferences(template.GlobalAnimals, lootTable) ||
                   CreatureReferences(template.GlobalBosses, lootTable);
        }

        /// <summary>
        /// Whether any creature in the array drops this table.
        /// </summary>
        /// <remarks>
        /// Reached the serialized <c>lootTable</c> field by name until the mod sandbox ruled out
        /// <c>System.Reflection</c>. Mobs, animals and bosses already expose <c>LootTable</c>, which
        /// <see cref="IDimensionLootBearingAsset"/> names, so the comparison is the same reference
        /// check against the same value.
        /// </remarks>
        private static bool CreatureReferences<T>(T[] creatures, DimensionLootTableAsset lootTable)
            where T : Object, IDimensionLootBearingAsset
        {
            for (int i = 0; i < creatures.Length; i++)
            {
                if (creatures[i] == null)
                {
                    continue;
                }

                if (ReferenceEquals(creatures[i].LootTable, lootTable))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
