using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionAccessService
    {
        bool TryRegisterAccessProvider(
            IDimensionAccessProvider provider,
            out DimensionOperationResult result);

        bool TryRemoveAccessProvider(string providerId, out DimensionOperationResult result);

        IReadOnlyList<string> GetAccessProviderIds();
    }
}
