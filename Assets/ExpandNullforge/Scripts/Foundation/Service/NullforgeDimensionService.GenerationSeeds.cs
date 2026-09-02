using ExpandNullforge.Api;
using ExpandNullforge.Persistence;

namespace ExpandNullforge.Foundation
{
  // The deterministic seed service. Generation providers consume the seeds it hands out.
  public sealed partial class NullforgeDimensionService
  {
    public uint ResolveGenerationSeed(DimensionGenerationSeedRequest request)
    {
      return ComputeGenerationSeed(request);
    }

    public int ResolveGenerationRange(
        DimensionGenerationSeedRequest request,
        int maxExclusive)
    {
      if (maxExclusive <= 1)
      {
        return 0;
      }

      return (int)(ResolveGenerationSeed(request) % (uint)maxExclusive);
    }

    public float ResolveGenerationUnitFloat(DimensionGenerationSeedRequest request)
    {
      uint seed = ResolveGenerationSeed(request);
      return (seed & 0x00ffffffu) / 16777216.0f;
    }

    private static uint ComputeGenerationSeed(DimensionGenerationSeedRequest request)
    {
      string worldKey = DimensionWorldRegistry.WorldKey;
      if (string.IsNullOrEmpty(worldKey))
      {
        DimensionWorldRegistry.EnsureLoadedForCurrentWorld();
        worldKey = DimensionWorldRegistry.WorldKey;
      }

      uint hash = StableHashOffset;
      hash = MixGenerationHash(hash, request.ExternalSeed);
      hash = MixGenerationHash(hash, worldKey);
      hash = MixGenerationHash(hash, request.DimensionId);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.Min.x);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.Min.y);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.MaxExclusive.x);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.MaxExclusive.y);
      hash = MixGenerationHash(hash, request.ProviderId);
      hash = MixGenerationHash(hash, request.PassId);
      hash = MixGenerationHash(hash, request.Purpose);
      hash = MixGenerationHash(hash, request.Salt);
      return hash == 0u ? StableHashPrime : hash;
    }

    private static uint MixGenerationHash(uint hash, uint value)
    {
      unchecked
      {
        hash ^= value & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 8) & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 16) & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 24) & 0xffu;
        hash *= StableHashPrime;
        return hash;
      }
    }

    private static uint MixGenerationHash(uint hash, string value)
    {
      if (string.IsNullOrEmpty(value))
      {
        return MixGenerationHash(hash, 0u);
      }

      unchecked
      {
        for (int i = 0; i < value.Length; i++)
        {
          hash ^= value[i];
          hash *= StableHashPrime;
        }

        return hash;
      }
    }
  }
}
