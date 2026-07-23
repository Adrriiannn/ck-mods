using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// The instant item-portal (V2) flavor of <see cref="DimensionPortal"/>. Deliberately empty:
  /// the game's pooled graphical-object system buckets view instances by component type, so the
  /// placed/return portal visual and the frameless instant visual must carry DIFFERENT types —
  /// with a shared type the pool hands placed-portal clones to instant portal entities, binding
  /// the wrong artwork (frame and 16px center instead of the instant sheets). All behaviour
  /// lives in the base class; the per-entity instant-mode switch in OnOccupied remains as a
  /// second line of defence for any cross-served instance.
  /// </summary>
  [AddComponentMenu("Dimension Framework/Dimension Instant Portal")]
  public sealed class DimensionInstantPortal : DimensionPortal
  {
  }
}
