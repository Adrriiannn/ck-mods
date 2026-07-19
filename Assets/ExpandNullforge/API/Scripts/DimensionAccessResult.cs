namespace ExpandNullforge.Api
{
    public readonly struct DimensionAccessResult
    {
        public readonly bool Allowed;
        public readonly string Code;
        public readonly string Message;

        public DimensionAccessResult(bool allowed, string code, string message)
        {
            Allowed = allowed;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static DimensionAccessResult Allow()
        {
            return new DimensionAccessResult(true, string.Empty, string.Empty);
        }

        public static DimensionAccessResult Deny(string code, string message)
        {
            return new DimensionAccessResult(false, code, message);
        }
    }
}
