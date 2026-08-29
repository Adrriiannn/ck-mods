using System;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Honest maturity level of a framework capability. The vocabulary matches the Dimensions
    /// API handoff so the dashboard, docs, and consumers describe features the same way and
    /// never present an unfinished capability as production-ready.
    /// </summary>
    public enum DimensionCapabilityMaturity
    {
        /// <summary>The full path exists and has current build/runtime evidence.</summary>
        ImplementedAndEvidenced,

        /// <summary>
        /// Substantial code exists, but one or more clean-build, runtime, multiplayer,
        /// persistence, or performance proofs are still missing.
        /// </summary>
        ImplementedNotFullyProven,

        /// <summary>A deliberately narrow path works; broader API claims do not yet follow.</summary>
        PartialVerticalSlice,

        /// <summary>
        /// Consumers can register a provider/definition, but the framework does not itself
        /// supply the content behavior.
        /// </summary>
        ContractExtensionSeam,

        /// <summary>
        /// Assets, DTOs, dashboard models, previews, or contracts exist without a complete
        /// runtime realization.
        /// </summary>
        AuthoringModelOnly,

        /// <summary>Known regressions, unfinished work, or no reliable test gate.</summary>
        ExperimentalUnstable,

        /// <summary>No meaningful framework path exists beyond perhaps a name.</summary>
        NotImplemented
    }

    /// <summary>An immutable maturity record for one framework capability.</summary>
    public readonly struct DimensionCapability
    {
        public DimensionCapability(
            string id,
            string title,
            DimensionCapabilityMaturity maturity,
            string note)
        {
            Id = id;
            Title = title;
            Maturity = maturity;
            Note = note;
        }

        /// <summary>Stable identifier (kebab-case) for lookups and UI keys.</summary>
        public string Id { get; }

        /// <summary>Human-readable capability name.</summary>
        public string Title { get; }

        public DimensionCapabilityMaturity Maturity { get; }

        /// <summary>Short note on what is proven and what is still missing.</summary>
        public string Note { get; }

        /// <summary>
        /// True when the capability is proven enough to depend on for a limited alpha. Only
        /// evidenced capabilities qualify; everything else is preview/experimental.
        /// </summary>
        public bool IsAlphaReady =>
            Maturity == DimensionCapabilityMaturity.ImplementedAndEvidenced;
    }

    /// <summary>
    /// Single source of truth for how mature each Dimensions API capability actually is.
    /// The dashboard and documentation should read maturity from here rather than implying
    /// every exposed interface is finished. Values are intentionally conservative: a
    /// capability is only raised once real build/runtime/multiplayer/persistence evidence
    /// exists (see the handoff's definition of done).
    /// </summary>
    public static class DimensionCapabilityRegistry
    {
        private static readonly DimensionCapability[] Capabilities =
        {
            new DimensionCapability(
                "api-service-registration",
                "Shared API and service registration",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "API v1 and one service provider seam exist; lifecycle/duplicate-provider and compatibility tests are still needed."),
            new DimensionCapability(
                "dimension-identity-registry",
                "Dimension identity and registry",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Definitions, lookup, lifecycle, and diagnostics exist; not proven across load order, migration, world switching, and many mods."),
            new DimensionCapability(
                "coordinate-translation",
                "Coordinate translation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Absolute/local conversion and bounds exist; complete UI presentation isolation is unproven."),
            new DimensionCapability(
                "slot-allocation",
                "Cross-mod slot allocation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Deterministic candidates, conflict checks, and persisted slots exist; concurrency/load-order/migration are untested."),
            new DimensionCapability(
                "persistence",
                "Per-world persistence",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A/B records, checksums, backups, coalesced writes exist; corruption, migration, server, and world-switch coverage is missing."),
            new DimensionCapability(
                "loaded-area-orchestration",
                "Loaded-area orchestration",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Real loaded-area components, tickets, merging, and quotas work; a scalable general scheduler is not proven."),
            new DimensionCapability(
                "generation",
                "Generation orchestration",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Every plan is synthesized (terrain, then dungeons, then scenes, then ore) and five framework providers run the ladder; authored passes slot in by phase. " +
                "The generated terrain is no longer always dirt: each biome's first Ground and first Walls block is registered by the bootstrap, bound to that biome's zones as they register, and read per cell by the safe-platform provider — a cell no biome covers still gets dirt, which is what every cell got before. " +
                "A dimension can also be painted tile by tile: the Map page's Paint tab writes onto the layout asset and export copies that map into the runtime manifest, at which point the painted-map provider generates it and the other two providers stand aside. Capped at 256 x 256 tiles by the snapshot format. " +
                "In-game evidence pending."),
            new DimensionCapability(
                "travel-networking",
                "Travel and networking",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Server-side queued vanilla teleport path and state records exist; entry/return/reconnect/death/multiplayer are unproven."),
            new DimensionCapability(
                "portals",
                "Portals",
                DimensionCapabilityMaturity.ExperimentalUnstable,
                "Extensive runtime/editor implementation; recently hardened (swirl, positioning, parity trace) but not yet cleared in a built game."),
            new DimensionCapability(
                "portal-studio",
                "Portal Studio",
                DimensionCapabilityMaturity.ExperimentalUnstable,
                "Rich preview, profiles, layers, packages; profile-owned baking and a parity validator now exist but need in-game golden-capture proof."),
            new DimensionCapability(
                "custom-tilesets",
                "Custom tilesets",
                DimensionCapabilityMaturity.ExperimentalUnstable,
                "Hash-identity registry plus rendering/placement/map-color patches and block generation exist; not yet cleared in a built game. " +
                "Ground Cover (grass, pebbles, roots, slime) now reaches the world: the authored layers are mirrored into primitive arrays at Generate, which is the shape that survives the mod runtime's recompile, and both the painted and the generated terrain paths scatter from them. Grass and pebbles are erased by any later tile write at their cell — scene, dungeon and ore passes all legitimately clear them under their footprints — while roots and slime are not. A reskin block scatters nothing, because its tiles carry the game's own tileset id. " +
                "Known limit: enemies treat a custom wall as one they cannot mine — they route around it and tunnel only when there is no other path. " +
                "Pathfinding reads wall durability from a fixed 75-entry table inside a Burst job, so custom ids fall to the same cost vanilla uses for an over-armoured wall."),
            new DimensionCapability(
                "manifest-ownership",
                "Content manifest and ownership",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Comprehensive definition/ownership/reference snapshot; transactional registration and schema migration tooling are missing."),
            new DimensionCapability(
                "dashboard-wizard",
                "Dashboard and authoring wizard",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "The eleven-studio journey, the collection pages and the field catalog are real, and the old warning that 'several fields have no runtime executor' no longer holds: every one of the 210 field rows in DimensionStageCatalog resolves to a real serialized field on the asset its page edits, checked mechanically. What keeps this a partial slice is the other half — the DimensionTemplateCustomizer/CreationWizard model is far larger than the window built on it, with roughly 190 public properties across its plan, preview, rollback and pipeline types that no UI reads. Those are unreached editor model surface, not broken authoring: nothing a creator can type into is affected. Trim or wire them before treating this as finished."),
            // Raised from AuthoringModelOnly. That rating predated the biome work and had gone
            // stale in the direction that matters least but still misleads: the Biome Studio page
            // wears this row's badge, so a creator was being told "preview, authoring only" about
            // fields that do reach the game.
            new DimensionCapability(
                "biomes-zones",
                "Biomes",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A biome carries a name, a map colour, a discovery title card, its own ambience and its own music, and the signature blocks that make an area identifiably its. The floor and wall lists resolve to this framework's tileset ids (CollectBiomeTilesetIds), and the ore list gates ore-vein generation through DimensionOreBiomeGate.RegisterBiomeOres, so both reach generation rather than sitting in a record. The FIRST block in each of those two lists is now also what the world builds the biome's ground and walls out of (DimensionTerrainMaterialRegistry), picked from a menu of the mod's own blocks and forty of the game's; the rest of each list keeps its existing job of saying what the biome is made of. Both gates are bound where zones are registered, so a dimension that never applies a content manifest gets them too — it previously did not. Honest limits: Core Keeper biomes are radial and cannot be given an arbitrary shape at the biome layer (shape belongs to the tile layer), and five fields carried on the biome asset — environment profile, palette, spawn table, resource table and world-event table — feed only the framework's own definition record and have NO game effect. They are deliberately not offered in the Biome Studio; the world-event one names a module that was deleted. In-game evidence pending."),
            // Retitled and rewritten rather than retired: the id is stable and referenced as a
            // string, but three of the four things the old title named now have rows of their own,
            // and the fourth (world events) was DELETED as a dead module — registry, system and all
            // — so a row claiming an authoring model for it was claiming a model for nothing.
            new DimensionCapability(
                "scenes-resources-spawns-events",
                "Scene, spawn and resource definitions (umbrella)",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Umbrella record kept for id stability. The real, separately-evidenced paths are scene-placement (authored placement policy into a runtime pass), custom-creatures (the per-creature spawn chain into DimensionCreatureSpawnRegistry) and ore-veins (per-biome ore gating). World events are NOT part of this or anything else: DimensionWorldEventRegistry and DimensionWorldEventSystem were deleted after an audit found the registry had zero producers and the system early-outed forever. Read the three specific rows rather than this one."),
            new DimensionCapability(
                "custom-items",
                "Custom items",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Archetypes drive prefab generation (ObjectAuthoring, inventory, placement, damage, cooldown, health, loot, recipe ingredients), names and tooltips are merged into the mod's localization table, and the runtime declares generated ids so unregistered items can be named. The validator blocks incomplete items and the generator suite runs green in Unity. Known SDK constraint: DurabilityAuthoring derives its value in-game, so an authored durability value is not baked (the creator is warned). SpriteAsset generation and ScriptableData registration are still missing, and no item has yet been confirmed in a running game. Crafting-station injection graduated — see recipes-workbenches-loot."),
            new DimensionCapability(
                "recipes-workbenches-loot",
                "Recipes, workbenches, and loot",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "An authored recipe reaches a station three ways, all through one registry and the object-type-added injector: one of the game's benches by ObjectID, one of the mod's own Workbenches by qualified object name (resolved while the world converts, since a mod object has no number before that), and by hand — which in Core Keeper means ObjectID.Player, the player being a crafting station carrying six recipes of its own. What a craft COSTS is not part of this: Core Keeper stores it on the produced item, so the item generator stamps it and a recipe whose output is one of the game's items is refused rather than registered at a price nobody wrote. Known limits: the by-hand list holds twelve on top of the game's six and cannot page, and offering a recipe at a Workbench that generates no object is refused. Loot tables themselves graduated — see loot-tables. In-game evidence pending."),
            new DimensionCapability(
                "loot-tables",
                "Custom loot tables",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Authored tables become real game tables: ids minted from the name (FNV, floor 1000, vanilla enum tops out at 723), appended to Manager.mod.LootTable at the game's own conversion seam, found by the game's linear id scan with no patch at roll time. Every field that names a table (creature drops, dungeon chest loot, placed-container loot, melody rewards, on-use loot, becomes-loot) resolves vanilla names first and the mod's own second. In-game evidence pending."),
            new DimensionCapability(
                "explosives",
                "Bombs",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A bomb is one thing an author makes and two objects the framework writes: the placeable explosive and the blast it turns into, linked by name at load because ExplosiveAuthoring.explosionID is an ObjectID and a mod has none while its prefabs are written. Three triggers, all vanilla data: a countdown, something coming close, and a wire giving it power — plus being broken, which every explosive gets for free and is what makes a pile of bombs chain. Damage and terrain damage sit on the bomb because the game copies them onto the blast every time; reach sits on the blast because nothing overwrites it. 'Leaves a patch of fire' is the one thing no prefab field can say — vanilla writes that field at runtime from a gear roll — so a framework system writes it back between the game creating the blast and the game reading it. Honest limits: TERRAIN ONLY BREAKS WITHIN 4 TILES no matter the reach (a hard-coded 9x9 block), throwing is not offered (that is the projectile path and needs a weapon), and the remote detonator is hard-coded to the game's own RemoteExplosive so a modded bomb cannot be wired to it. In-game evidence pending."),
            new DimensionCapability(
                "custom-creatures",
                "Custom creatures",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Mobs, animals, critters and bosses generate as real creature prefabs: stats, states, attacks, habits, lifecycle and the per-creature spawn chain (surface, chance, amount, groups, allowed biomes) into DimensionCreatureSpawnRegistry. Every kind now gets a body — a generated SpriteAsset built from the authored clip strips, a view prefab shaped like the game's own caveling, and hit/death sounds through the game's soundOptions. Honest limits: the framework's view is one shared pooled type per kind, so anything that differs between creatures is re-derived at spawn rather than baked, and taming is not exposed at all (the cattle subsystem was never harvested). In-game evidence pending. Keeping them coming back is its own record — see creature-respawning."),
            new DimensionCapability(
                "plants-and-food",
                "Plants and food",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A crop is authored once and generates its seed, its growing stages, its ripe form and its rarer versions, with the seed-to-plant link repaired at post-conversion so it no longer depends on a name hash resolving in time. A crop is now VISIBLE: one picture per stage plus the seed in the soil become a generated SpriteAsset, and every prefab points at a shared plant body whose art, colour, shadow and glow are re-chosen per entity from the object id and variation — which is what gives a rarer version a look of its own, the way the game gives its golden crops one. Cooking is authored on the item: ingredient type, the four-shade tint, what it gives raw and cooked, its golden version, and the dish it makes — including the category tag and object type without which nothing was edible. Honest limits: the pot's own two-slot rule is the game's, so a dish is still built from two ingredients; a version's recolour is authored as its own pictures or a colour wash rather than as a gradient map, and a modded crop cannot be shown inside a planter box, whose display is keyed to the game's own reskin list. In-game evidence pending. The item pipeline underneath is its own record — see custom-items."),
            new DimensionCapability(
                "vehicles",
                "Things a player rides",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A vehicle is a whole object now: an item with an icon and a name, placement, two-hits-to-break with the vehicle given back, and a body a player walks up to and uses to get on. Getting on is Core Keeper's own ECS path — TriggerUseControllableSystem reads the buffer the boat/minecart/kart converter adds and hands the player the matching riding state — and no step of it consults an ObjectID, so a vehicle this framework invents is driven by the same code as the game's own. Honest limits: there are exactly three kinds and a fourth cannot be added, because the player has three riding states and the dispatch is compiled; custom rails are impossible (TileType.rail is compared by literal inside a Burst job); a go-kart's ram damage is a hardcoded 10; and a generated vehicle has no dust, smoke or engine loop, because the game's own Boat and GoKart components read a hand-built particle hierarchy with no null check and cannot be reused. Generation is test-locked; in-game evidence pending."),
            new DimensionCapability(
                "creature-respawning",
                "Creatures that keep coming back",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A mob can declare the tile surface that breeds it (own or vanilla tileset; ground, nest, slime coat or water), riding the game's own periodic respawn sweep via rows appended to Manager.mod.SpawnTable before conversion. This is vanilla's dungeon-repopulation mechanism, and it runs inside spawn-blocked dungeons by the game's own design. In-game evidence pending."),
            // Distinct from "custom-tilesets" above, which is this framework's own authoring and
            // rendering of tilesets. This entry is the seam a DIFFERENT mod plugs into. Both carried
            // the id "custom-tilesets" until now, which made the registry's uniqueness test fail and
            // meant any lookup silently resolved to whichever was declared first.
            new DimensionCapability(
                "tileset-provider-seam",
                "Third-party tileset providers",
                DimensionCapabilityMaturity.ContractExtensionSeam,
                "Tile roles and a priority-arbitrated provider registry exist so a third-party tileset mod can coexist without fighting this one for the tile registry. Stated as bluntly as the code deserves: IDimensionTilesetProvider has ZERO implementations and DimensionTilesetProviderRegistry has zero callers outside its own tests — no provider ships, including the framework's own, and nothing in the framework routes tileset resolution through it. Registering a provider today therefore has no effect on anything. It is kept as a published offer to another mod author, and this note exists so that reading the interface cannot lead anyone to believe otherwise. The framework's own tilesets go the other road entirely — see custom-tilesets."),
            new DimensionCapability(
                "map-presentation",
                "Map and coordinate presentation",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Marker scoping and presented-local coordinates exist; complete vanilla-quality map/minimap isolation is unproven."),
            new DimensionCapability(
                "diagnostics-readiness",
                "Diagnostics and readiness",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Bounded diagnostics and readiness/preflight models exist; creator-readable, grouped, actionable errors are still needed."),
            new DimensionCapability(
                "automated-tests",
                "Automated tests",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "1,050 tests across 93 edit-mode test files, all four projects building clean. Coverage is broad across generation, registries, identity, layout versioning and world rules, and three structural guards now catch this framework's most expensive recurring bug — a surface that compiles and is connected to nothing: DimensionSystemLivenessTests (every system is explicitly created by the mod entry), DimensionHarmonyPatchTargetTests (every patched method exists in the game, method-level attributes included) and DimensionZeroCoverageTests. Still a partial slice for one reason that no amount of edit-mode testing fixes: these are all EDIT-MODE tests over generator and registry logic. There is no play-mode suite, no built-game run, and therefore no persistence, load-order, world-switch or multiplayer evidence anywhere in the framework."),
            new DimensionCapability(
                "scene-placement",
                "Scene placement",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Authored placement policy (mode, radial band, weight, unique, required) reaches a runtime pass with vanilla-shaped spot search and a weighted fill phase; determinism is test-locked. In-game evidence pending."),
            new DimensionCapability(
                "dungeons",
                "Dungeons",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Full pipeline archetype, authored room/path rules, fill shell, room fillings, swaps, single-scene mode, in-dimension placement (local coordinates), Overworld random-table binding (vanilla biome names bind vanilla biomes), and Overworld unique pinning all assemble; identity-gate preflight warns on unresolvable tiles. Alignment is engineered, not hoped: scenes register an explicit centre pivot, room radii default to the exact fit radius, corridor widths normalize to the odd band the game actually carves, and undersized authored rooms warn with the number to use. In-game evidence pending."),
            new DimensionCapability(
                "ore-veins",
                "Natural ore veins",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Per-ore abundance and vein size grow deterministic wall-safe veins on painted and generated terrain, gated by biome ore lists; ordering law is test-locked. In-game evidence pending — the arrays-survive-recompile log line is the proof to check."),
            new DimensionCapability(
                "boss-presentation",
                "Boss presence",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Map pins, floating names, fight music (vanilla and custom rosters), item summoning by name, and real respawn cooldowns are wired end to end. The summoning circle was inert until its four query components were all written: the game's BossSummoningSystem only looks at circles carrying NearbyEntitiesBufferCD, AnimationBuffer, AnimationBufferPointer and SummonAreaCD, and SummonAreaAuthoring supplies only the last. In-game evidence pending."),
            new DimensionCapability(
                "floating-text",
                "Words floating above things",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A sign shows what is written on it, a chest shows the name a player gives it, and a tended animal wears a name tag — the game's own WorldLabel and ObjectNameTag, wired to a generated PugText styled value for value from Chest, SignText, Camel and BossLarva. This was recorded as impossible three times on the grounds that a PugText needs a PugFont no mod ships; it does not, because PugText.font is [NonSerialized] and refilled from the game's TextManager on every render. Two things had to be right and are test-locked: the text sits on the WorldUI layer PugFont stamps onto every glyph, and it has the parent transform both components write to without a null check. In-game evidence pending."),
            new DimensionCapability(
                "dimension-types",
                "Dimension types",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "World, Dungeon, Arena and Room carry enforced behavior: ambient-spawn gating, dungeon music, arena reset and victory-armed exits. A dimension of any type can also name its own music — one of the game's rosters or a name of its own backed by the mod's clips — which outranks the type's default and yields to a boss's fight music. Migration from the old space kinds is normalize-on-read and test-locked. In-game evidence pending."),
            new DimensionCapability(
                "named-areas",
                "Named areas",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "One asset fans a title card, ambience and music out to signature blocks through the shipped registries. Honest limit: areas have no border — they exist where their blocks stand, like vanilla's own."),
            new DimensionCapability(
                "gates",
                "Doors and gates",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Held-item, nearby-object and melody gates author on world objects through vanilla components; key-in-container locks shipped earlier on containers. In-game evidence pending."),
            new DimensionCapability(
                "world-rules-upgrade-costs",
                "What upgrading costs",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Authored per-level prices reach the game through a prefix on UpgradeCostsTableConverter that deep-copies the shared table, swaps it for the call and puts the original back. Item names resolve at load, so a mod's own bars can be a price. Copy-not-mutate is test-locked; in-game evidence pending."),
            new DimensionCapability(
                "world-rules-fishing",
                "What fishing catches",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Per-biome and per-water catches emit as Conf JSON in Core Keeper's own mod-file shapes, which the game merges at start-up with no code of ours involved. Fight patterns take whichever of two roads can carry them: one of the game's own fish through the same Conf JSON, a fish this mod adds through a prefix on FishingTableConverter that appends to the table the converter is about to bake, after mod objects have their numbers. Honest limits: a custom biome or ground still cannot be keyed (the baked table has 12 biome and 75 ground slots), and every row needs a junk table because the game reads a rule without one as no rule at all. File shapes, the split between the two roads, and the append's idempotence are test-locked; in-game evidence pending."),
            new DimensionCapability(
                "world-rules-talents",
                "What talents give",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Talent names, effects and per-point values emit as Conf/Talents JSON. A talent may grant one of this mod's own conditions, which vanilla could not do. A talent with a name of its own now also gets the line the square reads (SkillTalents/<name>) and a picture — the picture through the talent window as it draws, because the file shape the game takes a mod's talents from has no field for one. Reusing one of Core Keeper's own 96 talent names deliberately writes no line, so its wording stays translated in every language. Honest limits: the twelve skills are the game's and a thirteenth cannot be added, there is no ninth square on any tree (the pyramid is an authored prefab layout), and the game merges by position so a skill's talents are listed whole. File shape and the grouping the three passes share are test-locked; in-game evidence pending."),
            new DimensionCapability(
                "world-rules-player-overrides",
                "Overrides on the game's player",
                DimensionCapabilityMaturity.PartialVerticalSlice,
                "Turning delay, vehicle drift and aim offset are written over Core Keeper's own player object (ObjectID 6000) just before a loading world reads it, then put back. Only these three numeric fields: a mod cannot ship a player, the new-character look is a placeholder the character creator overwrites, and held-item handlers are code rather than values. Restore-after-read is test-locked; in-game evidence pending."),
            new DimensionCapability(
                "world-rules-container-aux-data",
                "Extra things saved inside containers",
                DimensionCapabilityMaturity.NotImplemented,
                "The seam is known and real — a postfix on InventoryAuxDataConverter can append name-to-prefab entries — but nothing in the framework needs custom container save data yet, so no authoring surface exists. Build it when a feature actually asks for it."),

            // ---------------------------------------------------------------------------------
            // Capabilities a creator can reach from the authoring window that had NO record here.
            // Every one below is a collection or page in Editor/UI/DimensionStageCatalog.cs, so a
            // creator could author it and find nothing in the framework's own product truth about
            // how far it got. An absent row reads as "not a feature", which for these was wrong in
            // the opposite of the usual direction — they are built, and were simply never listed.
            // ---------------------------------------------------------------------------------

            new DimensionCapability(
                "custom-conditions",
                "Custom stat effects",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A mod can invent an effect Core Keeper does not have and it becomes a real condition, through two guarded prefixes: one on ConditionsTableConverter.Convert for the simulation's table, and one on ConditionsTable.GetConditionInfo for the one the interface reads. The second is what makes the effect visible at all — that table is a fixed 357 entries read with no bounds check, so before it a mod's first condition threw an exception every frame it sat on a player. It also carries the five things only that table holds: the picture, whose wording to borrow, whether to show a decimal, whether to show the sign, whether to appear in an item's stat list. Ids are assigned in name order so they are stable across builds, and a talent, an item, a food or a hazard tile may grant one. In-game evidence pending."),
            new DimensionCapability(
                "skill-experience",
                "Creatures worth killing",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A creature can be made to give skill experience when it is killed. Core Keeper awards experience only from inside Burst-compiled jobs, so the framework writes the award itself through the one door left open — the AddSkillValueCD entity PlayerController.AddSkill creates — read by the game's own AddSkillValueSystem, which then handles the level cap and the per-level condition. The kill is recognised the way vanilla's own loot job recognises one: KilledByPlayerCD present AND enabled, since it is added disabled to everything with health. Honest limits: it does not count towards the game's achievements, the amount is flat rather than scaled by area level, and only killing is exposed — harvesting and crafting are not. Costs nothing when no creature asks for it. In-game evidence pending."),
            new DimensionCapability(
                "weapons-projectiles",
                "Weapons and things that fly",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Swing arcs, charged attacks, an overridden melee hitbox, and projectiles with authored flight, spread, bounce and impact, all generated as real prefabs by DimensionProjectileGenerator on the one Generate path. In-game evidence pending."),
            new DimensionCapability(
                "containers",
                "Chests and containers",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Measured against vanilla rather than guessed: the game's own 6x3 and 12x3 grids, two-hits-with-anything to break, contents living on the placed object rather than the item, and a locked chest reading as a one-slot key socket. Generated by DimensionContainerGenerator with its own view. In-game evidence pending."),
            new DimensionCapability(
                "world-objects",
                "Props, decorations and machines",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Placeable objects with authored art, placement rules, what they do when used, what they leave behind, and whether they turn to face the player — plus the wiring and automation surfaces (what needs power, what pushes, what picks up, what may reach into inventories) and traders (stock, unlocks, seasonal rules), all applied through DimensionObjectSpine by DimensionWorldObjectGenerator. In-game evidence pending."),
            new DimensionCapability(
                "layout-versioning",
                "Layouts that do not change under an existing save",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "A world pins the layout version it was generated with, so editing the mod does not silently regenerate an existing save into a different shape. The fingerprint, the archive of past layouts, the drift policy and the Layout Studio are real, and the emitted bootstrap calls DimensionLayoutPinService.ApplyForCurrentWorld. Determinism is test-locked; in-game evidence pending."),
            new DimensionCapability(
                "localization",
                "Names and tooltips in the player's language",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Everything a creator names is collected into one localization plan on a single generate path and written as the mod's own table, with a coverage check that fails the build rather than shipping a raw id to a player. Keys are written with colons replaced by underscores because the game performs that replacement at lookup time. In-game evidence pending."),
            new DimensionCapability(
                "sound-by-name",
                "Choosing sounds by name",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Every sound in the framework is authored by picking or typing a name rather than hunting an id: the hash is Unity's own Animator.StringToHash, which is what the game uses, so all 1,413 of Core Keeper's sounds are reachable and the mapping is reversible for display. The names are now browsable from a button beside each field instead of only existing in a generated source file, and a name that is neither the game's nor blank is reported at generate time so a typo does not ship as silence — with its own sentence when the value is an audio file address, which is a different sort of string entirely (Docs/sound-key-spaces.md). Honest limits: a name cannot be previewed, because a name and a file address do not map to each other, and a mod cannot yet ship a sound of its own. In-game evidence pending."),
            new DimensionCapability(
                "equipment-skins",
                "Armour that changes how a player looks",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "Paperdoll art is a separate data block the item points at by address, and PugMod registers mod-owned data blocks, so this works at all. The sheet layout is a fixed 234x156. In-game evidence pending."),
            new DimensionCapability(
                "borrowed-behaviour",
                "Borrowing the game's own creature behaviour",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "280 vanilla attacks, chases and wanders were harvested into named presets a creature can take wholesale by name. They are POURED IN rather than linked — the values are copied onto the creature at generation — so a game update changing a vanilla attack does not change a mod that borrowed it, in either direction. In-game evidence pending."),
            new DimensionCapability(
                "drop-sources",
                "Answering what drops a thing on the thing itself",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "An item names the mobs, chests and blocks that give it and how often, and the emitter inverts that into the per-source loot the game actually reads (AppendAuthoredDropRegistrations). This is the authoring direction a creator thinks in, rather than editing one table per source. A Core Keeper loot table holds no chance per row, so the chance is turned into a number of picks and a weight each, with a row of nothing taking up the rest — the way 74 of the game's own 176 tables express a rare drop. Two things the game cannot express are reported instead: a chance on a table this mod did not make, and more than one always-drop from one source. In-game evidence pending."),
            new DimensionCapability(
                "instruments",
                "Instruments and music sheets",
                DimensionCapabilityMaturity.ImplementedNotFullyProven,
                "An object can be an instrument (note sound, octave-up sound, starting key) or a music sheet naming a track per instrument, both writing Core Keeper's own InstrumentAuthoring and MusicSheetAuthoring. A silent instrument or a blank sheet is reported at generate time. In-game evidence pending.")
        };

        /// <summary>All capability maturity records, in dependency-ish order.</summary>
        public static DimensionCapability[] All()
        {
            return (DimensionCapability[])Capabilities.Clone();
        }

        /// <summary>Looks up a capability by its stable <see cref="DimensionCapability.Id"/>.</summary>
        public static bool TryGet(string id, out DimensionCapability capability)
        {
            for (int i = 0; i < Capabilities.Length; i++)
            {
                if (string.Equals(Capabilities[i].Id, id, StringComparison.Ordinal))
                {
                    capability = Capabilities[i];
                    return true;
                }
            }

            capability = default;
            return false;
        }

        /// <summary>
        /// True only when the named capability is proven enough for a limited alpha. Unknown
        /// ids return false so callers never accidentally advertise an unlisted feature.
        /// </summary>
        public static bool IsAlphaReady(string id)
        {
            return TryGet(id, out DimensionCapability capability) && capability.IsAlphaReady;
        }

        /// <summary>Short human-readable label for a maturity level (for badges/logs).</summary>
        public static string Describe(DimensionCapabilityMaturity maturity)
        {
            switch (maturity)
            {
                case DimensionCapabilityMaturity.ImplementedAndEvidenced:
                    return "Stable";
                case DimensionCapabilityMaturity.ImplementedNotFullyProven:
                    return "Implemented (unproven)";
                case DimensionCapabilityMaturity.PartialVerticalSlice:
                    return "Partial slice";
                case DimensionCapabilityMaturity.ContractExtensionSeam:
                    return "Provider required";
                case DimensionCapabilityMaturity.AuthoringModelOnly:
                    return "Preview (authoring only)";
                case DimensionCapabilityMaturity.ExperimentalUnstable:
                    return "Experimental";
                case DimensionCapabilityMaturity.NotImplemented:
                    return "Not implemented";
                default:
                    return maturity.ToString();
            }
        }
    }
}
