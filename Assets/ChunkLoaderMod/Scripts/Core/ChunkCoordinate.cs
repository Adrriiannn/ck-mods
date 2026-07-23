using System;
using Newtonsoft.Json;
using Unity.Mathematics;

[Serializable]
[JsonObject(MemberSerialization.OptIn)]
public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
{
  public ChunkCoordinate(int x, int y)
  {
    X = x;
    Y = y;
  }

  [JsonProperty]
  public int X { get; }

  [JsonProperty]
  public int Y { get; }

  [JsonIgnore]
  public int2 Index => new int2(X, Y);

  [JsonIgnore]
  public int2 Origin => new int2(X * ChunkLoaderConstants.ChunkSize, Y * ChunkLoaderConstants.ChunkSize);

  [JsonIgnore]
  public int2 Center => Origin + new int2(ChunkLoaderConstants.ChunkSize / 2);

  [JsonIgnore]
  public int2 OperationalOrigin => Origin - new int2(ChunkLoaderConstants.DependencyHalo);

  [JsonIgnore]
  public int2 ParentSubMapIndex => new int2(
      FloorDiv(Origin.x, ChunkLoaderConstants.ParentSubMapSize),
      FloorDiv(Origin.y, ChunkLoaderConstants.ParentSubMapSize));

  [JsonIgnore]
  public long ParentSubMapKey =>
      ((long)ParentSubMapIndex.x << 32) ^ (uint)ParentSubMapIndex.y;

  public static ChunkCoordinate FromWorldTile(int2 tile)
  {
    return new ChunkCoordinate(
        FloorDiv(tile.x, ChunkLoaderConstants.ChunkSize),
        FloorDiv(tile.y, ChunkLoaderConstants.ChunkSize));
  }

  public static ChunkCoordinate FromWorldPosition(float2 position)
  {
    return FromWorldTile((int2)math.floor(position));
  }

  public static ChunkCoordinate FromLegacy64Coordinate(int legacyX, int legacyY)
  {
    int2 legacyCenter =
        new int2(
            legacyX * ChunkLoaderConstants.LegacyChunkSize,
            legacyY * ChunkLoaderConstants.LegacyChunkSize) +
        new int2(ChunkLoaderConstants.LegacyChunkSize / 2);
    return FromWorldTile(legacyCenter);
  }

  public static int FloorDiv(int value, int divisor)
  {
    int quotient = value / divisor;
    int remainder = value % divisor;
    if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
    {
      quotient--;
    }

    return quotient;
  }

  public bool ContainsTile(int2 tile)
  {
    int2 origin = Origin;
    return tile.x >= origin.x &&
           tile.y >= origin.y &&
           tile.x < origin.x + ChunkLoaderConstants.ChunkSize &&
           tile.y < origin.y + ChunkLoaderConstants.ChunkSize;
  }

  public bool ContainsOperationalPosition(float2 position)
  {
    int2 origin = OperationalOrigin;
    return position.x >= origin.x &&
           position.y >= origin.y &&
           position.x < origin.x + ChunkLoaderConstants.OperationalSize &&
           position.y < origin.y + ChunkLoaderConstants.OperationalSize;
  }

  public long ToKey()
  {
    return ((long)X << 32) ^ (uint)Y;
  }

  public bool Equals(ChunkCoordinate other)
  {
    return X == other.X && Y == other.Y;
  }

  public override bool Equals(object obj)
  {
    return obj is ChunkCoordinate other && Equals(other);
  }

  public override int GetHashCode()
  {
    unchecked
    {
      return (X * 397) ^ Y;
    }
  }

  public override string ToString()
  {
    return $"({X},{Y})";
  }

  public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right)
  {
    return left.Equals(right);
  }

  public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right)
  {
    return !left.Equals(right);
  }
}
