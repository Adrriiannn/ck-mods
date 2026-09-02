namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPassContext
    {
        public readonly DimensionGenerationContext GenerationContext;
        public readonly DimensionGenerationPassDefinition Pass;
        public readonly int PassIndex;
        public readonly int PassCount;

        public DimensionGenerationPassContext(
            DimensionGenerationContext generationContext,
            DimensionGenerationPassDefinition pass,
            int passIndex,
            int passCount)
        {
            GenerationContext = generationContext;
            Pass = pass;
            PassIndex = passIndex;
            PassCount = passCount;
        }
    }
}
