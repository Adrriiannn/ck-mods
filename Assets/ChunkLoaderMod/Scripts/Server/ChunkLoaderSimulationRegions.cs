using System.Collections.Generic;
using Unity.Mathematics;

public static class ChunkLoaderSimulationRegions
{
  private static readonly Dictionary<ulong, ChunkCoordinate> ActiveRegions = new();
  private static readonly Dictionary<long, ChunkCoordinate> ActiveRegionsByCoordinate = new();

  public static int Count => ActiveRegions.Count;
  private static bool UsesExactChunks => ChunkLoaderConstants.DependencyHalo == 0;

  public static void Reset()
  {
    ActiveRegions.Clear();
    ActiveRegionsByCoordinate.Clear();
  }

  public static void SetActive(ulong registrationId, ChunkCoordinate coordinate)
  {
    if (ActiveRegions.TryGetValue(
            registrationId,
            out ChunkCoordinate previous) &&
        previous != coordinate)
    {
      ActiveRegionsByCoordinate.Remove(previous.ToKey());
    }

    ActiveRegions[registrationId] = coordinate;
    ActiveRegionsByCoordinate[coordinate.ToKey()] = coordinate;
  }

  public static void SetInactive(ulong registrationId)
  {
    if (ActiveRegions.TryGetValue(registrationId, out ChunkCoordinate coordinate))
    {
      ActiveRegionsByCoordinate.Remove(coordinate.ToKey());
      ActiveRegions.Remove(registrationId);
    }
  }

  public static bool Contains(float2 position)
  {
    if (UsesExactChunks)
    {
      return ActiveRegionsByCoordinate.ContainsKey(
          ChunkCoordinate.FromWorldPosition(position).ToKey());
    }

    foreach (ChunkCoordinate coordinate in ActiveRegions.Values)
    {
      if (coordinate.ContainsOperationalPosition(position))
      {
        return true;
      }
    }

    return false;
  }

  public static bool ContainsCanonical(float2 position)
  {
    int2 tile = (int2)math.floor(position);
    if (UsesExactChunks)
    {
      return ActiveRegionsByCoordinate.ContainsKey(
          ChunkCoordinate.FromWorldTile(tile).ToKey());
    }

    foreach (ChunkCoordinate coordinate in ActiveRegions.Values)
    {
      if (coordinate.ContainsTile(tile))
      {
        return true;
      }
    }

    return false;
  }

  public static bool Intersects(int2 lowerCorner, int2 size)
  {
    if (size.x <= 0 || size.y <= 0 || ActiveRegionsByCoordinate.Count == 0)
    {
      return false;
    }

    if (UsesExactChunks)
    {
      int2 highInclusive = lowerCorner + size - new int2(1, 1);
      int minChunkX =
          ChunkCoordinate.FloorDiv(lowerCorner.x, ChunkLoaderConstants.ChunkSize);
      int minChunkY =
          ChunkCoordinate.FloorDiv(lowerCorner.y, ChunkLoaderConstants.ChunkSize);
      int maxChunkX =
          ChunkCoordinate.FloorDiv(highInclusive.x, ChunkLoaderConstants.ChunkSize);
      int maxChunkY =
          ChunkCoordinate.FloorDiv(highInclusive.y, ChunkLoaderConstants.ChunkSize);

      for (int y = minChunkY; y <= maxChunkY; y++)
      {
        for (int x = minChunkX; x <= maxChunkX; x++)
        {
          if (ActiveRegionsByCoordinate.ContainsKey(
                  new ChunkCoordinate(x, y).ToKey()))
          {
            return true;
          }
        }
      }

      return false;
    }

    int2 high = lowerCorner + size;
    foreach (ChunkCoordinate coordinate in ActiveRegions.Values)
    {
      int2 regionLow = coordinate.OperationalOrigin;
      int2 regionHigh = regionLow + new int2(ChunkLoaderConstants.OperationalSize);
      if (lowerCorner.x < regionHigh.x &&
          high.x > regionLow.x &&
          lowerCorner.y < regionHigh.y &&
          high.y > regionLow.y)
      {
        return true;
      }
    }

    return false;
  }

  public static void GetActiveCoordinates(List<ChunkCoordinate> destination)
  {
    destination.Clear();
    destination.AddRange(ActiveRegions.Values);
  }
}
