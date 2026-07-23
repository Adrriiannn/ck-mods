public static class ChunkLoaderConstants
{
  public const int RegistrySchemaVersion = 2;
  public const int LegacyChunkSize = 64;
  public const int ChunkSize = 16;
  public const int ParentSubMapSize = 64;
  public const int ParentSubMapChunksPerAxis = ParentSubMapSize / ChunkSize;
  public const int DependencyHalo = 0;
  public const int OperationalSize = ChunkSize + DependencyHalo * 2;

  public const float ResidencyRadius = 12.0f;
  public const float RegistryFlushDelaySeconds = 0.50f;
  public const float RegistryFlushFailureInitialRetrySeconds = 5.0f;
  public const float RegistryFlushFailureMaximumRetrySeconds = 60.0f;
  public const float RegistryFlushFailureLogIntervalSeconds = 60.0f;
  public const float RuntimeReconcileIntervalSeconds = 0.20f;
  public const float RuntimeLoadedStabilizationSeconds = 1.00f;
  public const float RuntimeLoadTimeoutSeconds = 30.0f;
  public const float LiveDetailsClientIntervalSeconds = 1.00f;

  public const int ActivitySessionEventLimit = 4096;
  public const int ActivityDetailsLineLimit = 256;

  public const int UnlimitedLimit = int.MaxValue;
  public const int DefaultPersonalActiveLimit = UnlimitedLimit;
  public const int DefaultWorldActiveLimit = UnlimitedLimit;
  public const int DefaultPersonalSavedLimit = UnlimitedLimit;
  public const int AbsoluteWorldActiveLimit = UnlimitedLimit;

  public const int MaxNameCharacters = 40;
  public const string WorldNamespaceVanilla = "vanilla";
  public const string DefaultNamePrefix = "Loaded chunk ";
}
