using Unity.Entities;

public struct ChunkLoaderRuntimeAnchorCD : IComponentData
{
  public ulong RegistrationId;
  public int ChunkX;
  public int ChunkY;
  public double CreatedAt;
  public double SubMapObservedAt;
  public byte ImmediateLoadEnabled;
}

public struct ChunkLoaderSimulationRegionCD : IComponentData
{
  public ulong RegistrationId;
  public int ChunkX;
  public int ChunkY;
}

public struct ChunkLoaderMergedSimulationRegionCD : IComponentData
{
  public int LowX;
  public int LowY;
  public int SizeX;
  public int SizeY;
}

public struct ChunkLoaderTemporarilyRelaxedSpawnerCD : IComponentData
{
}
