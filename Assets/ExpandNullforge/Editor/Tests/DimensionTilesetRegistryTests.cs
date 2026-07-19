#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves two tileset mods can coexist: the highest-priority provider wins a fresh id, an
    /// already-owned tileset keeps going back to its original owner, and uninstalling a provider
    /// frees its ids instead of stranding them.
    /// </summary>
    internal sealed class DimensionTilesetRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            DimensionTilesetRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DimensionTilesetRegistry.Clear();
        }

        [Test]
        public void WithNoProvider_RegistrationFailsLoudly()
        {
            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo("no-provider"));
            Assert.That(result.Message, Does.Contain("pack:caves"));
        }

        [Test]
        public void HighestPriorityWillingProvider_WinsTheTileset()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            FakeProvider adapter = new FakeProvider("other-mod-adapter", 10);
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.RegisterProvider(adapter);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.ProviderId, Is.EqualTo("other-mod-adapter"));
            Assert.That(adapter.Registered, Does.Contain("pack:caves"));
            Assert.That(framework.Registered, Is.Empty);
        }

        [Test]
        public void WhenTopProviderDeclines_TheNextOneTakesIt()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            FakeProvider picky = new FakeProvider("picky", 10) { AcceptsAnything = false };
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.RegisterProvider(picky);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void AnOwnedTileset_StaysWithItsOriginalOwner()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.Register("pack", "pack:caves");

            // A higher-priority provider arrives later; it must not steal a live tileset.
            FakeProvider adapter = new FakeProvider("late-adapter", 99);
            DimensionTilesetRegistry.RegisterProvider(adapter);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");

            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
            Assert.That(adapter.Registered, Is.Empty);

            Assert.That(DimensionTilesetRegistry.TryGetOwner("pack:caves", out string owner), Is.True);
            Assert.That(owner, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void ReleasingATileset_ReturnsTheIdToThePool()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.Register("pack", "pack:caves");

            Assert.That(DimensionTilesetRegistry.Release("pack:caves"), Is.True);
            Assert.That(DimensionTilesetRegistry.TryGetOwner("pack:caves", out _), Is.False);

            // Now a higher-priority provider may claim it.
            FakeProvider adapter = new FakeProvider("late-adapter", 99);
            DimensionTilesetRegistry.RegisterProvider(adapter);
            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");
            Assert.That(result.ProviderId, Is.EqualTo("late-adapter"));
        }

        [Test]
        public void UninstallingAProvider_FreesItsTilesetsInsteadOfStrandingThem()
        {
            FakeProvider adapter = new FakeProvider("other-mod-adapter", 10);
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetRegistry.RegisterProvider(adapter);
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.Register("pack", "pack:caves");
            Assert.That(DimensionTilesetRegistry.TryGetOwner("pack:caves", out string first), Is.True);
            Assert.That(first, Is.EqualTo("other-mod-adapter"));

            Assert.That(DimensionTilesetRegistry.UnregisterProvider("other-mod-adapter"), Is.True);
            Assert.That(DimensionTilesetRegistry.TryGetOwner("pack:caves", out _), Is.False);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetRegistry.Register("pack", "pack:caves");
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void ReRegisteringTheSameProviderId_ReplacesRatherThanDuplicates()
        {
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("dimensions-api", 0));
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("dimensions-api", 5));

            Assert.That(DimensionTilesetRegistry.ProviderCount, Is.EqualTo(1));
        }

        [Test]
        public void ProvidersAreOrderedByDescendingPriority()
        {
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("low", 0));
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("high", 50));
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("mid", 10));

            Assert.That(
                DimensionTilesetRegistry.GetProviderIds(),
                Is.EqualTo(new[] { "high", "mid", "low" }));
        }

        [Test]
        public void RuntimeTileIds_ResolveThroughTheOwningProvider()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetRegistry.RegisterProvider(framework);
            DimensionTilesetRegistry.Register("pack", "pack:caves");
            framework.TileIds["pack:caves/floor"] = 4242;

            Assert.That(
                DimensionTilesetRegistry.TryResolveRuntimeTileId(
                    "pack:caves", "floor", out int runtimeId),
                Is.True);
            Assert.That(runtimeId, Is.EqualTo(4242));

            Assert.That(
                DimensionTilesetRegistry.TryResolveRuntimeTileId(
                    "pack:caves", "missing", out _),
                Is.False);
        }

        [Test]
        public void UnknownTileset_ResolvesToNothingRatherThanGuessing()
        {
            DimensionTilesetRegistry.RegisterProvider(new FakeProvider("dimensions-api", 0));

            Assert.That(
                DimensionTilesetRegistry.TryResolveRuntimeTileId("nope", "floor", out int id),
                Is.False);
            Assert.That(id, Is.EqualTo(0));
            Assert.That(DimensionTilesetRegistry.Release("nope"), Is.False);
        }

        [Test]
        public void NullAndEmptyInputs_AreRejectedNotCrashed()
        {
            Assert.That(DimensionTilesetRegistry.RegisterProvider(null), Is.False);
            Assert.That(DimensionTilesetRegistry.UnregisterProvider(string.Empty), Is.False);
            Assert.That(DimensionTilesetRegistry.Register("pack", string.Empty).Accepted, Is.False);
            Assert.That(DimensionTilesetRegistry.Release(null), Is.False);
            Assert.That(DimensionTilesetRegistry.TryGetOwner(null, out _), Is.False);
            Assert.That(
                DimensionTilesetRegistry.TryResolveRuntimeTileId(null, null, out _), Is.False);
        }

        private sealed class FakeProvider : IDimensionTilesetProvider
        {
            public FakeProvider(string providerId, int priority)
            {
                ProviderId = providerId;
                Priority = priority;
            }

            public string ProviderId { get; }

            public int Priority { get; }

            public bool AcceptsAnything { get; set; } = true;

            public List<string> Registered { get; } = new List<string>();

            public Dictionary<string, int> TileIds { get; } = new Dictionary<string, int>();

            public bool CanRegister(string tilesetId)
            {
                return AcceptsAnything;
            }

            public DimensionTilesetRegistrationResult Register(
                string contentPackId,
                string tilesetId)
            {
                if (!AcceptsAnything)
                {
                    return DimensionTilesetRegistrationResult.Failed(
                        ProviderId, "declined", "declined");
                }

                if (!Registered.Contains(tilesetId))
                {
                    Registered.Add(tilesetId);
                }

                return DimensionTilesetRegistrationResult.Success(ProviderId, "registered");
            }

            public bool Release(string tilesetId)
            {
                return Registered.Remove(tilesetId);
            }

            public bool TryResolveRuntimeTileId(
                string tilesetId,
                string tileId,
                out int runtimeTileId)
            {
                return TileIds.TryGetValue(tilesetId + "/" + tileId, out runtimeTileId);
            }
        }
    }
}
#endif
