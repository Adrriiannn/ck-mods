namespace ExpandNullforge.Api
{
    public interface IDimensionPersistenceService
    {
        DimensionPersistenceHealthSnapshot GetPersistenceHealthSnapshot();

        DimensionOperationResult ForceFlushPersistence(string reason);

        DimensionOperationResult PreparePersistenceForWorldUnload(string reason);
    }
}
