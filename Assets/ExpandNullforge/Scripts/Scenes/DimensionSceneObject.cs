using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>One item inside a placed container.</summary>
    public readonly struct DimensionSceneContent
    {
        public DimensionSceneContent(string itemName, int amount)
        {
            ItemName = itemName ?? string.Empty;
            Amount = amount < 1 ? 1 : amount;
        }

        /// <summary>Held as a name, resolved to an id only when a world exists.</summary>
        public readonly string ItemName;

        public readonly int Amount;
    }

    /// <summary>One object a scene places, reduced to what the blob needs.</summary>
    public readonly struct DimensionSceneObject
    {
        public DimensionSceneObject(
            int2 localPosition,
            string objectName,
            DimensionSceneFacing facing,
            DimensionScenePaintChoice paint,
            string lootTableName = null,
            IReadOnlyList<DimensionSceneContent> contents = null)
        {
            LocalPosition = localPosition;
            ObjectName = objectName;
            Facing = facing;
            Paint = paint;
            LootTableName = lootTableName ?? string.Empty;
            Contents = contents;
        }

        /// <summary>A loot table to roll into this container, or empty for none.</summary>
        public readonly string LootTableName;

        /// <summary>
        /// Exact items to place inside, or null for none.
        /// </summary>
        /// <remarks>
        /// Null rather than an empty list so "this is not a container" and "this container is
        /// deliberately empty" stay distinguishable — the second is a real authoring choice, and the
        /// blob writes them differently.
        /// </remarks>
        public readonly IReadOnlyList<DimensionSceneContent> Contents;

        /// <summary>Whether anything was authored for this object's inventory.</summary>
        public bool HasInventoryOverride
        {
            get { return !string.IsNullOrEmpty(LootTableName) || (Contents != null && Contents.Count > 0); }
        }

        public readonly int2 LocalPosition;

        /// <summary>
        /// The object's name, resolved to an id only when a world exists.
        /// </summary>
        /// <remarks>
        /// Held as a name all the way to injection because that is the first moment the object
        /// database is loaded and the prefab is real. Resolving earlier would mean writing down a
        /// number the game had not decided yet.
        /// </remarks>
        public readonly string ObjectName;

        public readonly DimensionSceneFacing Facing;

        /// <summary>Which palette colour this copy is painted, if any.</summary>
        public readonly DimensionScenePaintChoice Paint;
    }

    /// <summary>
    /// A paint choice, mirroring the game's palette with an explicit "leave it alone" at zero.
    /// </summary>
    /// <remarks>
    /// Paint in Core Keeper is a named palette entry, not an RGB value — each colour has authored
    /// sprite work behind it, so there is nothing between Red and Purple to choose.
    /// <c>Unpainted</c> is both the game's own first entry and our "no override", which lines up
    /// conveniently: not painting something and painting it Unpainted are the same request.
    /// </remarks>
    public enum DimensionScenePaintChoice
    {
        Unpainted = 0,
        Yellow = 1,
        Green = 2,
        Red = 3,
        Purple = 4,
        Blue = 5,
        Brown = 6,
        White = 7,
        Black = 8,
        Orange = 9,
        Cyan = 10,
        Pink = 11,
        Gray = 12,
        Peach = 13,
        Teal = 14
    }

    /// <summary>Which way a placed object faces, independent of Unity types.</summary>
    public enum DimensionSceneFacing
    {
        Unchanged = 0,
        North = 1,
        East = 2,
        South = 3,
        West = 4
    }

    /// <summary>
    /// Turns a facing into the direction vector the scene blob stores.
    /// </summary>
    /// <remarks>
    /// Directions are on the ground plane: the game's world is X across and Z into the screen, so
    /// "north" is +Z and Y is never touched. Getting this wrong points every object at the sky, which
    /// is the kind of thing that looks like a rendering bug rather than a maths one.
    /// </remarks>
    public static class DimensionSceneFacings
    {
        public static bool TryGetDirection(DimensionSceneFacing facing, out float3 direction)
        {
            switch (facing)
            {
                case DimensionSceneFacing.North:
                    direction = new float3(0f, 0f, 1f);
                    return true;
                case DimensionSceneFacing.East:
                    direction = new float3(1f, 0f, 0f);
                    return true;
                case DimensionSceneFacing.South:
                    direction = new float3(0f, 0f, -1f);
                    return true;
                case DimensionSceneFacing.West:
                    direction = new float3(-1f, 0f, 0f);
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }
    }

    /// <summary>Everything a scene places that is not a tile.</summary>
    public sealed class DimensionSceneObjectSet
    {
        public DimensionSceneObjectSet(IReadOnlyList<DimensionSceneObject> objects)
        {
            Objects = objects ?? new List<DimensionSceneObject>();
        }

        public readonly IReadOnlyList<DimensionSceneObject> Objects;
    }
}
