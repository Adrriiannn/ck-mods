using System;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Public entry point for dimension-aware mods.
    /// The runtime implementation is registered by the Dimension Framework provider, while dimension mods only consume the service.
    /// </summary>
    public static class DimensionApi
    {
        public const int CurrentApiVersion = 1;

        private static IDimensionService service;
        private static string providerId = string.Empty;

        public static bool HasService
        {
            get { return service != null; }
        }

        public static string ProviderId
        {
            get { return providerId; }
        }

        public static bool TryGetService(out IDimensionService dimensionService)
        {
            dimensionService = service;
            return dimensionService != null;
        }

        public static bool RegisterService(string newProviderId, IDimensionService dimensionService)
        {
            if (string.IsNullOrEmpty(newProviderId) || dimensionService == null)
            {
                return false;
            }

            if (service != null && !string.Equals(providerId, newProviderId, StringComparison.Ordinal))
            {
                return false;
            }

            providerId = newProviderId;
            service = dimensionService;
            return true;
        }

        public static bool UnregisterService(string existingProviderId)
        {
            if (service == null)
            {
                return true;
            }

            if (!string.Equals(providerId, existingProviderId, StringComparison.Ordinal))
            {
                return false;
            }

            providerId = string.Empty;
            service = null;
            return true;
        }
    }
}
