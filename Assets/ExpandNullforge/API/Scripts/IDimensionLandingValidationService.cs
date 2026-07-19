namespace ExpandNullforge.Api
{
    public interface IDimensionLandingValidationService
    {
        DimensionLandingValidationResult ValidateLandingTarget(
            DimensionLandingValidationRequest request);
    }
}
