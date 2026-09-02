namespace ExpandNullforge.Api
{
    using System;
    using System.Collections.Generic;

    public interface IDimensionSceneTemplateService
    {
        event Action<DimensionSceneTemplateChangedEvent> SceneTemplateChanged;

        IReadOnlyList<DimensionSceneTemplateDefinition> GetSceneTemplates(
            string dimensionId,
            string zoneId,
            string kind,
            bool includeDisabled);

        bool TryGetSceneTemplate(
            string templateId,
            out DimensionSceneTemplateDefinition template);

        bool TryRegisterSceneTemplate(
            DimensionSceneTemplateDefinition template,
            out DimensionOperationResult result);

        bool TryUpdateSceneTemplate(
            DimensionSceneTemplateDefinition template,
            string reason,
            out DimensionOperationResult result);

        bool TrySetSceneTemplateEnabled(
            string templateId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveSceneTemplate(
            string templateId,
            out DimensionOperationResult result);
    }
}
