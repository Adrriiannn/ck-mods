using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelFeedbackSnapshot
    {
        public readonly bool HasSnapshot;
        public readonly bool IsActive;
        public readonly bool IsTerminal;
        public readonly uint RequestId;
        public readonly uint CancelRequestId;
        public readonly DimensionTravelFeedbackPhase Phase;
        public readonly bool IsPortalRequest;
        public readonly string PortalId;
        public readonly string TravelId;
        public readonly string LoadTicketId;
        public readonly string TargetDimensionId;
        public readonly float2 TargetLocalPosition;
        public readonly float2 TargetAbsolutePosition;
        public readonly string Code;
        public readonly string Message;
        public readonly string Reason;
        public readonly double StartedAt;
        public readonly double UpdatedAt;

        public DimensionTravelFeedbackSnapshot(
            bool hasSnapshot,
            bool isActive,
            bool isTerminal,
            uint requestId,
            uint cancelRequestId,
            DimensionTravelFeedbackPhase phase,
            bool isPortalRequest,
            string portalId,
            string travelId,
            string loadTicketId,
            string targetDimensionId,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition,
            string code,
            string message,
            string reason,
            double startedAt,
            double updatedAt)
        {
            HasSnapshot = hasSnapshot;
            IsActive = isActive;
            IsTerminal = isTerminal;
            RequestId = requestId;
            CancelRequestId = cancelRequestId;
            Phase = phase;
            IsPortalRequest = isPortalRequest;
            PortalId = portalId ?? string.Empty;
            TravelId = travelId ?? string.Empty;
            LoadTicketId = loadTicketId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            TargetLocalPosition = targetLocalPosition;
            TargetAbsolutePosition = targetAbsolutePosition;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Reason = reason ?? string.Empty;
            StartedAt = startedAt;
            UpdatedAt = updatedAt;
        }

        public static DimensionTravelFeedbackSnapshot Idle(double now)
        {
            return new DimensionTravelFeedbackSnapshot(
                false,
                false,
                false,
                0,
                0,
                DimensionTravelFeedbackPhase.Idle,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                default(float2),
                default(float2),
                string.Empty,
                string.Empty,
                string.Empty,
                now,
                now);
        }
    }
}
