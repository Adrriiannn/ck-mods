using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public interface IDimensionSceneService
    {
        event Action<DimensionSceneChangedEvent> SceneChanged;

        IReadOnlyList<DimensionSceneDefinition> GetScenes(string dimensionId, bool includeDisabled);

        bool TryGetScene(string sceneId, out DimensionSceneDefinition scene);

        bool TryFindSceneAtLocal(
            string dimensionId,
            float2 localPosition,
            out DimensionSceneDefinition scene);

        bool TryRegisterScene(DimensionSceneDefinition scene, out DimensionOperationResult result);

        bool TryUpdateScene(
            DimensionSceneDefinition scene,
            string reason,
            out DimensionOperationResult result);

        bool TrySetSceneState(
            string sceneId,
            DimensionSceneState state,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveScene(string sceneId, out DimensionOperationResult result);
    }
}
