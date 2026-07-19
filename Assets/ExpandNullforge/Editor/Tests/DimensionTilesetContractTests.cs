#if UNITY_INCLUDE_TESTS
using System;
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Locks the tile-role contract: picking a role must decide the tile's behaviour, because the
    /// generator, the validator, and the authoring UI all read these rules. If a role's defaults
    /// drift, a creator's floor could become solid or a wall could stop dropping resources.
    /// </summary>
    internal sealed class DimensionTilesetContractTests
    {
        [Test]
        public void Ground_IsWalkableAndNotSolid()
        {
            Assert.That(
                DimensionTileRoleRules.Requires(
                    DimensionTileRole.Ground, DimensionTileCapabilities.Walkable),
                Is.True);
            Assert.That(DimensionTileRoleRules.IsSolid(DimensionTileRole.Ground), Is.False);
        }

        [Test]
        public void WallsAndVeins_AreSolidDiggableAndDrop()
        {
            DimensionTileRole[] mineable =
            {
                DimensionTileRole.Wall,
                DimensionTileRole.Vein
            };

            for (int i = 0; i < mineable.Length; i++)
            {
                DimensionTileRole role = mineable[i];
                Assert.That(DimensionTileRoleRules.IsSolid(role), Is.True, role.ToString());
                Assert.That(
                    DimensionTileRoleRules.Requires(role, DimensionTileCapabilities.Diggable),
                    Is.True,
                    role.ToString());
                Assert.That(
                    DimensionTileRoleRules.RequiresLootTable(role),
                    Is.True,
                    role.ToString());
            }
        }

        [Test]
        public void Liquid_IsSwimmableAndNotWalkable()
        {
            Assert.That(
                DimensionTileRoleRules.Requires(
                    DimensionTileRole.Liquid, DimensionTileCapabilities.Swimmable),
                Is.True);
            Assert.That(
                DimensionTileRoleRules.Requires(
                    DimensionTileRole.Liquid, DimensionTileCapabilities.Walkable),
                Is.False);
        }

        [Test]
        public void Decoration_CarriesNoBehaviour()
        {
            Assert.That(
                DimensionTileRoleRules.GetDefaultCapabilities(DimensionTileRole.Decoration),
                Is.EqualTo(DimensionTileCapabilities.None));
            Assert.That(DimensionTileRoleRules.IsSolid(DimensionTileRole.Decoration), Is.False);
        }

        [Test]
        public void LootTableRequirement_TracksTheDropsCapability()
        {
            foreach (DimensionTileRole role in Enum.GetValues(typeof(DimensionTileRole)))
            {
                bool drops = DimensionTileRoleRules.Requires(
                    role, DimensionTileCapabilities.Drops);
                Assert.That(
                    DimensionTileRoleRules.RequiresLootTable(role),
                    Is.EqualTo(drops),
                    role.ToString());
            }
        }

        [Test]
        public void EveryRole_HasAReadableLabel()
        {
            foreach (DimensionTileRole role in Enum.GetValues(typeof(DimensionTileRole)))
            {
                string label = DimensionTileRoleRules.Describe(role);
                Assert.That(string.IsNullOrEmpty(label), Is.False, role.ToString());
            }
        }

        [Test]
        public void RegistrationResult_ReportsOwnershipAndFailureReason()
        {
            DimensionTilesetRegistrationResult ok =
                DimensionTilesetRegistrationResult.Success("dimensions-api", "registered");
            Assert.That(ok.Accepted, Is.True);
            Assert.That(ok.ProviderId, Is.EqualTo("dimensions-api"));

            DimensionTilesetRegistrationResult failed =
                DimensionTilesetRegistrationResult.Failed(
                    "other-tileset-mod", "id-taken", "another mod owns this tileset id");
            Assert.That(failed.Accepted, Is.False);
            Assert.That(failed.Code, Is.EqualTo("id-taken"));
            Assert.That(failed.ProviderId, Is.EqualTo("other-tileset-mod"));
            Assert.That(failed.Message, Is.Not.Empty);
        }

        [Test]
        public void RegistrationResult_NeverExposesNullStrings()
        {
            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistrationResult.Failed(null, null, null);
            Assert.That(result.ProviderId, Is.EqualTo(string.Empty));
            Assert.That(result.Code, Is.EqualTo(string.Empty));
            Assert.That(result.Message, Is.EqualTo(string.Empty));
        }
    }
}
#endif
