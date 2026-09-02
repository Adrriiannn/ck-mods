using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Shared authoring contract for the generated portal SpriteObjects and the dashboard preview.
    /// Core Keeper pitches its camera 45 degrees and compensates the projection vertically, so
    /// screen-space Y is (world Y + world Z) at the game's native 16 pixels per world unit.
    /// </summary>
    internal static class DimensionPortalVisualContract
    {
        internal enum Layer
        {
            Frame,
            ChargeSweep,
            Milestones,
            Center
        }

        internal const float PixelsPerUnit = 16.0f;
        internal const int CanonicalFramePixels = 48;
        internal const int CanonicalCenterWidth = 16;
        internal const int CanonicalCenterHeight = 25;

        internal static readonly Vector3 FrameLocalPosition =
            new Vector3(0.0f, 1.5625f, 0.0625f);
        internal static readonly Vector3 ChargeSweepLocalPosition =
            new Vector3(0.0f, 1.5625f, 0.0625f);
        internal static readonly Vector3 MilestoneLocalPosition =
            new Vector3(0.0f, 1.5625f, 0.0624f);
        internal static readonly Vector3 CenterLocalPosition =
            new Vector3(0.0f, 0.71875f, 0.3125f);
        internal static readonly Vector3 OutlineLocalPosition =
            new Vector3(0.0f, 1.5625f, 0.0622f);

        internal static Vector3 GetLocalPosition(Layer layer, Vector2 offsetPixels)
        {
            Vector3 basePosition;
            switch (layer)
            {
                case Layer.ChargeSweep:
                    basePosition = ChargeSweepLocalPosition;
                    break;
                case Layer.Milestones:
                    basePosition = MilestoneLocalPosition;
                    break;
                case Layer.Center:
                    basePosition = CenterLocalPosition;
                    break;
                default:
                    basePosition = FrameLocalPosition;
                    break;
            }

            return basePosition + new Vector3(
                offsetPixels.x / PixelsPerUnit,
                offsetPixels.y / PixelsPerUnit,
                0.0f);
        }

        internal static Quaternion GetLocalRotation(float rotationDegrees)
        {
            return Quaternion.Euler(0.0f, 0.0f, rotationDegrees);
        }

        internal static Vector3 GetLocalScale(Vector2 scale, bool flipX, bool flipY)
        {
            float x = Mathf.Clamp(Mathf.Abs(scale.x), 0.05f, 8.0f);
            float y = Mathf.Clamp(Mathf.Abs(scale.y), 0.05f, 8.0f);
            return new Vector3(flipX ? -x : x, flipY ? -y : y, 1.0f);
        }

        internal static Vector2 ProjectToGamePixels(Vector3 localPosition)
        {
            return new Vector2(
                localPosition.x * PixelsPerUnit,
                (localPosition.y + localPosition.z) * PixelsPerUnit);
        }

        internal static Rect ResolveBodyLocalRect(
            Vector2Int bodyFrameSize,
            Vector2 bodyPivot,
            Vector2Int layerFrameSize,
            Vector2 layerPivot,
            Vector3 layerLocalPosition)
        {
            Vector2 bodyAnchor = ProjectToGamePixels(FrameLocalPosition);
            Vector2 bodyOrigin = bodyAnchor - Vector2.Scale(
                bodyPivot,
                new Vector2(bodyFrameSize.x, bodyFrameSize.y));
            Vector2 layerAnchor = ProjectToGamePixels(layerLocalPosition);
            Vector2 layerOrigin = layerAnchor - Vector2.Scale(
                layerPivot,
                new Vector2(layerFrameSize.x, layerFrameSize.y));
            Vector2 relativeOrigin = layerOrigin - bodyOrigin;
            return new Rect(
                relativeOrigin.x,
                relativeOrigin.y,
                layerFrameSize.x,
                layerFrameSize.y);
        }

        internal static Vector2 GetBodyScreenOrigin(Vector2Int bodyFrameSize, Vector2 bodyPivot)
        {
            return ProjectToGamePixels(FrameLocalPosition) - Vector2.Scale(
                bodyPivot,
                new Vector2(bodyFrameSize.x, bodyFrameSize.y));
        }

        internal static bool IsPixelAligned(Rect rect, float epsilon = 0.001f)
        {
            return Mathf.Abs(rect.x - Mathf.Round(rect.x)) <= epsilon &&
                   Mathf.Abs(rect.y - Mathf.Round(rect.y)) <= epsilon;
        }
    }
}
