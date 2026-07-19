namespace ExpandNullforge.Api
{
    /// <summary>
    /// Optional extension for generation providers that keep their own in-memory work queue.
    /// The dimension service calls this before it cancels or forgets a runtime generation job.
    /// </summary>
    public interface IDimensionGenerationControlProvider
    {
        bool TryCancelGeneration(
            DimensionDefinition dimension,
            DimensionArea area,
            string reason);
    }
}
