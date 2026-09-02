namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelLoopCheck
    {
        public readonly DimensionTravelLoopCheckKind Kind;
        public readonly string SubjectId;
        public readonly bool Passed;
        public readonly string Code;
        public readonly string Message;

        public DimensionTravelLoopCheck(
            DimensionTravelLoopCheckKind kind,
            string subjectId,
            bool passed,
            string code,
            string message)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            Passed = passed;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
