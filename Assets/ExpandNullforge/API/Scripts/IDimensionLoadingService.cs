namespace ExpandNullforge.Api
{
    using System;

    public interface IDimensionLoadingService
    {
        event Action<DimensionLoadTicketSnapshot> LoadTicketChanged;

        DimensionLoadTicket RequestLoad(DimensionLoadRequest request);

        bool TryGetLoadStatus(string ticketId, out DimensionLoadTicket ticket);

        DimensionOperationResult ReleaseLoadTicket(string ticketId);
    }
}
