namespace ExpandNullforge.Api
{
    public interface IDimensionContentManifestService
    {
        DimensionContentManifestResult ValidateContentManifest(
            DimensionContentManifestRequest request);

        DimensionContentManifestResult TryApplyContentManifest(
            DimensionContentManifestRequest request);
    }
}
