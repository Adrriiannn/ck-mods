using UnityEngine;

namespace ExpandNullforge.Portals
{
  [DisallowMultipleComponent]
  [AddComponentMenu("Dimension Framework/Dimension Portal")]
  public sealed class DimensionPortalAuthoring : MonoBehaviour
  {
    [Tooltip("Stable portal ID registered in the dimension service. New portals should set this to their own registered portal ID.")]
    public string PortalId = string.Empty;

    [Tooltip("Minimum time, in seconds, before this placed portal can accept another activation.")]
    public float ActivationCooldownSeconds = 2.0f;

    [Tooltip("Time, in seconds, for this portal to charge before travel is allowed. Vanilla portals become ready once object data amount reaches 600.")]
    public float ActivationChargeSeconds = 30.0f;

    [Tooltip("Require the target landing area to be generated before the travel request is accepted.")]
    public bool RequireGeneratedArea = true;

    [Tooltip("Allow the travel service to use a registered fallback landing target if the requested landing tile is blocked.")]
    public bool AllowFallbackPosition = true;

    [Tooltip("Initial state before the portal registry hydrates this entity. The registry state remains authoritative.")]
    public bool ActiveByDefault = true;

    [Tooltip("Allow players to interact with this portal. The portal presentation registry can hydrate this at runtime.")]
    public bool InteractableByDefault = true;

    [Tooltip("Bake this portal with vanilla indestructible/no-drop behavior. Generated return portals enable this; normal placed entry portals leave it off.")]
    public bool IndestructibleByDefault;

    [Header("Editor fallback preview")]
    [Tooltip("Optional fallback shown in ECS before registry hydration. The registered portal destination is authoritative at runtime.")]
    public string PreviewTargetDimensionId = string.Empty;

    [Tooltip("Optional fallback local X shown in ECS before registry hydration. The registered portal destination is authoritative at runtime.")]
    public float PreviewTargetLocalX;

    [Tooltip("Optional fallback local Y shown in ECS before registry hydration. The registered portal destination is authoritative at runtime.")]
    public float PreviewTargetLocalY;
  }
}
