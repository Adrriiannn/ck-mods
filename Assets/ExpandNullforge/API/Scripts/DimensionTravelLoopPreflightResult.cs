using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelLoopPreflightResult
    {
        public readonly bool Ready;
        public readonly string Code;
        public readonly string Message;
        public readonly DimensionTravelLoopPreflightRequest Request;
        public readonly IReadOnlyList<DimensionTravelLoopCheck> Checks;
        public readonly bool HasGenerationStatus;
        public readonly DimensionGenerationStatus GenerationStatus;

        public DimensionTravelLoopPreflightResult(
            bool ready,
            string code,
            string message,
            DimensionTravelLoopPreflightRequest request,
            IReadOnlyList<DimensionTravelLoopCheck> checks,
            bool hasGenerationStatus,
            DimensionGenerationStatus generationStatus)
        {
            Ready = ready;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Request = request;
            Checks = checks ?? new List<DimensionTravelLoopCheck>();
            HasGenerationStatus = hasGenerationStatus;
            GenerationStatus = generationStatus;
        }
    }
}
