namespace ExpandNullforge.Authoring
{
    /// <summary>How a scene decides where in a dimension it belongs.</summary>
    public enum DimensionScenePlacementMode
    {
        /// <summary>Anywhere the generator finds room.</summary>
        Automatic = 0,

        /// <summary>Somewhere inside an authored rectangle.</summary>
        PreferredBounds = 1,

        /// <summary>At one exact spot, for a landmark there is only one of.</summary>
        ExactLocalPosition = 2,

        /// <summary>
        /// At a distance from the dimension's own centre, anywhere around it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The mode that matches how Core Keeper's world is actually shaped. Its biomes are rings
        /// around the origin and its landmarks sit at distances from it, so "somewhere 300 to 400 tiles
        /// out" is the natural way to say where a thing belongs — far more so than a rectangle, which
        /// would put the structure closer on the diagonals than on the axes.
        /// </para>
        /// <para>
        /// Measured from local 0,0, which is where the player arrives in a dimension. A radius is
        /// therefore also a statement about how long the walk is before they find it.
        /// </para>
        /// </remarks>
        RadialBand = 3
    }
}
