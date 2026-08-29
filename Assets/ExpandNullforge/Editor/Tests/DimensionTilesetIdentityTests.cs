#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Tilesets;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Guards the name → id mapping that world saves are written against.
    /// </summary>
    /// <remarks>
    /// A world stores the bare numeric id in its chunk data, so the mapping is effectively part of
    /// the save format: if it ever changes, every tile a player already placed starts naming a
    /// different tileset. These tests exist to make that change impossible to make by accident —
    /// the golden vectors below fail the moment the hash, the range, or the character walk moves.
    /// </remarks>
    internal sealed class DimensionTilesetIdentityTests
    {
        /// <summary>
        /// The save-format lock. If this fails, the identity function changed and every existing
        /// world with custom tiles in it just broke. Do not "fix" it by updating the constants
        /// unless renumbering is the actual intent.
        /// </summary>
        [Test]
        public void GoldenVectors_PinTheMapping()
        {
            Assert.That(DimensionTilesetRegistry.ComputeTilesetId(""), Is.EqualTo(1939984251));
            Assert.That(DimensionTilesetRegistry.ComputeTilesetId("DimensionsAPI:nullglass"), Is.EqualTo(1079990253));
            Assert.That(DimensionTilesetRegistry.ComputeTilesetId("MyMod:stone"), Is.EqualTo(2069250073));
            Assert.That(DimensionTilesetRegistry.ComputeTilesetId("MyMod:stone_dark"), Is.EqualTo(378469402));
            Assert.That(DimensionTilesetRegistry.ComputeTilesetId("OtherMod:stone"), Is.EqualTo(987023729));
        }

        [Test]
        public void ANullName_IsTreatedAsEmptyRatherThanThrowing()
        {
            Assert.That(
                DimensionTilesetRegistry.ComputeTilesetId(null),
                Is.EqualTo(DimensionTilesetRegistry.ComputeTilesetId("")));
        }

        /// <summary>
        /// Every id must clear the vanilla bank (so it can never be mistaken for a stock tileset)
        /// and stay a positive int (so the runtime's own bounds check passes).
        /// </summary>
        [Test]
        public void EveryIdLandsInsideTheCustomBand()
        {
            foreach (string name in RealisticNames(5000))
            {
                int id = DimensionTilesetRegistry.ComputeTilesetId(name);
                Assert.That(id, Is.GreaterThanOrEqualTo(DimensionTilesetRegistry.MinCustomTilesetId), name);
                Assert.That(id, Is.LessThan(DimensionTilesetRegistry.MaxCustomTilesetIdExclusive), name);
            }
        }

        /// <summary>
        /// The regression this range exists for. Under the old 16-bit band these 5000 names would
        /// have collided thousands of times over — the band only held ~64k ids in total.
        /// </summary>
        [Test]
        public void FiveThousandNamesAcrossFiftyMods_CollideZeroTimes()
        {
            Dictionary<int, string> byId = new Dictionary<int, string>();

            foreach (string name in RealisticNames(5000))
            {
                int id = DimensionTilesetRegistry.ComputeTilesetId(name);
                Assert.That(
                    byId.ContainsKey(id), Is.False,
                    "'" + name + "' collides with '" + (byId.ContainsKey(id) ? byId[id] : "?") +
                    "' at id " + id);
                byId.Add(id, name);
            }

            Assert.That(byId.Count, Is.EqualTo(5000));
        }

        /// <summary>
        /// Names in one mod share a long prefix and differ only in the tail — the input shape a
        /// weak hash smears together.
        /// </summary>
        [Test]
        public void NamesSharingALongPrefix_StayDistinct()
        {
            string[] names =
            {
                "MyDimensionMod:stone",
                "MyDimensionMod:stone_dark",
                "MyDimensionMod:stone_darker",
                "MyDimensionMod:stone_mossy",
                "MyDimensionMod:stone_mossy_2",
                "MyDimensionMod:stonf",
            };

            HashSet<int> ids = new HashSet<int>();
            foreach (string name in names)
            {
                Assert.That(ids.Add(DimensionTilesetRegistry.ComputeTilesetId(name)), Is.True, name);
            }
        }

        /// <summary>
        /// The qualifier is what lets two mods both ship a tileset called "stone", so it has to
        /// reach the id.
        /// </summary>
        [Test]
        public void TheSameTilesetNameInDifferentMods_GetsDifferentIds()
        {
            Assert.That(
                DimensionTilesetRegistry.ComputeTilesetId("ModA:stone"),
                Is.Not.EqualTo(DimensionTilesetRegistry.ComputeTilesetId("ModB:stone")));
        }

        // ---- the identity a rename is allowed to keep ----

        [Test]
        public void ABlockThatWasNeverRenamed_DerivesItsIdentityExactlyAsItAlwaysDid()
        {
            // The freeze field was added to an existing format. If an untouched block's identity
            // moved by so much as a character, every world already carrying its tiles would break
            // on the next load — which is the very failure the freeze exists to prevent.
            ExpandNullforge.Authoring.DimensionTilesetAsset asset = MakeAsset("Eerie Stone");

            Assert.That(asset.IdentityToken, Is.EqualTo("EerieStone"));
            Assert.That(asset.IdentityIsFrozen, Is.False);
            Assert.That(asset.TilesetName, Is.EqualTo("Mod:EerieStone"));

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void AFrozenIdentity_SurvivesAnyLaterName()
        {
            // What "rename, keep the identity" buys: the words change, the number a saved world
            // wrote into its tiles does not.
            ExpandNullforge.Authoring.DimensionTilesetAsset asset = MakeAsset("Eerie Stone");
            int before = asset.TilesetId;

            Freeze(asset, "EerieStone");
            SetName(asset, "Haunted Basalt");

            Assert.That(asset.BlockName, Is.EqualTo("Haunted Basalt"));
            Assert.That(asset.IdentityToken, Is.EqualTo("EerieStone"));
            Assert.That(asset.TilesetId, Is.EqualTo(before));
            Assert.That(asset.GroundBlockItemId, Is.EqualTo("Mod:EerieStone.ground.block"));

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void RenamingWithoutFreezing_ReallyDoesMintANewIdentity()
        {
            // The other answer has to keep working too: a creator who says "start fresh" means it.
            ExpandNullforge.Authoring.DimensionTilesetAsset asset = MakeAsset("Eerie Stone");
            int before = asset.TilesetId;

            SetName(asset, "Haunted Basalt");

            Assert.That(asset.TilesetId, Is.Not.EqualTo(before));

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void PunctuationAndSpacingAreNotARename()
        {
            // The guard asks nothing when the identity cannot move, and this is why: the token is
            // letters and digits only, so these three names are one block.
            Assert.That(ExpandNullforge.Authoring.DimensionTilesetAsset.IdentityTokenFor("Eerie Stone"), Is.EqualTo("EerieStone"));
            Assert.That(ExpandNullforge.Authoring.DimensionTilesetAsset.IdentityTokenFor("Eerie-Stone"), Is.EqualTo("EerieStone"));
            Assert.That(ExpandNullforge.Authoring.DimensionTilesetAsset.IdentityTokenFor("Eerie  Stone!"), Is.EqualTo("EerieStone"));
        }

        private static ExpandNullforge.Authoring.DimensionTilesetAsset MakeAsset(string blockName)
        {
            ExpandNullforge.Authoring.DimensionTilesetAsset asset =
                UnityEngine.ScriptableObject.CreateInstance<ExpandNullforge.Authoring.DimensionTilesetAsset>();
            SetName(asset, blockName);
            return asset;
        }

        private static void SetName(ExpandNullforge.Authoring.DimensionTilesetAsset asset, string blockName)
        {
            SetString(asset, "blockName", blockName);
        }

        private static void Freeze(ExpandNullforge.Authoring.DimensionTilesetAsset asset, string token)
        {
            SetString(asset, "identityToken", token);
        }

        private static void SetString(ExpandNullforge.Authoring.DimensionTilesetAsset asset, string field, string value)
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(asset);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<string> RealisticNames(int count)
        {
            int emitted = 0;
            for (int mod = 0; emitted < count; mod++)
            {
                for (int tileset = 0; tileset < 100 && emitted < count; tileset++)
                {
                    emitted++;
                    yield return "mod" + mod + ":tileset_" + tileset;
                }
            }
        }
    }
}
#endif
