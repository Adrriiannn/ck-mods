using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCreationWizardSessionStatus
    {
        Empty = 0,
        Ready = 1,
        NeedsReview = 2,
        Dirty = 3
    }

    public enum DimensionTemplateCreationWizardSessionEventKind
    {
        RequestAssigned = 0,
        FieldChanged = 1,
        PreviewRefreshed = 2,
        WorkspaceCreated = 3,
        MarkedClean = 4,
        HistoryReset = 5
    }

    public sealed class DimensionTemplateCreationWizardSessionEvent
    {
        public DimensionTemplateCreationWizardSessionEvent(
            int version,
            DimensionTemplateCreationWizardSessionEventKind kind,
            string code,
            string message,
            string fieldId,
            string previousValue,
            string newValue,
            bool dirtyAfterEvent,
            DimensionTemplateCreationWizardPreview preview)
        {
            Version = version < 0 ? 0 : version;
            Kind = kind;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            FieldId = fieldId ?? string.Empty;
            PreviousValue = previousValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            DirtyAfterEvent = dirtyAfterEvent;
            Preview = preview;
        }

        public int Version { get; private set; }

        public DimensionTemplateCreationWizardSessionEventKind Kind { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public string FieldId { get; private set; }

        public string PreviousValue { get; private set; }

        public string NewValue { get; private set; }

        public bool DirtyAfterEvent { get; private set; }

        public DimensionTemplateCreationWizardPreview Preview { get; private set; }
    }

    public sealed class DimensionTemplateCreationWizardSessionSnapshot
    {
        public DimensionTemplateCreationWizardSessionSnapshot(
            DimensionTemplateCreationWizardSessionStatus status,
            int version,
            bool dirty,
            DimensionTemplateCreationWizardRequest request,
            DimensionTemplateCreationWizardModel model,
            DimensionTemplateCreationWizardPreview preview,
            DimensionTemplateAuthoringWorkspace lastCreatedWorkspace,
            IReadOnlyList<DimensionTemplateCreationWizardSessionEvent> history)
        {
            Status = status;
            Version = version < 0 ? 0 : version;
            Dirty = dirty;
            Request = request ?? new DimensionTemplateCreationWizardRequest();
            Model = model;
            Preview = preview;
            LastCreatedWorkspace = lastCreatedWorkspace;
            History = history ?? new List<DimensionTemplateCreationWizardSessionEvent>();
        }

        public DimensionTemplateCreationWizardSessionStatus Status { get; private set; }

        public int Version { get; private set; }

        public bool Dirty { get; private set; }

        public DimensionTemplateCreationWizardRequest Request { get; private set; }

        public DimensionTemplateCreationWizardModel Model { get; private set; }

        public DimensionTemplateCreationWizardPreview Preview { get; private set; }

        public DimensionTemplateAuthoringWorkspace LastCreatedWorkspace { get; private set; }

        public IReadOnlyList<DimensionTemplateCreationWizardSessionEvent> History { get; private set; }
    }

    public sealed class DimensionTemplateCreationWizardSessionController
    {
        private const int MaxHistoryEntries = 64;

        private readonly List<DimensionTemplateCreationWizardSessionEvent> history =
            new List<DimensionTemplateCreationWizardSessionEvent>();

        public DimensionTemplateCreationWizardSessionController()
            : this(new DimensionTemplateCreationWizardRequest())
        {
        }

        public DimensionTemplateCreationWizardSessionController(
            DimensionTemplateCreationWizardRequest request)
        {
            SetRequest(request);
        }

        public DimensionTemplateCreationWizardRequest Request { get; private set; }

        public DimensionTemplateCreationWizardModel Model { get; private set; }

        public DimensionTemplateCreationWizardPreview Preview { get; private set; }

        public DimensionTemplateAuthoringWorkspace LastCreatedWorkspace { get; private set; }

        public bool Dirty { get; private set; }

        public int Version { get; private set; }

        public IReadOnlyList<DimensionTemplateCreationWizardSessionEvent> History
        {
            get
            {
                return new List<DimensionTemplateCreationWizardSessionEvent>(history);
            }
        }

        public DimensionTemplateCreationWizardSessionStatus Status
        {
            get
            {
                if (Request == null)
                {
                    return DimensionTemplateCreationWizardSessionStatus.Empty;
                }

                if (Dirty)
                {
                    return DimensionTemplateCreationWizardSessionStatus.Dirty;
                }

                return Preview != null && Preview.CanCreate
                    ? DimensionTemplateCreationWizardSessionStatus.Ready
                    : DimensionTemplateCreationWizardSessionStatus.NeedsReview;
            }
        }

        public DimensionTemplateCreationWizardSessionSnapshot GetSnapshot()
        {
            return new DimensionTemplateCreationWizardSessionSnapshot(
                Status,
                Version,
                Dirty,
                Request,
                Model,
                Preview,
                LastCreatedWorkspace,
                History);
        }

        public DimensionTemplateCreationWizardActionPlan GetActionPlan()
        {
            return DimensionTemplateCreationWizardActionPlanUtility.Build(GetSnapshot());
        }

        public DimensionTemplateCreationWizardActionResult RunSourceAction(
            string actionId)
        {
            return DimensionTemplateCreationWizardActionPlanUtility.RunSourceAction(
                this,
                actionId);
        }

        public void SetRequest(DimensionTemplateCreationWizardRequest request)
        {
            Request = CopyRequest(request);
            LastCreatedWorkspace = null;
            Dirty = false;
            Version++;
            RefreshPreview();
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.RequestAssigned,
                "request-assigned",
                "Dimension template creation request assigned.",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public DimensionTemplateCreationWizardPreview RefreshPreview()
        {
            Model = DimensionTemplateCreationWizardUtility.BuildModel(Request);
            Preview = DimensionTemplateCreationWizardUtility.Preview(Request);
            Version++;
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.PreviewRefreshed,
                "preview-refreshed",
                Preview == null
                    ? "Dimension template creation preview could not be built."
                    : Preview.Message,
                string.Empty,
                string.Empty,
                string.Empty);
            return Preview;
        }

        public DimensionTemplateCreationWizardPreview UpdateField(
            string fieldId,
            string value)
        {
            DimensionTemplateCreationWizardRequest next = CopyRequest(Request);
            string previousValue = GetFieldValue(next, fieldId);
            ApplyFieldValue(next, fieldId, value);
            Request = next;
            Dirty = true;
            Version++;
            RefreshPreview();
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.FieldChanged,
                "field-changed",
                "Dimension template creation field changed.",
                fieldId,
                previousValue,
                GetFieldValue(next, fieldId));
            return Preview;
        }

        public DimensionTemplateAuthoringWorkspace CreateWorkspace()
        {
            RefreshPreview();
            if (Preview == null || !Preview.CanCreate)
            {
                AddEvent(
                    DimensionTemplateCreationWizardSessionEventKind.WorkspaceCreated,
                    "workspace-blocked",
                    Preview == null
                        ? "Workspace cannot be created because no preview exists."
                        : Preview.Message,
                    string.Empty,
                    string.Empty,
                    string.Empty);
                return null;
            }

            LastCreatedWorkspace =
                DimensionTemplateCreationWizardUtility.CreateWorkspace(Request);
            Dirty = false;
            Version++;
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.WorkspaceCreated,
                LastCreatedWorkspace == null ? "workspace-missing" : "workspace-created",
                LastCreatedWorkspace == null
                    ? "Workspace creation returned no workspace."
                    : "Dimension template authoring workspace created.",
                string.Empty,
                string.Empty,
                string.Empty);
            return LastCreatedWorkspace;
        }

        public void MarkClean()
        {
            Dirty = false;
            Version++;
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.MarkedClean,
                "marked-clean",
                "Dimension template creation session marked clean.",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public void ResetHistory()
        {
            history.Clear();
            Version++;
            AddEvent(
                DimensionTemplateCreationWizardSessionEventKind.HistoryReset,
                "history-reset",
                "Dimension template creation session history reset.",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static DimensionTemplateCreationWizardRequest CopyRequest(
            DimensionTemplateCreationWizardRequest request)
        {
            DimensionTemplateCreationWizardRequest source =
                request ?? new DimensionTemplateCreationWizardRequest();
            DimensionTemplateCreationWizardRequest copy =
                new DimensionTemplateCreationWizardRequest();
            copy.ExampleId = source.ExampleId ?? string.Empty;
            copy.ModIdPrefix = source.ModIdPrefix ?? string.Empty;
            copy.DimensionId = source.DimensionId ?? string.Empty;
            copy.DimensionDisplayName = source.DimensionDisplayName ?? string.Empty;
            copy.ContentPackId = source.ContentPackId ?? string.Empty;
            copy.ContentPackDisplayName = source.ContentPackDisplayName ?? string.Empty;
            copy.ContentPackAuthor = source.ContentPackAuthor ?? string.Empty;
            copy.BiomeId = source.BiomeId ?? string.Empty;
            copy.BiomeDisplayName = source.BiomeDisplayName ?? string.Empty;
            copy.ZoneId = source.ZoneId ?? string.Empty;
            copy.AbsoluteOriginX = source.AbsoluteOriginX;
            copy.AbsoluteOriginY = source.AbsoluteOriginY;
            copy.HalfSizeTiles = source.HalfSizeTiles;
            copy.ReservedShellPaddingTiles = source.ReservedShellPaddingTiles;
            copy.FloorResourceKey = source.FloorResourceKey ?? string.Empty;
            copy.WallResourceKey = source.WallResourceKey ?? string.Empty;
            copy.LiquidResourceKey = source.LiquidResourceKey ?? string.Empty;
            copy.OreResourceKey = source.OreResourceKey ?? string.Empty;
            copy.ObjectResourceKey = source.ObjectResourceKey ?? string.Empty;
            copy.TerrainGenerationProviderId = source.TerrainGenerationProviderId ?? string.Empty;
            copy.SuggestedRootFolder = source.SuggestedRootFolder ?? string.Empty;
            return copy;
        }

        private static string GetFieldValue(
            DimensionTemplateCreationWizardRequest request,
            string fieldId)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ExampleFieldId)
            {
                return request.ExampleId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.DimensionIdFieldId)
            {
                return request.DimensionId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ModIdPrefixFieldId)
            {
                return request.ModIdPrefix ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.DimensionDisplayNameFieldId)
            {
                return request.DimensionDisplayName ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackIdFieldId)
            {
                return request.ContentPackId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackDisplayNameFieldId)
            {
                return request.ContentPackDisplayName ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackAuthorFieldId)
            {
                return request.ContentPackAuthor ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.BiomeIdFieldId)
            {
                return request.BiomeId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.BiomeDisplayNameFieldId)
            {
                return request.BiomeDisplayName ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ZoneIdFieldId)
            {
                return request.ZoneId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginXFieldId)
            {
                return request.AbsoluteOriginX.ToString();
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginYFieldId)
            {
                return request.AbsoluteOriginY.ToString();
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.HalfSizeTilesFieldId)
            {
                return request.HalfSizeTiles.ToString();
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ReservedShellPaddingTilesFieldId)
            {
                return request.ReservedShellPaddingTiles.ToString();
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.FloorResourceKeyFieldId)
            {
                return request.FloorResourceKey ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.WallResourceKeyFieldId)
            {
                return request.WallResourceKey ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.LiquidResourceKeyFieldId)
            {
                return request.LiquidResourceKey ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.OreResourceKeyFieldId)
            {
                return request.OreResourceKey ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ObjectResourceKeyFieldId)
            {
                return request.ObjectResourceKey ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.TerrainGenerationProviderIdFieldId)
            {
                return request.TerrainGenerationProviderId ?? string.Empty;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.RootFolderFieldId)
            {
                return request.SuggestedRootFolder ?? string.Empty;
            }

            return string.Empty;
        }

        private static void ApplyFieldValue(
            DimensionTemplateCreationWizardRequest request,
            string fieldId,
            string value)
        {
            if (request == null)
            {
                return;
            }

            string safeValue = value ?? string.Empty;
            if (fieldId == DimensionTemplateCreationWizardUtility.ExampleFieldId)
            {
                request.ExampleId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.DimensionIdFieldId)
            {
                request.DimensionId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ModIdPrefixFieldId)
            {
                request.ModIdPrefix = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.DimensionDisplayNameFieldId)
            {
                request.DimensionDisplayName = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackIdFieldId)
            {
                request.ContentPackId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackDisplayNameFieldId)
            {
                request.ContentPackDisplayName = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ContentPackAuthorFieldId)
            {
                request.ContentPackAuthor = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.BiomeIdFieldId)
            {
                request.BiomeId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.BiomeDisplayNameFieldId)
            {
                request.BiomeDisplayName = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ZoneIdFieldId)
            {
                request.ZoneId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginXFieldId)
            {
                request.AbsoluteOriginX = ParseInt(safeValue, request.AbsoluteOriginX);
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.AbsoluteOriginYFieldId)
            {
                request.AbsoluteOriginY = ParseInt(safeValue, request.AbsoluteOriginY);
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.HalfSizeTilesFieldId)
            {
                request.HalfSizeTiles = ParseInt(safeValue, request.HalfSizeTiles);
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ReservedShellPaddingTilesFieldId)
            {
                request.ReservedShellPaddingTiles = ParseInt(safeValue, request.ReservedShellPaddingTiles);
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.FloorResourceKeyFieldId)
            {
                request.FloorResourceKey = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.WallResourceKeyFieldId)
            {
                request.WallResourceKey = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.LiquidResourceKeyFieldId)
            {
                request.LiquidResourceKey = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.OreResourceKeyFieldId)
            {
                request.OreResourceKey = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.ObjectResourceKeyFieldId)
            {
                request.ObjectResourceKey = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.TerrainGenerationProviderIdFieldId)
            {
                request.TerrainGenerationProviderId = safeValue;
                return;
            }

            if (fieldId == DimensionTemplateCreationWizardUtility.RootFolderFieldId)
            {
                request.SuggestedRootFolder = safeValue;
            }
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed)
                ? parsed
                : fallback;
        }

        private void AddEvent(
            DimensionTemplateCreationWizardSessionEventKind kind,
            string code,
            string message,
            string fieldId,
            string previousValue,
            string newValue)
        {
            history.Add(new DimensionTemplateCreationWizardSessionEvent(
                Version,
                kind,
                code,
                message,
                fieldId,
                previousValue,
                newValue,
                Dirty,
                Preview));
            while (history.Count > MaxHistoryEntries)
            {
                history.RemoveAt(0);
            }
        }
    }
}
