namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentManifestRequest
    {
        public readonly DimensionContentManifest Manifest;
        public readonly bool UpdateExisting;
        public readonly bool RequireExistingOwnershipRecords;
        public readonly string Reason;

        public DimensionContentManifestRequest(
            DimensionContentManifest manifest,
            bool updateExisting,
            bool requireExistingOwnershipRecords,
            string reason)
        {
            Manifest = manifest;
            UpdateExisting = updateExisting;
            RequireExistingOwnershipRecords = requireExistingOwnershipRecords;
            Reason = reason ?? string.Empty;
        }
    }
}
