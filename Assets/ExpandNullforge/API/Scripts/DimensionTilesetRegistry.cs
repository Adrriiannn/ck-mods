using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Arbitrates tileset ownership between providers so Dimensions API and a third-party tileset
    /// mod can be installed at the same time without fighting over the tile registry.
    ///
    /// Consumers always call this registry, never a specific provider. Requests are routed to the
    /// highest-priority provider willing to take the tileset, and the winning owner is recorded so
    /// later release/resolve calls go back to the same provider. When no provider is installed the
    /// registry says so explicitly rather than silently dropping the tileset.
    /// </summary>
    public static class DimensionTilesetRegistry
    {
        /// <summary>Provider id reported when nothing can serve a request.</summary>
        public const string NoProviderId = "none";

        private static readonly object Gate = new object();

        private static readonly List<IDimensionTilesetProvider> Providers =
            new List<IDimensionTilesetProvider>();

        /// <summary>Tileset id to the provider id that accepted it.</summary>
        private static readonly Dictionary<string, string> Owners =
            new Dictionary<string, string>();

        /// <summary>Number of providers currently installed.</summary>
        public static int ProviderCount
        {
            get
            {
                lock (Gate)
                {
                    return Providers.Count;
                }
            }
        }

        /// <summary>
        /// Installs a provider. Providers are kept sorted by descending priority, so an explicit
        /// compatibility adapter can outrank the built-in framework provider. Re-registering the
        /// same provider id replaces the previous instance instead of duplicating it.
        /// </summary>
        public static bool RegisterProvider(IDimensionTilesetProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.ProviderId))
            {
                return false;
            }

            lock (Gate)
            {
                for (int i = 0; i < Providers.Count; i++)
                {
                    if (Providers[i].ProviderId == provider.ProviderId)
                    {
                        Providers[i] = provider;
                        SortProviders();
                        return true;
                    }
                }

                Providers.Add(provider);
                SortProviders();
                return true;
            }
        }

        /// <summary>
        /// Removes a provider and forgets the tilesets it owned, so those ids become available to
        /// another provider instead of being permanently stranded.
        /// </summary>
        public static bool UnregisterProvider(string providerId)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                return false;
            }

            lock (Gate)
            {
                bool removed = false;
                for (int i = Providers.Count - 1; i >= 0; i--)
                {
                    if (Providers[i].ProviderId == providerId)
                    {
                        Providers.RemoveAt(i);
                        removed = true;
                    }
                }

                if (!removed)
                {
                    return false;
                }

                List<string> orphaned = new List<string>();
                foreach (KeyValuePair<string, string> pair in Owners)
                {
                    if (pair.Value == providerId)
                    {
                        orphaned.Add(pair.Key);
                    }
                }

                for (int i = 0; i < orphaned.Count; i++)
                {
                    Owners.Remove(orphaned[i]);
                }

                return true;
            }
        }

        /// <summary>Ids of the installed providers, highest priority first.</summary>
        public static string[] GetProviderIds()
        {
            lock (Gate)
            {
                string[] ids = new string[Providers.Count];
                for (int i = 0; i < Providers.Count; i++)
                {
                    ids[i] = Providers[i].ProviderId;
                }

                return ids;
            }
        }

        /// <summary>
        /// Registers a tileset with the best available provider. A tileset already owned by a
        /// provider is routed back to that same owner, so a re-register updates in place rather
        /// than handing the id to a different mod mid-session.
        /// </summary>
        public static DimensionTilesetRegistrationResult Register(
            string contentPackId,
            string tilesetId)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return DimensionTilesetRegistrationResult.Failed(
                    NoProviderId, "invalid-id", "A tileset id is required.");
            }

            IDimensionTilesetProvider target = null;
            lock (Gate)
            {
                if (Owners.TryGetValue(tilesetId, out string ownerId))
                {
                    target = FindProvider(ownerId);
                }

                if (target == null)
                {
                    for (int i = 0; i < Providers.Count; i++)
                    {
                        if (Providers[i].CanRegister(tilesetId))
                        {
                            target = Providers[i];
                            break;
                        }
                    }
                }
            }

            if (target == null)
            {
                return DimensionTilesetRegistrationResult.Failed(
                    NoProviderId,
                    "no-provider",
                    "No tileset provider accepted '" + tilesetId +
                    "'. Install Dimensions API's tileset provider or a compatibility adapter.");
            }

            DimensionTilesetRegistrationResult result = target.Register(contentPackId, tilesetId);
            if (result.Accepted)
            {
                lock (Gate)
                {
                    Owners[tilesetId] = target.ProviderId;
                }
            }

            return result;
        }

        /// <summary>Releases a tileset through whichever provider owns it.</summary>
        public static bool Release(string tilesetId)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return false;
            }

            IDimensionTilesetProvider owner;
            lock (Gate)
            {
                if (!Owners.TryGetValue(tilesetId, out string ownerId))
                {
                    return false;
                }

                owner = FindProvider(ownerId);
            }

            bool released = owner != null && owner.Release(tilesetId);
            if (released)
            {
                lock (Gate)
                {
                    Owners.Remove(tilesetId);
                }
            }

            return released;
        }

        /// <summary>Resolves an authored tile to the runtime tile id its owning provider assigned.</summary>
        public static bool TryResolveRuntimeTileId(
            string tilesetId,
            string tileId,
            out int runtimeTileId)
        {
            runtimeTileId = 0;
            if (string.IsNullOrEmpty(tilesetId) || string.IsNullOrEmpty(tileId))
            {
                return false;
            }

            IDimensionTilesetProvider owner;
            lock (Gate)
            {
                owner = Owners.TryGetValue(tilesetId, out string ownerId)
                    ? FindProvider(ownerId)
                    : null;
            }

            return owner != null && owner.TryResolveRuntimeTileId(tilesetId, tileId, out runtimeTileId);
        }

        /// <summary>Reports which provider owns a tileset, for diagnostics and conflict reporting.</summary>
        public static bool TryGetOwner(string tilesetId, out string providerId)
        {
            providerId = NoProviderId;
            if (string.IsNullOrEmpty(tilesetId))
            {
                return false;
            }

            lock (Gate)
            {
                if (Owners.TryGetValue(tilesetId, out string ownerId))
                {
                    providerId = ownerId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drops every provider and ownership record. Used when tearing down the framework and by
        /// tests; it does not ask providers to release, since the caller is discarding them.
        /// </summary>
        public static void Clear()
        {
            lock (Gate)
            {
                Providers.Clear();
                Owners.Clear();
            }
        }

        private static IDimensionTilesetProvider FindProvider(string providerId)
        {
            for (int i = 0; i < Providers.Count; i++)
            {
                if (Providers[i].ProviderId == providerId)
                {
                    return Providers[i];
                }
            }

            return null;
        }

        private static void SortProviders()
        {
            Providers.Sort(ComparePriorityDescending);
        }

        private static int ComparePriorityDescending(
            IDimensionTilesetProvider left,
            IDimensionTilesetProvider right)
        {
            int byPriority = right.Priority.CompareTo(left.Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            // Stable, predictable order when priorities tie.
            return string.CompareOrdinal(left.ProviderId, right.ProviderId);
        }
    }
}
