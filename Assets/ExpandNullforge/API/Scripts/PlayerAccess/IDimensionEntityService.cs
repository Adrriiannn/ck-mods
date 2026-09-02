using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionEntityService
    {
        bool TryGetEntityContext(Entity entity, out DimensionContext context);

        bool TryGetEntityContext(
            Entity entity,
            DimensionEntityWorldScope worldScope,
            out DimensionContext context);

        DimensionResolveResult ResolveEntityLocalTarget(Entity entity, float2 localPosition);

        DimensionResolveResult ResolveEntityLocalTarget(
            Entity entity,
            DimensionEntityWorldScope worldScope,
            float2 localPosition);
    }
}
