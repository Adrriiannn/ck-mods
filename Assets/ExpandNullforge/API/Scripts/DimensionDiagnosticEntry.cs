namespace ExpandNullforge.Api
{
    public readonly struct DimensionDiagnosticEntry
    {
        public readonly double TimeSeconds;
        public readonly DimensionDiagnosticSeverity Severity;
        public readonly string DimensionId;
        public readonly string Message;

        public DimensionDiagnosticEntry(
            double timeSeconds,
            DimensionDiagnosticSeverity severity,
            string dimensionId,
            string message)
        {
            TimeSeconds = timeSeconds;
            Severity = severity;
            DimensionId = dimensionId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
