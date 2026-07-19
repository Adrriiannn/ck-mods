namespace ExpandNullforge.Api
{
    public interface IDimensionMapPresentationService
    {
        DimensionMapPresentationSnapshot GetMapPresentation(DimensionMapPresentationRequest request);
    }
}
