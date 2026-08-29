#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Entities;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The shared mechanism that lets a mod's own object be named by another of the mod's objects.
    /// </summary>
    /// <remarks>
    /// Each test names the failure it would catch, because every one of them is a failure that is
    /// invisible in game: the reference is <c>ObjectID.None</c>, the game spawns nothing, and
    /// nothing anywhere says so.
    /// </remarks>
    internal sealed class DimensionObjectLinkTests
    {
        private World world;

        [SetUp]
        public void SetUp()
        {
            DimensionObjectLinkRegistry.Clear();
            world = new World("nullforge-object-links");
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null && world.IsCreated)
            {
                world.Dispose();
            }

            world = null;
            DimensionObjectLinkRegistry.Clear();
            DimensionItemObjectRegistry.Clear();
        }

        // ---- the registry --------------------------------------------------------------

        [Test]
        public void ARowIsReplacedRatherThanAppendedWhenItIsRegisteredTwice()
        {
            // The generated bootstrap calls EnsureStaticRuntimeExtras from both EarlyInit and Init,
            // and the retry path can call it again. Appending would leave the hydration system
            // walking the same row twice and its settled ledger permanently short of the row count,
            // so it would never stop scanning.
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.FiresProjectile, "MyMod:Bolt");
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.FiresProjectile, "MyMod:BetterBolt");

            Assert.That(DimensionObjectLinkRegistry.All.Count, Is.EqualTo(1));
            Assert.That(DimensionObjectLinkRegistry.All[0].TargetObjectName,
                Is.EqualTo("MyMod:BetterBolt"));
        }

        [Test]
        public void TwoDifferentFieldsOnOneObjectAreTwoRows()
        {
            // Keyed on owner alone, the second registration would overwrite the first and the bow
            // would fire its wind-up shot and nothing else.
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.FiresProjectile, "MyMod:Bolt");
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.SecondProjectile, "MyMod:BigBolt");

            Assert.That(DimensionObjectLinkRegistry.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void TwoPositionsInOneListAreTwoRows()
        {
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.RandomProjectile, "MyMod:Red", 0);
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.RandomProjectile, "MyMod:Blue", 1);

            Assert.That(DimensionObjectLinkRegistry.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void ARowWithNoTargetIsNotRegisteredAtAll()
        {
            DimensionObjectLinkRegistry.Register(
                "MyMod:Bow", DimensionObjectLink.FiresProjectile, string.Empty);

            Assert.That(DimensionObjectLinkRegistry.All.Count, Is.EqualTo(0),
                "An empty target would make the hydration system retry forever on a row that can " +
                "never resolve.");
        }

        // ---- the write table -----------------------------------------------------------

        [Test]
        public void EveryLinkInTheEnumHasAnArmThatWritesSomething()
        {
            // The enum is the framework's census of what it can defer. A member with no arm is a
            // promise nothing keeps: the generator would leave the name for the runtime, the
            // runtime would answer "this framework does not write that kind of link", and the
            // reference would be dead with one confusing log line.
            foreach (DimensionObjectLink link in Enum.GetValues(typeof(DimensionObjectLink)))
            {
                if (link == DimensionObjectLink.None)
                {
                    continue;
                }

                // A fresh entity per link: the one arm that ADDS its component would otherwise
                // leave it behind for the next.
                Entity entity = world.EntityManager.CreateEntity();
                DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                    world.EntityManager,
                    entity,
                    new DimensionObjectLinkDefinition("Owner", link, "Target"),
                    ObjectID.Wood);

                // The entity carries no vanilla components, so every arm either says Impossible in
                // its own words or — for the one arm whose vanilla converter never wrote the
                // component in the first place — adds it. What no arm may do is fall through to
                // the catch-all at the bottom of the switch.
                Assert.That(outcome.Reason, Does.Not.Contain("does not write"),
                    link + " has no arm in DimensionObjectLinkHydration.Apply.");

                if (link == DimensionObjectLink.PolishesInto)
                {
                    Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written),
                        "Polishing has to add its component; JewelryConverter never wrote one.");
                    continue;
                }

                Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Impossible),
                    link + " wrote into a component the prefab does not have.");
                Assert.That(outcome.Reason, Is.Not.Empty,
                    link + " gave no reason a creator could act on.");
            }
        }

        [Test]
        public void TheWeaponArmWritesTheShotOntoTheRangedComponent()
        {
            Entity prefab = world.EntityManager.CreateEntity(typeof(RangeWeaponCD));

            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                prefab,
                new DimensionObjectLinkDefinition(
                    "MyMod:Bow", DimensionObjectLink.FiresProjectile, "MyMod:Bolt"),
                ObjectID.WoodArrowProjectile);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written));
            Assert.That(
                world.EntityManager.GetComponentData<RangeWeaponCD>(prefab).projectileID,
                Is.EqualTo(ObjectID.WoodArrowProjectile));
        }

        [Test]
        public void WritingTheSameValueTwiceIsNotAWrite()
        {
            // Setting a component marks its chunk changed whether or not the value moved, and a
            // re-entered world re-runs every row.
            Entity prefab = world.EntityManager.CreateEntity(typeof(RangeWeaponCD));
            world.EntityManager.SetComponentData(prefab, new RangeWeaponCD
            {
                projectileID = ObjectID.WoodArrowProjectile
            });

            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                prefab,
                new DimensionObjectLinkDefinition(
                    "MyMod:Bow", DimensionObjectLink.FiresProjectile, "MyMod:Bolt"),
                ObjectID.WoodArrowProjectile);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.AlreadyCorrect));
        }

        [Test]
        public void ARandomShotIsWrittenAtItsOwnPositionAndNowhereElse()
        {
            Entity prefab = world.EntityManager.CreateEntity(typeof(RangeWeaponCD));
            RangeWeaponCD weapon = default;
            weapon.randomProjectiles.Add(ObjectID.WoodArrowProjectile);
            weapon.randomProjectiles.Add(ObjectID.None);
            weapon.randomProjectiles.Add(ObjectID.Wood);
            world.EntityManager.SetComponentData(prefab, weapon);

            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                prefab,
                new DimensionObjectLinkDefinition(
                    "MyMod:Bow", DimensionObjectLink.RandomProjectile, "MyMod:Bolt", 1),
                ObjectID.IronArrowProjectile);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written));
            RangeWeaponCD after = world.EntityManager.GetComponentData<RangeWeaponCD>(prefab);
            Assert.That(after.randomProjectiles[0], Is.EqualTo(ObjectID.WoodArrowProjectile));
            Assert.That(after.randomProjectiles[1], Is.EqualTo(ObjectID.IronArrowProjectile));
            Assert.That(after.randomProjectiles[2], Is.EqualTo(ObjectID.Wood));
        }

        [Test]
        public void PolishingAddsTheComponentBecauseTheGameOnlyAddsItForAKnownId()
        {
            // JewelryConverter writes JewelryCanBePolishedCD only when the id it reads is not None
            // (ck-db\Pug.ECS.Conversion\JewelryConverter.cs), and the id we baked WAS None. This is
            // the one arm in the table that has to add a component rather than write into one; a
            // Set would throw and the whole polish path would be dead.
            Entity prefab = world.EntityManager.CreateEntity();
            Assert.That(world.EntityManager.HasComponent<JewelryCanBePolishedCD>(prefab), Is.False);

            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                prefab,
                new DimensionObjectLinkDefinition(
                    "MyMod:Ring", DimensionObjectLink.PolishesInto, "MyMod:BetterRing"),
                ObjectID.Wood);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written));
            Assert.That(
                world.EntityManager.GetComponentData<JewelryCanBePolishedCD>(prefab).polishedVersion,
                Is.EqualTo(ObjectID.Wood));
        }

        [Test]
        public void AMissingComponentIsSaidOnceInWordsThatNameTheCheckbox()
        {
            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                world.EntityManager.CreateEntity(),
                new DimensionObjectLinkDefinition(
                    "MyMod:Rod", DimensionObjectLink.ScansFor, "MyMod:Ore"),
                ObjectID.Wood);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Impossible));
            Assert.That(outcome.Reason, Does.Contain("generate again"),
                "A creator reading the log has to be told what to go and do.");
        }

        // ---- the resolver --------------------------------------------------------------

        [Test]
        public void TheGamesOwnNamesResolveWithNoGameRunning()
        {
            // The runtime lookup is a dictionary a loaded mod fills, so outside a game it answers
            // None for every one of the game's own names. Four copies of this resolver existed and
            // they had drifted; this is what the whole framework now shares.
            Assert.That(DimensionObjectNames.Resolve("Wood"), Is.EqualTo(ObjectID.Wood));
        }

        [Test]
        public void TheLiteralNameNoneIsNotAnObject()
        {
            // Enum.TryParse("None") SUCCEEDS. Two of the generators' own helpers returned it as a
            // resolved id, so an object naming "None" read as a valid reference.
            Assert.That(DimensionObjectNames.Resolve("None"), Is.EqualTo(ObjectID.None));
            Assert.That(DimensionObjectBinder.Vanilla("None"), Is.EqualTo(ObjectID.None));
        }

        // ---- the binder ----------------------------------------------------------------

        [Test]
        public void TheBinderTellsTheThreeCasesApart()
        {
            DimensionObjectBinder binder = new DimensionObjectBinder(
                new DimensionNamingContext("MyMod", new[] { "EmberBolt" }));

            ObjectID baked;
            Assert.That(binder.TryBind("Wood", out baked), Is.True, "a game object");
            Assert.That(baked, Is.EqualTo(ObjectID.Wood));

            Assert.That(binder.TryBind("EmberBolt", out baked), Is.True, "one of the mod's own");
            Assert.That(baked, Is.EqualTo(ObjectID.None));
            Assert.That(binder.IsDeferred("EmberBolt"), Is.True);

            Assert.That(binder.TryBind("Embrbolt", out baked), Is.False, "a typo");
            Assert.That(binder.IsDeferred("Embrbolt"), Is.False,
                "A typo deferred to the runtime is a reference that is never reported and never " +
                "resolves.");
        }

        // ---- the item registry's three defects -----------------------------------------

        [Test]
        public void PendingCountIsNotCorruptedByAnUndeclaredResolution()
        {
            // Resolved is filled by every TryResolve, declared or not, so Declared.Count minus
            // Resolved.Count went negative and IsComplete answered true while declared items were
            // genuinely missing.
            DimensionItemObjectRegistry.SetResolverForTesting(
                name => name == "known" ? ObjectID.Wood : ObjectID.None);
            try
            {
                DimensionItemObjectRegistry.Declare("pack", new[] { "known", "missingOne" });
                DimensionItemObjectRegistry.TryResolve("known", out _);
                DimensionItemObjectRegistry.TryResolve("neverDeclared", out _);

                Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(1));
                Assert.That(DimensionItemObjectRegistry.IsComplete, Is.False);
            }
            finally
            {
                DimensionItemObjectRegistry.SetResolverForTesting(null);
            }
        }

        [Test]
        public void ClearForgetsTheTestResolver()
        {
            // Clear left a fake lookup wired into a static that production code reads.
            DimensionItemObjectRegistry.SetResolverForTesting(name => ObjectID.Wood);
            DimensionItemObjectRegistry.Clear();

            Assert.That(DimensionItemObjectRegistry.TryResolve("NotAnObjectAnywhere", out _),
                Is.False);
        }

        [Test]
        public void DeclaringSomethingReArmsTheOneShotReport()
        {
            int before = DimensionItemObjectRegistry.DeclarationVersion;
            DimensionItemObjectRegistry.Declare("pack", new[] { "thing" });

            Assert.That(DimensionItemObjectRegistry.DeclarationVersion, Is.GreaterThan(before),
                "Without this the report fires on the first frame a world exists, which is before " +
                "ApplyManifests has run, and the ledger is never read.");
        }

        // ---- the system is created in BOTH world blocks --------------------------------

        [Test]
        public void TheHydrationSystemIsCreatedInBothWorldBlocks()
        {
            // The liveness test regex-matches the call ANYWHERE in the file, so a system whose
            // WorldSystemFilter says both worlds but whose creation line sits only in the server
            // block passes while constructing nothing on the client.
            string entry = File.ReadAllText(ModEntryPath());
            string server = MethodBody(entry, "RegisterServerWorld");
            string client = MethodBody(entry, "RegisterClientWorld");
            const string call = "DimensionObjectLinkHydrationSystem>()";

            Assert.That(server, Does.Contain(call), "Missing from RegisterServerWorld.");
            Assert.That(client, Does.Contain(call), "Missing from RegisterClientWorld.");
        }

        [Test]
        public void TheCustomTileSystemsAreCreatedOnTheServerAndNowhereElse()
        {
            // WHAT THIS USED TO ASSERT AND WHY IT CHANGED. It checked that the two creation calls
            // sat below the "already registered" early-out in RegisterClientWorld, because they had
            // once been stranded above it. Then the deeper fault came out: they should not be in
            // that method at all. Everything that touches a serialized submap is server-side —
            // DeserializeComponentsSystem is [WorldSystemFilter(ServerSimulation)] — so a client
            // copy of either system queries and returns forever, and EnsureSystemOrdering was
            // asking a client group to order the capture system against a system that world does
            // not have. Both classes now say ServerSimulation, and the client block must not
            // contradict them.
            string entry = File.ReadAllText(ModEntryPath());
            string server = MethodBody(entry, "RegisterServerWorld");
            string client = MethodBody(entry, "RegisterClientWorld");

            Assert.That(
                server,
                Does.Contain("DimensionCustomTileCaptureSystem>()"),
                "The capture system is not looked up in the server block, so EnsureSystemOrdering "
                + "below it has nothing to order.");
            Assert.That(
                server,
                Does.Contain("DimensionCustomTileRestoreSystem>()"),
                "The restore half is not looked up in the server block, and a bracket with one "
                + "half captures tile layers and never puts them back.");
            Assert.That(
                client,
                Does.Not.Contain("DimensionCustomTileCaptureSystem>()"),
                "The capture system is server-only, so creating it in the client block makes a "
                + "system that can never do anything and re-emits the engine's ordering warning.");
            Assert.That(
                client,
                Does.Not.Contain("EnsureSystemOrdering"),
                "EnsureSystemOrdering orders the capture system before the game's deserializer, "
                + "and a client world has neither.");
        }

        // ---- the two lists that can only ever be baked at conversion --------------------

        [Test]
        public void AVehicleThatStandsOnAModBlockKeepsTheWholeListAtConversion()
        {
            // PlaceableObjectAuthoring.canBePlacedOnObjects goes into the baked property blob with
            // SetPropertyList, and the blob is sealed before any world exists — so no hydration
            // system can ever reach it. The names ride on our own component and both halves of the
            // list are written together at conversion, which is the only moment both are possible.
            UnityEngine.GameObject host = new UnityEngine.GameObject("placed-on-probe");
            try
            {
                ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                    host.AddComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>();
                names.canBePlacedOnNames = new[] { "Wood", "MyMod:Jetty" };

                ExpandNullforge.Objects.DimensionPlacedOnLists lists =
                    ExpandNullforge.Objects.DimensionPlacedOnLists.Build(names);

                Assert.That(lists.EverythingResolved, Is.False,
                    "The mod's block has no id outside a running game, so the converter must hold " +
                    "off and let the post-converter write the complete list.");
                Assert.That(lists.CanBePlacedOn, Is.EquivalentTo(new[] { ObjectID.Wood }));

                // The game merges "can be placed on" into "survives on" itself, so a list written
                // only into the first property would leave the object destroying itself where it
                // was legally placed.
                Assert.That(lists.AllowedTiles, Is.EquivalentTo(new[] { ObjectID.Wood }));

                Dictionary<string, ObjectID[]> written = new Dictionary<string, ObjectID[]>();
                lists.WriteWith((property, values) => written[property] = values);
                Assert.That(written.ContainsKey("PlaceableObject/canBePlacedOnObjects"), Is.True);
                Assert.That(written.ContainsKey("CanBePlaced/allowedObjects"), Is.True);
                Assert.That(written.ContainsKey("PlaceableObject/canNotBePlacedOnObjects"), Is.False,
                    "An empty list must not be written; SetPropertyList overwrites, so writing " +
                    "nothing is how the game's own default survives.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ---- nobody parses object names by hand any more --------------------------------

        [Test]
        public void TheConvertedGeneratorsNoLongerParseObjectNamesThemselves()
        {
            // What stops the twenty-second site being added next month. Every generator that
            // resolves an object name is listed — the three that were scoped in before left the
            // creature, container and world-object copies invisible to this check, and each of
            // those was still asking a runtime lookup that is empty at generation time.
            string[] files =
            {
                "Editor/DimensionItemGenerator.cs",
                "Editor/DimensionProjectileGenerator.cs",
                "Editor/DimensionExplosionGenerator.cs",
                "Editor/DimensionCreatureGenerator.cs",
                "Editor/DimensionContainerGenerator.cs",
                "Editor/DimensionWorldObjectGenerator.cs"
            };

            foreach (string relative in files)
            {
                string text = DimensionFrameworkSourceScanner.ReadByName(
                    Path.GetFileName(relative));
                foreach (Match match in Regex.Matches(text, @"Enum\.TryParse\([^)]*out (\w+)"))
                {
                    string variable = match.Groups[1].Value;
                    Assert.That(
                        Regex.IsMatch(text, @"ObjectID\s+" + variable + @"\s*[;=]"),
                        Is.False,
                        relative + " parses an ObjectID by hand at '" + match.Value +
                        "'. Route it through DimensionObjectBinder so one of this mod's own " +
                        "objects is deferred rather than reported as a mistake.");
                }
            }
        }

        [Test]
        public void EveryLinkInTheEnumHasSomethingThatEmitsIt()
        {
            // THE TEST THE PREVIOUS ONE WAS MISTAKEN FOR. Walking the enum and finding an arm
            // proves the runtime could write the field; it proves nothing about whether anything
            // ever asks. Fourteen members once had arms, careful doc comments and no producer at
            // all — several hundred lines of code no template could reach, under a remark saying
            // every member was wired. This reads the emitter itself and fails on the first member
            // no walk in it names.
            //
            // THE COMMENTS ARE STRIPPED FIRST, and that is not tidiness. The emitter's own remarks
            // name several members in prose, so a member could lose its producer and stay green on
            // the strength of the sentence explaining what it used to do.
            string emitter = CodeWithoutComments(DimensionFrameworkSourceScanner.ReadByName(
                "DimensionRuntimeConsumerBootstrapUtility.ObjectLinks.cs"));

            foreach (DimensionObjectLink link in Enum.GetValues(typeof(DimensionObjectLink)))
            {
                if (link == DimensionObjectLink.None)
                {
                    continue;
                }

                Assert.That(
                    emitter,
                    Does.Contain("DimensionObjectLink." + link),
                    link + " is in the enum and nothing in the bootstrap emitter ever writes a " +
                    "row for it. Either give it a producer or take it out of the enum — an arm " +
                    "nothing can reach is worse than a shorter list, because the doc says it works.");
            }
        }

        [Test]
        public void TheEnumSaysNothingItCannotDo()
        {
            // The remark on the enum is load-bearing: the next person to add a member reads it to
            // learn what a member costs. It used to say every member was wired while fourteen were
            // not, so it is pinned to the claim the test above actually checks.
            string enumSource =
                DimensionFrameworkSourceScanner.ReadByName("DimensionObjectLink.cs");

            Assert.That(
                enumSource,
                Does.Contain("EVERY MEMBER IS WIRED END TO END"),
                "The enum's own remark is what tells the next author a member needs a producer.");
            Assert.That(
                enumSource,
                Does.Not.Contain("ParchmentRecipe"),
                "ParchmentRecipe had an arm and no authoring surface anywhere in the framework, " +
                "so nothing could ever have produced it. It was removed rather than left as a " +
                "member that reads as available.");
        }

        [Test]
        public void TheTwoLootTablePatchesAreOrderedRatherThanRaced()
        {
            // Both are prefixes on LootTableConverter.Convert, and Harmony orders unrelated patch
            // classes of equal priority arbitrarily. The drop registry looks a mob's table up by
            // the id on its own DropsLootFromLootTableCD, which for one of this mod's creatures is
            // a minted id belonging to a table the OTHER prefix creates — so if the drop pass wins
            // the race the drop is skipped and `applied` latches for the rest of the session.
            string creates =
                DimensionFrameworkSourceScanner.ReadByName("DimensionLootTableRegistry.cs");
            string appends =
                DimensionFrameworkSourceScanner.ReadByName("DimensionPortalDropRegistry.cs");

            Assert.That(
                creates,
                Does.Contain("[HarmonyPriority(Priority.First)]"),
                "The pass that CREATES this mod's loot tables must run first.");
            Assert.That(
                appends,
                Does.Contain("[HarmonyPriority(Priority.Last)]"),
                "The pass that APPENDS drops to those tables must run last.");
            Assert.That(
                creates,
                Does.Not.Contain("order irrelevant"),
                "The comment claiming the order does not matter outlived the fact.");
        }

        [Test]
        public void ADropSourceOfTheModsOwnIsGivenALootTableToHangDropsOn()
        {
            // The headline of the whole wave. A creature authored the ordinary way has no loot
            // table asset, so it carried no DropsLootFromLootTableCD at all — and the runtime drop
            // registry finds a source's table through exactly that component. The drop had nowhere
            // to go, so it never happened, and the one warning read like a spelling mistake.
            string creature =
                DimensionFrameworkSourceScanner.ReadByName("DimensionCreatureGenerator.cs");
            string container =
                DimensionFrameworkSourceScanner.ReadByName("DimensionContainerGenerator.cs");
            string worldObject =
                DimensionFrameworkSourceScanner.ReadByName("DimensionWorldObjectGenerator.cs");

            foreach (string source in new[] { creature, container, worldObject })
            {
                Assert.That(
                    source,
                    Does.Contain("EnsureALootTableToHangDropsOn"),
                    "A drop that needs a table is collected here and the source is given no table " +
                    "to put it in, so the drop is lost.");
            }

            // And the id both sides compute is pure arithmetic on one name, so the prefab can carry
            // it without the registry ever being consulted.
            Assert.That(
                ExpandNullforge.Loot.DimensionLootTableRegistry.ComputeLootTableId(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor("MyMod:Grub")),
                Is.EqualTo(ExpandNullforge.Loot.DimensionLootTableRegistry.ComputeLootTableId(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor("MyMod:Grub"))));
            Assert.That(
                ExpandNullforge.Loot.DimensionLootTableRegistry.ComputeLootTableId(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor("MyMod:Grub")),
                Is.Not.EqualTo(ExpandNullforge.Loot.DimensionLootTableRegistry.ComputeLootTableId(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor("MyMod:Bug"))),
                "Two creatures would share one table, so one would drop the other's loot.");
            Assert.That(
                ExpandNullforge.Loot.DimensionLootTableRegistry.ComputeLootTableId(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.AutoTableNameFor("MyMod:Grub")),
                Is.GreaterThanOrEqualTo(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.MinCustomLootTableId),
                "An auto table must never mint an id in the game's own range.");
        }

        [Test]
        public void AModBossNamedOnASummoningItemIsCarriedByName()
        {
            // The authoring component was kept and nothing was registered and nothing was warned:
            // the idol generated clean and did nothing on a summoning circle, forever. The names
            // ride on the framework's own summoning component, whose converter appends them into
            // the game's SummoningItemBuffer at load.
            // Comments stripped, and the WRITE looked for rather than the type's name: the type is
            // mentioned in prose in three places here, so a grep for the bare name would survive
            // the assignment being deleted.
            string spine = CodeWithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("DimensionObjectSpine.cs"));

            Assert.That(
                spine,
                Does.Contain("DimensionSummoningItemAuthoring"),
                "ApplyWorldRoles drops a mod boss's name on the floor, so nothing summons it.");
            Assert.That(
                spine.Replace("\r", string.Empty).Replace("\n", " "),
                Does.Match(@"\.bossObjectNames\s*=\s*ourBosses"),
                "The names of this mod's own bosses are gathered and never written onto the " +
                "component, so the idol still does nothing on a summoning circle.");
        }

        [Test]
        public void NoRepairMessageNamesAControlThatDoesNotExist()
        {
            // Rule (3) of the hydration table's own remarks. Every "Tick 'x'" in it used to name a
            // checkbox nobody could find — the shot's control is a dropdown, the scanner's is a
            // text box, and "it fires a beam" is a heading rather than a tick.
            //
            // IT READS THE MESSAGES RATHER THAN A LIST OF OLD ONES. This used to be nine literal
            // strings that no longer appear anywhere, so it would have stayed green through every
            // control invented after it was written — the same criticism this wave made of the
            // enum census. Now it lifts every quoted phrase out of the repair messages and looks
            // for it in the tooltips a modder actually reads, so inventing a new one fails here.
            string table =
                DimensionFrameworkSourceScanner.ReadByName("DimensionObjectLinkHydration.cs");

            // EVERY framework source, not one folder. The tooltips a modder reads live on the
            // authoring templates, and where those templates live is not this test's business:
            // naming the folder means the day one moves, a repair message can name a control
            // nobody can find and this stays green.
            string authoring = string.Empty;
            foreach (string file in DimensionFrameworkSourceScanner.SourceFiles())
            {
                authoring += File.ReadAllText(file);
            }

            List<string> named = QuotedControlNames(table);
            Assert.That(
                named.Count,
                Is.GreaterThan(3),
                "No repair message names a control at all, so this test is proving nothing. " +
                "Either the messages stopped saying what to do, or the phrasing this reads changed.");

            foreach (string control in named)
            {
                Assert.That(
                    authoring,
                    Does.Contain(control),
                    "A repair message tells a modder to find '" + control + "', and no tooltip in " +
                    "the authoring surface says that. A message naming a control nobody can find " +
                    "is worse than no message.");
            }
        }

        /// <summary>
        /// A source file with its comments taken out, so a test that greps for a symbol cannot be
        /// satisfied by prose about that symbol.
        /// </summary>
        private static string CodeWithoutComments(string source)
        {
            string withoutBlocks = Regex.Replace(
                source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
        }

        /// <summary>
        /// The control names a set of repair messages tells a modder to go and find.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only the phrases introduced by one of the lead-ins below count. Every other quoted string
        /// in the file is an object name, a field name or a piece of prose, and reading those as
        /// controls would make this fail on things that are not controls at all.
        /// </para>
        /// <para>
        /// THE LEAD-IN LIST WAS TWO LONG AND THE REMARK CLAIMED IT WAS ALL OF THEM. Three repair
        /// messages name a control after "Set '", "under '" or "settings under '" — the ranged
        /// weapon kind, the melody list and the beam block — and none of the three was extracted, so
        /// a new message written that way would have stayed green for ever. That is the exact
        /// criticism this test's own comment levels at the version it replaced.
        /// </para>
        /// </remarks>
        private static List<string> QuotedControlNames(string source)
        {
            // A message is written across several source lines as "…Tick " + "'It leaves…'", so
            // neither the lead-in nor the quoted phrase is contiguous in the file. Joining every
            // C# concatenation first is what makes the phrase readable the way a modder reads it —
            // and without it this test would quietly skip the wrapped messages, which are most of
            // them.
            string joined = Regex.Replace(source, "\"\\s*\\+\\s*\"", string.Empty);

            List<string> names = new List<string>();
            foreach (string lead in new[] { "Tick '", "Fill in '", "Set '", "under '" })
            {
                int at = 0;
                while (true)
                {
                    int start = joined.IndexOf(lead, at, StringComparison.Ordinal);
                    if (start < 0)
                    {
                        break;
                    }

                    start += lead.Length;
                    int end = joined.IndexOf('\'', start);
                    if (end < 0)
                    {
                        break;
                    }

                    string quoted = joined.Substring(start, end - start).Trim();
                    if (quoted.Length > 3 && !names.Contains(quoted))
                    {
                        names.Add(quoted);
                    }

                    at = end + 1;
                }
            }

            return names;
        }

        /// <summary>Where the mod entry point is, wherever it has been put.</summary>
        /// <remarks>
        /// This used to resolve from Directory.GetCurrentDirectory(), which is whatever launched
        /// the editor rather than the project folder, so on a machine started from anywhere else
        /// the read found nothing.
        /// </remarks>
        private static string ModEntryPath()
        {
            return DimensionFrameworkSourceScanner.FindByName("ExpandNullforgeModEntry.cs");
        }

        /// <summary>The text of one method, from its signature to the start of the next one.</summary>
        /// <summary>
        /// One method's body from the mod entry's source, with its comments blanked out.
        /// </summary>
        /// <remarks>
        /// THE COMMENTS GO BECAUSE EVERY CALLER HERE IS A SUBSTRING SEARCH. A test that asserts a
        /// call is absent was failing on the comment that explains why it is absent, and a test
        /// that asserts a call is present would pass on a commented-out one. Neither is the
        /// question being asked. It removes a <c>//</c> run to the end of its line, which is every
        /// comment in this file's subject.
        /// </remarks>
        private static string MethodBody(string source, string methodName)
        {
            int start = source.IndexOf("private void " + methodName + "()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), methodName + " is not in the mod entry.");

            int next = source.IndexOf("\n  private ", start + 1, StringComparison.Ordinal);
            string body = next < 0 ? source.Substring(start) : source.Substring(start, next - start);

            string[] lines = body.Split('\n');
            System.Text.StringBuilder kept = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                int slashes = lines[i].IndexOf("//", StringComparison.Ordinal);
                kept.Append(slashes < 0 ? lines[i] : lines[i].Substring(0, slashes)).Append('\n');
            }

            return kept.ToString();
        }
    }
}
#endif
