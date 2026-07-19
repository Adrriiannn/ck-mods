using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionSlotAllocationResult
    {
        public readonly bool Accepted;
        public readonly string Code;
        public readonly string Message;
        public readonly int2 AbsoluteOrigin;
        public readonly int CandidateIndex;
        public readonly bool UsedFixedOrigin;

        public DimensionSlotAllocationResult(
            bool accepted,
            string code,
            string message,
            int2 absoluteOrigin,
            int candidateIndex,
            bool usedFixedOrigin)
        {
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AbsoluteOrigin = absoluteOrigin;
            CandidateIndex = candidateIndex;
            UsedFixedOrigin = usedFixedOrigin;
        }

        public static DimensionSlotAllocationResult Success(
            int2 absoluteOrigin,
            int candidateIndex,
            bool usedFixedOrigin,
            string message)
        {
            return new DimensionSlotAllocationResult(
                true,
                "ok",
                message,
                absoluteOrigin,
                candidateIndex,
                usedFixedOrigin);
        }

        public static DimensionSlotAllocationResult Failed(
            string code,
            string message)
        {
            return new DimensionSlotAllocationResult(
                false,
                code,
                message,
                default,
                -1,
                false);
        }
    }
}
