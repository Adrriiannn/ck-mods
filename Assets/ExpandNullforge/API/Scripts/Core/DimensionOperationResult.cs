namespace ExpandNullforge.Api
{
    public readonly struct DimensionOperationResult
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string Message;

        public DimensionOperationResult(bool success, string code, string message)
        {
            Success = success;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static DimensionOperationResult Ok()
        {
            return new DimensionOperationResult(true, string.Empty, string.Empty);
        }

        public static DimensionOperationResult Failed(string code, string message)
        {
            return new DimensionOperationResult(false, code, message);
        }
    }
}
