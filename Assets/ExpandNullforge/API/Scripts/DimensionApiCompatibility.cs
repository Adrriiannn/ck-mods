namespace ExpandNullforge.Api
{
    public static class DimensionApiCompatibility
    {
        public const int MinimumSupportedApiVersion = 1;

        public const string Policy =
            "Dimension Framework API v1 is pre-1.0 but versioned. " +
            "Additive public interfaces keep the current API version. " +
            "Breaking removals or signature changes require a new API version.";

        public static bool IsApiVersionSupported(int requiredApiVersion)
        {
            return requiredApiVersion >= MinimumSupportedApiVersion &&
                   requiredApiVersion <= DimensionApi.CurrentApiVersion;
        }
    }
}
