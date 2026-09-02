using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which serialized string on each authored asset is the name other things point at, and what
    /// changing it costs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A TABLE RATHER THAN A CONVENTION. The naming looks uniform and is not: an item's is
    /// <c>itemId</c>, a block's is <c>blockName</c>, a world object's is <c>objectIdentifier</c>, a
    /// condition's is <c>conditionName</c>. Worse, six of these assets carry TWO names — a domain id
    /// and an <c>objectId</c> — and which one a pointer means is not uniform either. Guessing the
    /// field by suffix would rename the wrong string on six asset types and say nothing.
    /// </para>
    /// <para>
    /// This table does not decide what a reference MEANS. The sweep that finds inbound references
    /// matches on the value and knows nothing about namespaces (see
    /// <see cref="DimensionIdReferences"/>). All this says is which field is the one being renamed,
    /// which array the asset hangs off, and what the rename would cost — the three things the
    /// creator has to be told before the button is pressed.
    /// </para>
    /// <para>
    /// EDITOR ONLY, and it stays that way. Nothing generated reads it and nothing shipped needs to;
    /// the runtime already resolves names through the object links and the manifest.
    /// </para>
    /// </remarks>
    internal sealed class DimensionIdentityField
    {
        internal DimensionIdentityField(
            Type owner,
            string property,
            string label,
            string thing,
            string explains,
            string containerProperty,
            string generatedFolder,
            string prefabWordWhenBlank,
            string[] prefabSuffixes,
            bool inboundNamesUseIt,
            bool savedWorldsRememberIt,
            string pinProperty,
            bool becomesAGameObject = false,
            Func<string, string>[] derivedIds = null,
            bool deleteIsOfferedHere = true)
        {
            Owner = owner;
            Property = property;
            Label = label;
            Thing = thing;
            Explains = explains;
            ContainerProperty = containerProperty ?? string.Empty;
            GeneratedFolder = generatedFolder ?? string.Empty;
            PrefabWordWhenBlank = prefabWordWhenBlank ?? string.Empty;
            PrefabSuffixes = prefabSuffixes ?? new string[0];
            InboundNamesUseIt = inboundNamesUseIt;
            SavedWorldsRememberIt = savedWorldsRememberIt;
            PinProperty = pinProperty ?? string.Empty;
            BecomesAGameObject = becomesAGameObject;
            DerivedIds = derivedIds ?? new Func<string, string>[0];
            DeleteIsOfferedHere = deleteIsOfferedHere;
        }

        /// <summary>The asset type this name lives on.</summary>
        internal Type Owner { get; }

        /// <summary>The serialized field, e.g. <c>itemId</c>.</summary>
        internal string Property { get; }

        /// <summary>What the row is called on the page.</summary>
        internal string Label { get; }

        /// <summary>The creator's word for this kind of thing: "item", "block", "monster".</summary>
        internal string Thing { get; }

        /// <summary>One sentence saying what the name is for, in the words a creator thinks in.</summary>
        internal string Explains { get; }

        /// <summary>The array on the dimension this asset hangs off, or empty when it hangs off another asset.</summary>
        internal string ContainerProperty { get; }

        /// <summary>The folder under the mod root the generator writes this kind's prefabs into.</summary>
        internal string GeneratedFolder { get; }

        /// <summary>The word the prefab namer falls back to when the id sanitizes away to nothing.</summary>
        internal string PrefabWordWhenBlank { get; }

        /// <summary>
        /// What the generator appends to the sanitized id, one entry per file it writes. An empty
        /// string is the plain prefab; a creature also writes "Visual", "MapMarker", "SummonCircle".
        /// </summary>
        internal string[] PrefabSuffixes { get; }

        /// <summary>
        /// True when other authored content points at this asset by THIS name. False for a second
        /// name on the same asset that only a runtime registry keys off — renaming one of those
        /// changes what the game is told, not what the project says.
        /// </summary>
        internal bool InboundNamesUseIt { get; }

        /// <summary>
        /// True when a saved world writes this name into itself, so a rename reaches worlds people
        /// have already played and no amount of rewriting the project can reach them.
        /// </summary>
        internal bool SavedWorldsRememberIt { get; }

        /// <summary>
        /// The field that pins the old identity so the rename is a label change only, or empty when
        /// the asset has no such pin.
        /// </summary>
        internal string PinProperty { get; }

        /// <summary>
        /// True when this name ends up in the game's object table, so taking one of the game's own
        /// object names for it would silently redirect every reference.
        /// </summary>
        /// <remarks>
        /// False for a recipe, a loot table, a biome, a scene, a dungeon, a generation step, a
        /// portal rule, a game setup, a room filling and a layout. None of those is ever resolved
        /// against <c>ObjectID</c>, so a refusal there would be a block delivered with a reason
        /// that is not true of it.
        /// </remarks>
        internal bool BecomesAGameObject { get; }

        /// <summary>
        /// The SECOND ids a generator makes out of this one — a plant's "EmberSeed", a monster's
        /// "Slime.elite", a block's "EerieStone.wall.block" — each asked of the code that makes it.
        /// </summary>
        /// <remarks>
        /// The sweep matches on the value, so without these a rename reports "nothing else pointed
        /// at it" while every recipe naming "EmberSeed" is left naming a seed nobody builds any
        /// more. Two of these families are ids the framework itself tells a creator are valid to
        /// type. Each entry is the framework's own method for the derived name rather than a copy
        /// of its spelling, so it cannot fall out of step with the generator.
        /// </remarks>
        internal Func<string, string>[] DerivedIds { get; }

        /// <summary>
        /// True when the card is the place this kind is deleted from.
        /// </summary>
        /// <remarks>
        /// False for a block alone. A block owns two generated item assets, and its own page has
        /// had a "Delete Block" button since long before this card that takes them with it; the
        /// card's delete would leave them behind, and two delete buttons on one page with
        /// different consequences is worse than either.
        /// </remarks>
        internal bool DeleteIsOfferedHere { get; }
    }

    /// <summary>Every renameable name in the authoring layer, one row each.</summary>
    internal static class DimensionIdentityCatalog
    {
        // THE FOLDER NAMES ARE THE GENERATORS' OWN CONSTANTS, and now really are. They were
        // literals here, and that is how the plant row below came to name a file the plant
        // generator never writes: nothing tied the two together, so nothing could disagree out
        // loud. The one that stays a literal is "Items", because the item generator composes it
        // inline (DimensionItemGenerator.cs:2652) and has no constant to borrow.
        //
        // The file suffixes are read off the same generators: the creature generator writes four
        // files per creature (:367 the prefab, :1182 "Visual", :1454 "MapMarker", :1516
        // "SummonCircle"), and the plant generator writes SIX (:292, :302, :329, :343, :355, :366)
        // — none of them the bare plant id.
        private static readonly string[] Plain = { string.Empty };

        private static readonly string[] CreatureFiles =
            { string.Empty, "Visual", "MapMarker", "SummonCircle" };

        // A plant's files are named after ids of their own, not after the plant's. The version
        // variants ("<id>Plant<versionKey>") are left out on purpose: the key is worked out from
        // each version's name at generate time, so naming them here would either miss them or
        // claim files that were never written. The four fixed ones are the ones a delete can
        // honestly promise to take away.
        private static readonly string[] PlantFiles =
        {
            DimensionPlantGenerator.PlantSuffix,
            DimensionPlantGenerator.RipeSuffix,
            DimensionPlantGenerator.SeedSuffix,
            DimensionPlantGenerator.PlainSeedSuffix
        };

        // THE SECOND IDS, ASKED OF THE CODE THAT MAKES THEM. Each of these is the framework's own
        // method for the derived name, called rather than copied, so a suffix that moves moves
        // these with it. Six families were measured invisible to a rename before they were here:
        // a plant's seed and grown form, a monster's elite, a block's two block items, a boss's
        // summoning circle and map marker, a dish's two better qualities and an item's golden form.
        // Two of them are ids the framework itself tells a creator are valid to type.
        private static readonly Func<string, string>[] PlantIds =
        {
            id => id + DimensionPlantGenerator.PlantSuffix,
            id => id + DimensionPlantGenerator.SeedSuffix
        };

        private static readonly Func<string, string>[] BossCompanionIds =
        {
            id => id + DimensionGeneratedObjectIds.BossSummonCircleSuffix,
            id => id + DimensionGeneratedObjectIds.BossMapMarkerSuffix
        };

        private static readonly Func<string, string>[] EliteIds =
        {
            DimensionEliteVariantTemplate.IdFor
        };

        private static readonly Func<string, string>[] GoldenIds =
        {
            DimensionCookingTemplate.GoldenItemIdFor
        };

        private static readonly Func<string, string>[] DishIds =
        {
            DimensionDishAsset.RareItemIdFor,
            DimensionDishAsset.EpicItemIdFor
        };

        // A block's two items hang off the identity TOKEN, not off the name as typed, so "Eerie
        // Stone" and "Eerie-Stone" derive the same pair. Only the local half is composed here; the
        // sweep matches on the local half and puts each place's own qualifier back.
        private static readonly Func<string, string>[] BlockItemIds =
        {
            name => DimensionTilesetAsset.IdentityTokenFor(name) + ".ground.block",
            name => DimensionTilesetAsset.IdentityTokenFor(name) + ".wall.block"
        };

        private static readonly DimensionIdentityField[] Rows =
        {
            new DimensionIdentityField(
                typeof(DimensionItemAsset), "itemId", "Its id", "item",
                "What recipes, loot tables, traders and portals type when they mean this item.",
                "globalItems", "Items", "Item", Plain, true, false, null, true, GoldenIds),

            new DimensionIdentityField(
                typeof(DimensionTilesetAsset), "blockName", "Its name", "block",
                "A block's name IS its identity: the number a saved world writes into every tile " +
                "of it is worked out from this.",
                "tilesets", string.Empty, string.Empty, new string[0], true, true, "identityToken",
                false, BlockItemIds, false),

            new DimensionIdentityField(
                typeof(BiomeTemplateAsset), "biomeId", "Its id", "biome",
                "What the map's rings, your dungeons, your creatures and the title card point at. " +
                "A played world stores it against every piece of ground this biome claims.",
                "biomes", string.Empty, string.Empty, new string[0], true, true, null),

            new DimensionIdentityField(
                typeof(SceneTemplateAsset), "sceneId", "Its id", "scene",
                "What a boss arena, a dungeon room or a biome's scene pool types when it means " +
                "this scene.",
                "globalScenes", string.Empty, string.Empty, new string[0], true, false, null),

            // The scene's SECOND name, and it is a different job. Other authored content finds a
            // scene by sceneId; this one is the key the running service files the compiled template
            // under. Renaming it changes what the game is handed, not what the project says, so it
            // rewrites no inbound pointers — which is exactly why it is a row of its own rather
            // than a second guess at the first one.
            new DimensionIdentityField(
                typeof(SceneTemplateAsset), "templateId", "Its name to the game", "scene",
                "The name the running game files this scene's shape under. Nothing you author " +
                "points at it; changing it changes what the game is told, and two scenes sharing " +
                "one of these means the second never registers.",
                "globalScenes", string.Empty, string.Empty, new string[0], false, false, null),

            new DimensionIdentityField(
                typeof(DimensionRecipeAsset), "recipeId", "Its id", "recipe",
                "What a workbench types when it lists this recipe.",
                "globalRecipes", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionWorkbenchAsset), "workbenchId", "Its id", "workbench",
                "What a recipe types when it says where it is made.",
                "globalWorkbenches", DimensionWorkbenchGenerator.FolderName, "Workbench", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionLootTableAsset), "lootTableId", "Its id", "loot table",
                "What a creature, a chest, a scene object or a fishing spot types when it rolls " +
                "on this table.",
                "globalLootTables", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionMobAsset), "mobId", "Its id", "monster",
                "What a drop, a nest, a spawn rule or a portal's drop target types when it means " +
                "this creature.",
                "globalMobs", DimensionCreatureGenerator.FolderName, "Creature", CreatureFiles,
                true, false, null, true, EliteIds),

            new DimensionIdentityField(
                typeof(DimensionBossAsset), "bossId", "Its id", "boss",
                "What a drop, an arena or a summoning item types when it means this boss.",
                "globalBosses", DimensionCreatureGenerator.FolderName, "Creature", CreatureFiles,
                true, false, null, true, BossCompanionIds),

            new DimensionIdentityField(
                typeof(DimensionAnimalAsset), "animalId", "Its id", "animal",
                "What a drop or a spawn rule types when it means this animal.",
                "globalAnimals", DimensionCreatureGenerator.FolderName, "Creature", CreatureFiles,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionCritterAsset), "critterId", "Its id", "critter",
                "What a spawn rule types when it means this critter.",
                "globalCritters", DimensionCritterGenerator.FolderName, "Critter", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionContainerAsset), "containerId", "Its id", "container",
                "What a room filling or a scene types when it puts one of these down.",
                "globalContainers", DimensionContainerGenerator.FolderName, "Container", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionPlantAsset), "plantId", "Its id", "plant",
                "What a crop's seed, its grown form and anything that plants it are all named from.",
                "globalPlants", DimensionPlantGenerator.FolderName, "Plant", PlantFiles,
                true, false, null, true, PlantIds),

            new DimensionIdentityField(
                typeof(DimensionWorldObjectAsset), "objectIdentifier", "Its id", "world object",
                "What a scene, a room filling or a drop types when it places this object.",
                "globalWorldObjects", DimensionWorldObjectGenerator.FolderName, "WorldObject", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionProjectileAsset), "projectileId", "Its id", "projectile",
                "What a weapon types when it says what it fires.",
                "globalProjectiles", DimensionProjectileGenerator.FolderName, "Projectile", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionExplosionAsset), "explosionId", "Its id", "explosion",
                "What a chain charge types when it says what goes off.",
                "globalExplosions", DimensionExplosionGenerator.FolderName, "Explosion", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionVehicleAsset), "vehicleId", "Its id", "vehicle",
                "What anything that puts one of these in the world types when it means it.",
                "globalVehicles", DimensionVehicleGenerator.FolderName, "Vehicle", Plain,
                true, false, null, true),

            new DimensionIdentityField(
                typeof(DimensionDishAsset), "dishId", "Its id", "dish",
                "What a cooking pair types when it says what comes out.",
                "globalDishes", string.Empty, string.Empty, new string[0], true, false, null,
                true, DishIds),

            new DimensionIdentityField(
                typeof(DimensionConditionAsset), "conditionName", "Its name", "stat effect",
                "What an item, a talent, a set bonus or a beam types when it applies this effect.",
                "globalConditions", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionDungeonAsset), "dungeonId", "Its id", "dungeon",
                "What generation and the dungeon's own rooms type when they mean this dungeon.",
                "globalDungeons", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionNamedAreaAsset), "areaId", "Its id", "named area",
                "What generation types when it means this piece of the game it replaces.",
                "namedAreas", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionPortalAccessRuleAsset), "ruleId", "Its id", "portal rule",
                "What the dimension types when it means this way in or out.",
                "portalAccessRules", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(GenerationPassTemplateAsset), "passId", "Its id", "generation step",
                "What a biome types when it lists this step.",
                "globalGenerationPasses", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionGameSetupAsset), "setupIdentifier", "Its id", "game setup",
                "What the dimension types when it means this setup.",
                "globalGameSetups", string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionRoomFillingAsset), "fillingId", "Its id", "room filling",
                "What a dungeon's room group types when it dresses a room with this.",
                string.Empty, string.Empty, string.Empty, new string[0], true, false, null),

            new DimensionIdentityField(
                typeof(DimensionLayoutTemplateAsset), "layoutId", "Its id", "layout",
                "What the dimension types when it means this layout.",
                "layoutTemplate", string.Empty, string.Empty, new string[0], true, false, null)
        };

        /// <summary>Every name this framework knows how to rename, in the order they were written.</summary>
        internal static IReadOnlyList<DimensionIdentityField> All
        {
            get { return Rows; }
        }

        /// <summary>The names on one asset, or an empty list for a type with no row here.</summary>
        internal static List<DimensionIdentityField> Of(Object asset)
        {
            List<DimensionIdentityField> found = new List<DimensionIdentityField>();
            if (asset == null)
            {
                return found;
            }

            Type type = asset.GetType();
            for (int i = 0; i < Rows.Length; i++)
            {
                if (Rows[i].Owner == type)
                {
                    found.Add(Rows[i]);
                }
            }

            return found;
        }

        /// <summary>
        /// The kind of thing a field's NAME reads as — "loot table" for <c>lootTableId</c>, "item"
        /// for <c>outputItemId</c> — or empty when the name says nothing either way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A HINT, AND ONLY EVER USED IN THE SAFE DIRECTION. This is a reading of a field's name,
        /// not a fact about what the generator does with it, so it is never allowed to tick a row
        /// on. It is allowed to leave one OFF: when two things in a pack share a name, a row whose
        /// field reads as the other kind is the one that must not be rewritten by default.
        /// </para>
        /// <para>
        /// Every answer comes from the rows above rather than from a second list, so a field named
        /// after a kind this framework does not have answers nothing rather than answering wrongly.
        /// Two rows disagreeing about one field name is also nothing: an unsure answer here costs a
        /// tick a creator can put back, and a confident wrong one costs the rewrite this whole
        /// change exists to stop.
        /// </para>
        /// </remarks>
        internal static string ReadsAs(string propertyPath)
        {
            string leaf = DimensionIdReferences.LeafOf(propertyPath);
            if (leaf.Length == 0)
            {
                return string.Empty;
            }

            string answer = string.Empty;
            for (int i = 0; i < Rows.Length; i++)
            {
                if (!Mentions(leaf, Rows[i].Property))
                {
                    continue;
                }

                if (answer.Length > 0 &&
                    !string.Equals(answer, Rows[i].Thing, StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                answer = Rows[i].Thing;
            }

            return answer;
        }

        /// <summary>
        /// Whether a field name is that identity's own name, or ends with it — <c>itemId</c>,
        /// <c>outputItemId</c>, <c>soldItemIds</c>.
        /// </summary>
        private static bool Mentions(string leaf, string property)
        {
            if (property.Length == 0)
            {
                return false;
            }

            string plural = property + "s";
            if (string.Equals(leaf, property, StringComparison.Ordinal) ||
                string.Equals(leaf, plural, StringComparison.Ordinal))
            {
                return true;
            }

            // "outputItemId" ends with "ItemId". Capitalised, so "requiredItemId" counts and
            // "spawnsOnBrokenTilesId" does not accidentally read as an id of some kind called
            // "tiles" — the join has to be a word boundary the way the framework writes them.
            string joined = char.ToUpperInvariant(property[0]) + property.Substring(1);
            return leaf.EndsWith(joined, StringComparison.Ordinal) ||
                   leaf.EndsWith(joined + "s", StringComparison.Ordinal);
        }

        /// <summary>
        /// True when this serialized path on this asset is one of its own names, so a sweep that
        /// found the value there found an identity rather than a pointer.
        /// </summary>
        /// <remarks>
        /// A creature carries a domain id and an <c>objectId</c>, and a creator who set both to the
        /// same string gets a sweep hit on the second when renaming the first. That hit is real and
        /// must be shown; what it must not be is ticked by default, because rewriting a second
        /// identity is a second rename nobody asked for.
        /// </remarks>
        internal static bool IsAName(Object asset, string propertyPath)
        {
            if (asset == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            if (string.Equals(propertyPath, "objectId", StringComparison.Ordinal))
            {
                return true;
            }

            List<DimensionIdentityField> names = Of(asset);
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i].Property, propertyPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
