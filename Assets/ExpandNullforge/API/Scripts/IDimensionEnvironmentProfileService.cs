using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionEnvironmentProfileService
    {
        event Action<DimensionEnvironmentProfileChangedEvent> EnvironmentProfileChanged;

        IReadOnlyList<DimensionEnvironmentProfile> GetEnvironmentProfiles(
            string dimensionId,
            string zoneId,
            bool includeDisabled);

        bool TryGetEnvironmentProfile(
            string profileId,
            out DimensionEnvironmentProfile profile);

        bool TryFindEnvironmentProfile(
            string dimensionId,
            string zoneId,
            out DimensionEnvironmentProfile profile);

        bool TryFindEnvironmentProfileAtLocal(
            string dimensionId,
            float2 localPosition,
            out DimensionEnvironmentProfile profile);

        DimensionEnvironmentProfileResolutionResult ResolveEnvironmentProfileAtLocal(
            string dimensionId,
            float2 localPosition);

        bool TryRegisterEnvironmentProfile(
            DimensionEnvironmentProfile profile,
            out DimensionOperationResult result);

        bool TryUpdateEnvironmentProfile(
            DimensionEnvironmentProfile profile,
            string reason,
            out DimensionOperationResult result);

        bool TrySetEnvironmentProfileEnabled(
            string profileId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveEnvironmentProfile(
            string profileId,
            out DimensionOperationResult result);
    }
}
