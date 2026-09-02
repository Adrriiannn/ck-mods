namespace ExpandNullforge.Api
{
    public readonly struct DimensionLoadTicket
    {
        public readonly bool IsValid;
        public readonly string TicketId;
        public readonly DimensionLoadState State;
        public readonly string Message;

        public DimensionLoadTicket(bool isValid, string ticketId, DimensionLoadState state, string message)
        {
            IsValid = isValid;
            TicketId = ticketId ?? string.Empty;
            State = state;
            Message = message ?? string.Empty;
        }

        public static DimensionLoadTicket Invalid(string message)
        {
            return new DimensionLoadTicket(false, string.Empty, DimensionLoadState.Failed, message);
        }
    }
}
