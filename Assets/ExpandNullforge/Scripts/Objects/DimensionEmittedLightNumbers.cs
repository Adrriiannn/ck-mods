using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The numbers one object's light runs at, once the game's own flicker rule has been applied to
    /// what the author typed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PLAIN NUMBERS RATHER THAN THE AUTHORING BLOCK. The block an author fills in is a serialized
    /// editor type with its own presets and its own "this is wrong" answers; what has to reach a
    /// light while the game is running is only the nine values below. Keeping them apart is what
    /// lets the generated bootstrap register a light with one call that names no editor type.
    /// </para>
    /// <para>
    /// <c>HowBright</c> is already the brightness the game settles on, and
    /// <c>AtItsDimmest</c>/<c>AtItsBrightest</c> are already closed up for a light that does not
    /// flicker. Both conversions happen once, in
    /// <c>DimensionEmittedLightTemplate.TheNumbersTheGameRunsWith</c>, so the prefab written at
    /// generation and the light re-derived per entity cannot drift apart.
    /// </para>
    /// </remarks>
    public readonly struct DimensionEmittedLightNumbers
    {
        public DimensionEmittedLightNumbers(
            Color colour,
            float howBright,
            float howFarItReaches,
            bool itCastsShadows,
            float atItsDimmest,
            float atItsBrightest,
            bool theFlameMoves,
            float howHighAboveTheFloor,
            float howFarFrontToBack)
        {
            Colour = colour;
            HowBright = howBright;
            HowFarItReaches = howFarItReaches;
            ItCastsShadows = itCastsShadows;
            AtItsDimmest = atItsDimmest;
            AtItsBrightest = atItsBrightest;
            TheFlameMoves = theFlameMoves;
            HowHighAboveTheFloor = howHighAboveTheFloor;
            HowFarFrontToBack = howFarFrontToBack;
        }

        /// <summary>The colour of the light on the floor.</summary>
        public Color Colour { get; }

        /// <summary>What <c>Light.intensity</c> is set to.</summary>
        public float HowBright { get; }

        /// <summary>What <c>Light.range</c> is set to, in tiles.</summary>
        public float HowFarItReaches { get; }

        /// <summary>Whether things near it throw shadows.</summary>
        public bool ItCastsShadows { get; }

        /// <summary><c>LightFlickerEffect.minIntensity</c>.</summary>
        public float AtItsDimmest { get; }

        /// <summary><c>LightFlickerEffect.maxIntensity</c>.</summary>
        public float AtItsBrightest { get; }

        /// <summary><c>LightFlickerEffect.enableMovement</c>.</summary>
        public bool TheFlameMoves { get; }

        /// <summary>The local Y given to the node that carries the <c>ManagedLight</c>.</summary>
        public float HowHighAboveTheFloor { get; }

        /// <summary>The local Z given to that same node.</summary>
        public float HowFarFrontToBack { get; }
    }
}
