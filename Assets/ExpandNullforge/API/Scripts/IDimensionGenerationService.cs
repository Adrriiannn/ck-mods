namespace ExpandNullforge.Api
{
    using System;
    using System.Collections.Generic;

    public interface IDimensionGenerationService
    {
        event Action<DimensionGenerationStatus> GenerationStatusChanged;
        event Action<DimensionGenerationReservationChangedEvent> GenerationReservationChanged;

        DimensionGenerationStatus RequestGeneration(DimensionGenerationRequest request);

        DimensionGenerationStatus SetGenerationStatus(
            string dimensionId,
            DimensionBounds localBounds,
            DimensionGenerationState state,
            float progress01,
            string message);

        bool TryGetGenerationStatus(
            string dimensionId,
            DimensionBounds localBounds,
            out DimensionGenerationStatus status);

        IReadOnlyList<DimensionGenerationStatus> GetGenerationStatuses(string dimensionId);

        bool IsAreaGenerated(string dimensionId, DimensionBounds localBounds);

        DimensionGenerationPreflightResult PreflightGeneration(
            DimensionGenerationPreflightRequest request);

        DimensionGenerationPreviewResult PreviewGenerationRequest(
            DimensionGenerationPreviewRequest request);

        bool TryCancelGeneration(
            string dimensionId,
            DimensionBounds localBounds,
            string reason,
            out DimensionOperationResult result);

        bool TryForgetGeneratedArea(
            string dimensionId,
            DimensionBounds localBounds,
            out DimensionOperationResult result);

        bool TryGetGenerationReservation(
            string reservationId,
            out DimensionGenerationReservation reservation);

        IReadOnlyList<DimensionGenerationReservation> GetGenerationReservations(
            DimensionGenerationReservationQuery query);

        bool TryReserveGenerationArea(
            DimensionGenerationReservationRequest request,
            out DimensionGenerationReservation reservation,
            out DimensionOperationResult result);

        bool TryReleaseGenerationReservation(
            string reservationId,
            string reason,
            out DimensionGenerationReservation reservation,
            out DimensionOperationResult result);

        bool TryRegisterGenerationProvider(
            IDimensionGenerationProvider provider,
            out DimensionOperationResult result);

        bool TryRemoveGenerationProvider(
            string providerId,
            out DimensionOperationResult result);

        IReadOnlyList<string> GetGenerationProviderIds();
    }
}
