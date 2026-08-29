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
    /// <remarks>
    /// THE SUBJECT HAS NO PRODUCTION CALLER. Nothing in the framework registers a provider or
    /// routes tileset resolution through this registry, and <c>IDimensionTilesetProvider</c> has no
    /// implementations — the framework's own tilesets go the other road entirely. These tests are
    /// kept because they are the only proof the published offer works for another mod author; read
    /// them as a specification, not as evidence that the surface is live.
    /// </remarks>
    internal sealed class DimensionTilesetProviderRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            DimensionTilesetProviderRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DimensionTilesetProviderRegistry.Clear();
        }

        [Test]
        public void WithNoProvider_RegistrationFailsLoudly()
        {
            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo("no-provider"));
            Assert.That(result.Message, Does.Contain("pack:caves"));
        }

        [Test]
        public void HighestPriorityWillingProvider_WinsTheTileset()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            FakeProvider adapter = new FakeProvider("other-mod-adapter", 10);
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.RegisterProvider(adapter);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

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
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.RegisterProvider(picky);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void AnOwnedTileset_StaysWithItsOriginalOwner()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

            // A higher-priority provider arrives later; it must not steal a live tileset.
            FakeProvider adapter = new FakeProvider("late-adapter", 99);
            DimensionTilesetProviderRegistry.RegisterProvider(adapter);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
            Assert.That(adapter.Registered, Is.Empty);

            Assert.That(DimensionTilesetProviderRegistry.TryGetOwner("pack:caves", out string owner), Is.True);
            Assert.That(owner, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void ReleasingATileset_ReturnsTheIdToThePool()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.Register("pack", "pack:caves");

            Assert.That(DimensionTilesetProviderRegistry.Release("pack:caves"), Is.True);
            Assert.That(DimensionTilesetProviderRegistry.TryGetOwner("pack:caves", out _), Is.False);

            // Now a higher-priority provider may claim it.
            FakeProvider adapter = new FakeProvider("late-adapter", 99);
            DimensionTilesetProviderRegistry.RegisterProvider(adapter);
            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");
            Assert.That(result.ProviderId, Is.EqualTo("late-adapter"));
        }

        [Test]
        public void UninstallingAProvider_FreesItsTilesetsInsteadOfStrandingThem()
        {
            FakeProvider adapter = new FakeProvider("other-mod-adapter", 10);
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetProviderRegistry.RegisterProvider(adapter);
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.Register("pack", "pack:caves");
            Assert.That(DimensionTilesetProviderRegistry.TryGetOwner("pack:caves", out string first), Is.True);
            Assert.That(first, Is.EqualTo("other-mod-adapter"));

            Assert.That(DimensionTilesetProviderRegistry.UnregisterProvider("other-mod-adapter"), Is.True);
            Assert.That(DimensionTilesetProviderRegistry.TryGetOwner("pack:caves", out _), Is.False);

            DimensionTilesetRegistrationResult result =
                DimensionTilesetProviderRegistry.Register("pack", "pack:caves");
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.ProviderId, Is.EqualTo("dimensions-api"));
        }

        [Test]
        public void ReRegisteringTheSameProviderId_ReplacesRatherThanDuplicates()
        {
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("dimensions-api", 0));
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("dimensions-api", 5));

            Assert.That(DimensionTilesetProviderRegistry.ProviderCount, Is.EqualTo(1));
        }

        [Test]
        public void ProvidersAreOrderedByDescendingPriority()
        {
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("low", 0));
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("high", 50));
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("mid", 10));

            Assert.That(
                DimensionTilesetProviderRegistry.GetProviderIds(),
                Is.EqualTo(new[] { "high", "mid", "low" }));
        }

        [Test]
        public void RuntimeTileIds_ResolveThroughTheOwningProvider()
        {
            FakeProvider framework = new FakeProvider("dimensions-api", 0);
            DimensionTilesetProviderRegistry.RegisterProvider(framework);
            DimensionTilesetProviderRegistry.Register("pack", "pack:caves");
            framework.TileIds["pack:caves/floor"] = 4242;

            Assert.That(
                DimensionTilesetProviderRegistry.TryResolveRuntimeTileId(
                    "pack:caves", "floor", out int runtimeId),
                Is.True);
            Assert.That(runtimeId, Is.EqualTo(4242));

            Assert.That(
                DimensionTilesetProviderRegistry.TryResolveRuntimeTileId(
                    "pack:caves", "missing", out _),
                Is.False);
        }

        [Test]
        public void UnknownTileset_ResolvesToNothingRatherThanGuessing()
        {
            DimensionTilesetProviderRegistry.RegisterProvider(new FakeProvider("dimensions-api", 0));

            Assert.That(
                DimensionTilesetProviderRegistry.TryResolveRuntimeTileId("nope", "floor", out int id),
                Is.False);
            Assert.That(id, Is.EqualTo(0));
            Assert.That(DimensionTilesetProviderRegistry.Release("nope"), Is.False);
        }

        [Test]
        public void NullAndEmptyInputs_AreRejectedNotCrashed()
        {
            Assert.That(DimensionTilesetProviderRegistry.RegisterProvider(null), Is.False);
            Assert.That(DimensionTilesetProviderRegistry.UnregisterProvider(string.Empty), Is.False);
            Assert.That(DimensionTilesetProviderRegistry.Register("pack", string.Empty).Accepted, Is.False);
            Assert.That(DimensionTilesetProviderRegistry.Release(null), Is.False);
            Assert.That(DimensionTilesetProviderRegistry.TryGetOwner(null, out _), Is.False);
            Assert.That(
                DimensionTilesetProviderRegistry.TryResolveRuntimeTileId(null, null, out _), Is.False);
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
