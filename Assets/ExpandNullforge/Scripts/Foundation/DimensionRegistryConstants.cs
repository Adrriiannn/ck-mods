namespace ExpandNullforge.Foundation
{
  public static class DimensionRegistryConstants
  {
    public const int RegistrySchemaVersion = 4;
    public const int RegistryMinimumReadableSchemaVersion = 1;
    public const string FilePrefix = "ExpandNullforge_world_";
    public const string FileSuffixA = "_dimensions_A.json";
    public const string FileSuffixB = "_dimensions_B.json";
    public const float FlushDelaySeconds = 2.50f;
    public const float FlushFailureInitialRetrySeconds = 5.0f;
    public const float FlushFailureMaximumRetrySeconds = 60.0f;
    public const float FlushFailureLogIntervalSeconds = 60.0f;
  }
}
