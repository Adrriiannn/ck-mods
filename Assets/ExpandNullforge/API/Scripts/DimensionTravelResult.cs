using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelResult
    {
        public readonly bool Accepted;
        public readonly string Code;
        public readonly string Message;
        public readonly string TravelId;
        public readonly string LoadTicketId;
        public readonly string TargetDimensionId;
        public readonly float2 TargetLocalPosition;
        public readonly float2 TargetAbsolutePosition;

        public DimensionTravelResult(
            bool accepted,
            string code,
            string message,
            string travelId,
            string loadTicketId,
            string targetDimensionId,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition)
        {
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            TravelId = travelId ?? string.Empty;
            LoadTicketId = loadTicketId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            TargetLocalPosition = targetLocalPosition;
            TargetAbsolutePosition = targetAbsolutePosition;
        }

        public static DimensionTravelResult Failed(string code, string message)
        {
            return new DimensionTravelResult(
                false,
                code,
                message,
                string.Empty,
                string.Empty,
                string.Empty,
                default(float2),
                default(float2));
        }

        public static DimensionTravelResult AcceptedRequest(
            string travelId,
            string loadTicketId,
            string targetDimensionId,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition)
        {
            return new DimensionTravelResult(
                true,
                string.Empty,
                string.Empty,
                travelId,
                loadTicketId,
                targetDimensionId,
                targetLocalPosition,
                targetAbsolutePosition);
        }
    }
}
