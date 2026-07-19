namespace ExpandNullforge.Api
{
    public readonly struct DimensionPermissionResult
    {
        public readonly bool Allowed;
        public readonly string ProviderId;
        public readonly string Code;
        public readonly string Message;

        public DimensionPermissionResult(
            bool allowed,
            string providerId,
            string code,
            string message)
        {
            Allowed = allowed;
            ProviderId = providerId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static DimensionPermissionResult Allow(string providerId, string message)
        {
            return new DimensionPermissionResult(true, providerId, string.Empty, message);
        }

        public static DimensionPermissionResult Deny(string providerId, string code, string message)
        {
            return new DimensionPermissionResult(false, providerId, code, message);
        }
    }
}
