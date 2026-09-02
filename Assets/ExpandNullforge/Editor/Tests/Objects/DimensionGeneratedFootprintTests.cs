#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// That the files a rename or a delete goes looking for are the files the generators wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE ONE THING HERE THAT CAN GO WRONG IN SILENCE. If a folder name or a suffix stops
    /// matching what a generator writes, the footprint finds nothing, says nothing, and every
    /// rename from then on leaves a ghost prefab behind — which PugMod still ships, still converts,
    /// and still registers an object for. So the paths are composed against the generators' own
    /// <c>FolderName</c> constants and their own naming helper here, side by side.
    /// </para>
    /// <para>
    /// What this does NOT claim: that a generator writes these files on any given run, or that the
    /// removal succeeds. It compares two path compositions, which is exactly the thing that can
    /// drift without anything else noticing.
    /// </para>
    /// </remarks>
    internal sealed class DimensionGeneratedFootprintTests
    {
        private const string ModRoot = "Assets/TestMod";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static DimensionIdentityField NameOf(Object asset, string property)
        {
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(asset);
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].Property == property)
                {
                    return names[i];
                }
            }

            Assert.Fail("No renameable name '" + property + "' on " + asset.GetType().Name);
            return null;
        }

        [Test]
        public void AMonstersFilesAreTheOnesTheCreatureGeneratorWrites()
        {
            DimensionMobAsset mob = Make<DimensionMobAsset>();
            List<string> paths =
                DimensionGeneratedFootprint.PathsUnder(ModRoot, NameOf(mob, "mobId"), "Ashling");

            string folder = ModRoot + "/" + DimensionCreatureGenerator.FolderName + "/";
            string stem = DimensionGeneratedPrefabUtility.SanitizeAuthoredName("Ashling", "Creature");

            Assert.That(paths, Contains.Item(folder + stem + ".prefab"), "the creature itself");
            Assert.That(paths, Contains.Item(folder + stem + "Visual.prefab"), "its view");
            Assert.That(paths, Contains.Item(folder + stem + "MapMarker.prefab"), "its map pin");
            Assert.That(
                paths,
                Contains.Item(folder + stem + "SummonCircle.prefab"),
                "and the circle a summoning item leaves. A creature generates four files, and " +
                "three of them are named from the same id — miss them and a renamed boss leaves " +
                "its pin and its circle behind under the old name.");
        }

        [Test]
        public void EveryKindThatGeneratesPrefabsNamesAFolderAGeneratorOwns()
        {
            // The generators' own constants. A kind naming a folder that is not one of these is
            // looking somewhere nothing was ever written.
            HashSet<string> owned = new HashSet<string>
            {
                "Items",
                DimensionCreatureGenerator.FolderName,
                DimensionCritterGenerator.FolderName,
                DimensionContainerGenerator.FolderName,
                DimensionWorkbenchGenerator.FolderName,
                DimensionPlantGenerator.FolderName,
                DimensionWorldObjectGenerator.FolderName,
                DimensionProjectileGenerator.FolderName,
                DimensionExplosionGenerator.FolderName,
                DimensionVehicleGenerator.FolderName
            };

            int checkedKinds = 0;
            IReadOnlyList<DimensionIdentityField> all = DimensionIdentityCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].GeneratedFolder.Length == 0)
                {
                    continue;
                }

                checkedKinds++;
                Assert.That(
                    owned.Contains(all[i].GeneratedFolder),
                    Is.True,
                    all[i].Owner.Name + " looks in '" + all[i].GeneratedFolder +
                    "', which no generator writes to.");
            }

            Assert.That(
                checkedKinds,
                Is.EqualTo(12),
                "Twelve rows in the catalog generate prefabs named from their id — ten folders, " +
                "with monsters, bosses and animals all writing into Creatures. If this number " +
                "moves, a kind was added or dropped and the loop above may have run over a " +
                "shorter list than anyone thinks.");
        }

        [Test]
        public void AKindThatGeneratesNoPrefabHasNoFootprint()
        {
            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            Assert.That(
                DimensionGeneratedFootprint.PathsUnder(
                    ModRoot, NameOf(loot, "lootTableId"), "DropsOld"),
                Is.Empty,
                "A loot table is never a prefab. Composing a path for one would offer to delete a " +
                "file that has nothing to do with it.");
        }

        [Test]
        public void AQualifiedIdIsLookedForBothWaysRound()
        {
            DimensionItemAsset item = Make<DimensionItemAsset>();
            List<string> paths =
                DimensionGeneratedFootprint.PathsUnder(ModRoot, NameOf(item, "itemId"), "MyMod:blade");

            Assert.That(
                paths,
                Contains.Item(ModRoot + "/Items/MyMod_blade.prefab"),
                "The generator sanitizes the id as it was typed, colon included.");
            Assert.That(
                paths,
                Contains.Item(ModRoot + "/Items/blade.prefab"),
                "And the pruner's own note says prefab files are named from the bare id. Rather " +
                "than ruling on which, both are looked for and only what exists is acted on.");
        }

        [Test]
        public void APlantsFilesAreTheOnesThePlantGeneratorWrites()
        {
            DimensionPlantAsset plant = Make<DimensionPlantAsset>();
            List<string> paths =
                DimensionGeneratedFootprint.PathsUnder(ModRoot, NameOf(plant, "plantId"), "Ember");

            string folder = ModRoot + "/" + DimensionPlantGenerator.FolderName + "/";
            string stem = DimensionGeneratedPrefabUtility.SanitizeAuthoredName("Ember", "Plant");

            Assert.That(
                paths,
                Has.No.Member(folder + stem + ".prefab"),
                "The plant generator never writes a file under the bare plant id, and this row " +
                "asked for exactly that one — so deleting a plant named zero files and left every " +
                "one of them shipping and registering an object, which is the ghost class this " +
                "whole file exists to close.");
            Assert.That(
                paths,
                Contains.Item(folder + stem + DimensionPlantGenerator.PlantSuffix + ".prefab"),
                "the growing plant");
            Assert.That(
                paths,
                Contains.Item(folder + stem + DimensionPlantGenerator.RipeSuffix + ".prefab"),
                "the same plant already ripe");
            Assert.That(
                paths,
                Contains.Item(folder + stem + DimensionPlantGenerator.SeedSuffix + ".prefab"),
                "its seed");
            Assert.That(
                paths,
                Contains.Item(folder + stem + DimensionPlantGenerator.PlainSeedSuffix + ".prefab"),
                "and the seed an ordinary planting sits on. The version variants are deliberately " +
                "not here: their names come from each version's own word at generate time, so " +
                "naming them would either miss some or claim files nothing wrote.");
        }
    }
}
#endif
