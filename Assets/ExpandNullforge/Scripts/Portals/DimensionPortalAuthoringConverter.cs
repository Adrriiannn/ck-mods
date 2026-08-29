using System;
using Interaction;
using Pug.Conversion;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using ExpandNullforge.Core;

namespace ExpandNullforge.Portals
{
  [Preserve]
  public sealed class DimensionPortalAuthoringConverter :
      SingleAuthoringComponentConverter<DimensionPortalAuthoring>
  {
    protected override void Convert(DimensionPortalAuthoring authoring)
    {
      if (authoring == null)
      {
        return;
      }

      float activationChargeSeconds = Mathf.Max(0.0f, authoring.ActivationChargeSeconds);
      byte active = authoring.ActiveByDefault ? (byte)1 : (byte)0;
      byte charged = active != 0 && activationChargeSeconds <= 0.0f
          ? (byte)1
          : (byte)0;

      AddComponentData(new DimensionPortalCD
      {
        PortalId = DimensionFixedStrings.ToFixed64(authoring.PortalId),
        TargetDimensionId = DimensionFixedStrings.ToFixed64(authoring.PreviewTargetDimensionId),
        TargetLocalX = authoring.PreviewTargetLocalX,
        TargetLocalY = authoring.PreviewTargetLocalY,
        ActivationCooldownSeconds = Mathf.Max(0.0f, authoring.ActivationCooldownSeconds),
        ActivationChargeSeconds = activationChargeSeconds,
        ActivationProgress = charged != 0 ? 1.0f : 0.0f,
        ActivationStartedAt = -1.0d,
        RequireGeneratedArea = authoring.RequireGeneratedArea ? (byte)1 : (byte)0,
        AllowFallbackPosition = authoring.AllowFallbackPosition ? (byte)1 : (byte)0,
        Active = active,
        Charged = charged,
        Interactable = authoring.InteractableByDefault ? (byte)1 : (byte)0
      });
      AddComponentData(new MapMarkerCD
      {
        mapMarkerType = MapMarkerType.Portal,
        userMapMarkerType = UserMapMarkerType.None,
        uniqueMarkerId = ObjectID.None
      });
      AddComponentData(new MapMarkerActivatedCD
      {
        Value = charged != 0,
        Hidden = false
      });

      EnsureHasComponent<IndestructibleCD>(authoring.IndestructibleByDefault);
      EnsureHasComponent<DontDropSelfCD>(authoring.IndestructibleByDefault);
      EnsureHasComponent<DontDropLootCD>(authoring.IndestructibleByDefault);
      if (active != 0 &&
          charged == 0 &&
          activationChargeSeconds > 0.0f)
      {
        AddComponentData(new DimensionPortalChargeUpdateCD());
      }

      EnsureHasBuffer<DimensionPortalActivationBuffer>();
      EnsureHasBuffer<TriggerUseInteractionBuffer>();
      EnsureHasBuffer<TriggerExitInteractionBuffer>();
    }

  }
}
