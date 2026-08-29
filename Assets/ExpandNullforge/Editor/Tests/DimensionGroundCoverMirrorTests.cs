#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The ground-cover mirror: the parallel primitive arrays the scatter reads in the game.
    /// </summary>
    /// <remarks>
    /// The list of layer configs on the asset is editor truth and does not survive Core Keeper's
    /// load-time recompile — a serialized List of a custom class comes back empty — which is why
    /// ticking "grass tufts" grew nothing. The arrays are the shape that crosses, so what goes
    /// into them, and what is left out of them, is the whole feature.
    /// </remarks>
    internal sealed class DimensionGroundCoverMirrorTests
    {
        private DimensionTilesetAsset tileset;

        [SetUp]
        public void CreateTileset()
        {
            tileset = ScriptableObject.CreateInstance<DimensionTilesetAsset>();
        }

        [TearDown]
        public void DestroyTileset()
        {
            if (tileset != null)
            {
                Object.DestroyImmediate(tileset);
            }
        }

        [Test]
        public void ANewTilesetMirrorsNoCover()
        {
            Assert.That(tileset.CoverCount, Is.EqualTo(0));
        }

        [Test]
        public void EnabledCoverWithDensityIsKept_InOrder()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 0.25f),
                Layer("pebbles", true, 0.5f),
                Layer("roots", true, 0.1f)
            });

            Assert.That(tileset.CoverCount, Is.EqualTo(3));

            string key;
            float density;
            Assert.That(tileset.TryGetCover(0, out key, out density), Is.True);
            Assert.That(key, Is.EqualTo("grass"));
            Assert.That(density, Is.EqualTo(0.25f).Within(0.0001f));

            Assert.That(tileset.TryGetCover(2, out key, out density), Is.True);
            Assert.That(key, Is.EqualTo("roots"));
            Assert.That(density, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void CoverThatIsSwitchedOffIsLeftOut()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", false, 0.5f)
            });

            Assert.That(tileset.CoverCount, Is.EqualTo(0));
        }

        [Test]
        public void CoverWithNoDensityIsLeftOut()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 0f)
            });

            Assert.That(tileset.CoverCount, Is.EqualTo(0));
        }

        /// <summary>
        /// A state that is not one of the four scatterable overlays cannot be grown by generation —
        /// tilled soil is made by a hoe. Storing it would put a row in the bundle that the runtime
        /// would then have to throw away.
        /// </summary>
        [Test]
        public void AStateThatIsNotScatterableIsLeftOut()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("tilled", true, 0.5f),
                Layer("slime", true, 0.5f)
            });

            Assert.That(tileset.CoverCount, Is.EqualTo(1));

            string key;
            float density;
            tileset.TryGetCover(0, out key, out density);
            Assert.That(key, Is.EqualTo("slime"));
        }

        [Test]
        public void DensityAboveOneIsBroughtBackToOne()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 2.5f)
            });

            string key;
            float density;
            tileset.TryGetCover(0, out key, out density);
            Assert.That(density, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// The author removed the cover, so the arrays must forget it. Without this a block keeps
        /// growing the grass of a build ago, and nothing in the project still asks for it.
        /// </summary>
        [Test]
        public void RemovingEveryCoverLayerClearsTheArrays()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 0.5f)
            });
            Assert.That(tileset.CoverCount, Is.EqualTo(1));

            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>());
            Assert.That(tileset.CoverCount, Is.EqualTo(0));
        }

        [Test]
        public void PassingNothingAtAllClearsTheArrays()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 0.5f)
            });

            tileset.EditorSetGroundCover(null);
            Assert.That(tileset.CoverCount, Is.EqualTo(0));
        }

        [Test]
        public void AnIndexOutsideTheArraysAnswersFalseInsteadOfThrowing()
        {
            tileset.EditorSetGroundCover(new List<DimensionTilesetLayerConfig>
            {
                Layer("grass", true, 0.5f)
            });

            string key;
            float density;
            Assert.That(tileset.TryGetCover(-1, out key, out density), Is.False);
            Assert.That(tileset.TryGetCover(1, out key, out density), Is.False);
        }

        private static DimensionTilesetLayerConfig Layer(string key, bool enabled, float density)
        {
            return new DimensionTilesetLayerConfig
            {
                key = key,
                enabled = enabled,
                density = density
            };
        }
    }
}
#endif
