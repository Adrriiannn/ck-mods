using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentReadinessResult
    {
        public readonly bool Ready;
        public readonly string Code;
        public readonly string Message;
        public readonly DimensionContentReadinessRequest Request;
        public readonly IReadOnlyList<DimensionContentReadinessIssue> Issues;

        public DimensionContentReadinessResult(
            bool ready,
            string code,
            string message,
            DimensionContentReadinessRequest request,
            IReadOnlyList<DimensionContentReadinessIssue> issues)
        {
            Ready = ready;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Request = request;
            Issues = issues ?? new List<DimensionContentReadinessIssue>();
        }
    }
}
