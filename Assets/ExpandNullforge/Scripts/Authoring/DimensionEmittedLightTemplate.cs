using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Which of the game's own lights to copy the numbers from.
    /// </summary>
    /// <remarks>
    /// Six real objects, read out of their prefabs rather than invented. The numbers each one fills
    /// in are in <see cref="DimensionEmittedLightTemplate.PresetFor"/>, with the prefab and the line
    /// beside every row.
    /// </remarks>
    public enum DimensionLightLike
    {
        /// <summary>Nothing is filled in; the numbers below are yours.</summary>
        Custom = 0,

        /// <summary>A wall torch: warm, close, and it flickers and moves.</summary>
        Torch = 1,

        /// <summary>A campfire: oranger than a torch and reaches further.</summary>
        Campfire = 2,

        /// <summary>A lamp: white, steady-looking, and it reaches a long way.</summary>
        Lamp = 3,

        /// <summary>A paper lantern: white and steady, a little closer in than a lamp.</summary>
        PaperLantern = 4,

        /// <summary>A crystal lamp: cold blue, and it sits high up.</summary>
        CrystalLamp = 5,

        /// <summary>A street lamp: nearly white, high above the floor.</summary>
        LampPost = 6
    }

    /// <summary>
    /// The light an object throws on the ground around it once it is standing in the world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS NOT A COMPONENT ON THE OBJECT. Core Keeper keeps a placed object's light in the
    /// object's graphical prefab, as a small subtree of nodes: a <c>ManagedLight</c> over a
    /// container over a point light carrying <c>PugLight</c> and <c>LightFlickerEffect</c>, with an
    /// indirect-light sprite beside it. Measured identically on <c>Torch</c>, <c>Lamp</c>,
    /// <c>Campfire</c>, <c>CrystalLamp</c>, <c>ChineseLantern</c> and <c>LampPost</c>. There is no
    /// light component to look for on the entity, which is why nothing here is a tick that writes
    /// one.
    /// </para>
    /// <para>
    /// TWO OF THESE SIDE BY SIDE MAY NOT BOTH LIGHT. <c>ManagedLight.UpdateOptimization</c> sorts
    /// every live light into two-tile buckets — <c>floor(position / 2)</c> at
    /// <c>ManagedLight.cs:62-63</c>, with <c>optimizationBucketSize</c> 2 at <c>:137</c> — and
    /// leaves only the newest one in each bucket lit; the rest are swapped for a glowing sprite
    /// that does not light the floor. Two lights on next-door tiles land in the same bucket only
    /// when the grid line falls outside them both, so sometimes both do light. Either way a row of
    /// torches on adjacent tiles renders as fewer lights than it has. That is the game's own
    /// behaviour, it applies to vanilla torches exactly as it does to these, and the switch that
    /// turns it off (<c>neverOptimize</c>) is deliberately not offered — the game's own torch
    /// leaves it off, and a mod that ticked it everywhere would degrade every scene it appeared in.
    /// </para>
    /// <para>
    /// AND THERE IS A CEILING. The renderer holds 512 lights of one kind on screen at once
    /// (<c>LightData.MAX_LIGHT_COUNT</c>). Nothing a mod does raises it. Both this and the
    /// bucketing are said again on the toggle's own tooltip, because an XML remark is not
    /// somewhere a modder can read: Unity draws it into neither the inspector, the framework
    /// window, nor the stage card.
    /// </para>
    /// <para>
    /// IT IS THE OBJECT'S OWN LIGHT EVEN THOUGH THE PREFAB IS SHARED. Every framework view is
    /// pooled by component type, so a light baked onto a usable object's prefab would be one light
    /// for every object of that behaviour. <c>DimensionAuthoredLight</c> re-derives it per entity
    /// on every occupy, the way <c>DimensionAuthoredBody</c> already re-derives the picture, and
    /// the numbers it reads are the ones <see cref="TheNumbersTheGameRunsWith"/> hands over.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionEmittedLightTemplate
    {
        [Tooltip("It gives off light where it stands. This is what a torch on a wall does: it " +
                 "lights the floor around it.\n\n" +
                 "Two things the game decides and a mod cannot. Lights are kept one per two " +
                 "tiles of floor, so two of these on next-door tiles may not both light — the " +
                 "loser is drawn as a glow instead. And the screen holds 512 lit lights at once; " +
                 "nothing a mod does raises that.")]
        [SerializeField] private bool givesOffLight;

        [Tooltip("Copy one of the game's own lights. Picking one fills in the numbers below " +
                 "once, and they are yours from then on: it will not fill them in again. Pick a " +
                 "different light to replace them. Custom fills in nothing.")]
        [SerializeField] private DimensionLightLike lightLike = DimensionLightLike.Custom;

        /// <summary>Which preset's numbers are already in the fields below.</summary>
        /// <remarks>
        /// Without this, every repaint would copy the preset back over anything that had been
        /// typed, and the promise that the numbers stay editable would be a lie.
        /// </remarks>
        [HideInInspector]
        [SerializeField] private DimensionLightLike lightLikeAlreadyCopied = DimensionLightLike.Custom;

        [Tooltip("Colour of the light on the floor. The game's torch is a warm off-white.")]
        [SerializeField] private Color lightColor = new Color(1f, 0.93333334f, 0.8f, 1f);

        [Tooltip("How far it reaches, in tiles — the game's torch reaches five and its campfire " +
                 "seven.")]
        [Min(0f)]
        [SerializeField] private float howFarItReaches = 5f;

        [Tooltip("How bright it is. Only read when it does not flicker: a flickering light takes " +
                 "its brightness from the dimmest and brightest numbers instead.")]
        [Min(0f)]
        [SerializeField] private float howBright = 0.65f;

        [Tooltip("It flickers, the way a flame does. Nearly every light in the game does, " +
                 "including the lamps — a lamp just flickers in a narrower band.")]
        [SerializeField] private bool itFlickers = true;

        [Tooltip("How dim it gets at the bottom of a flicker.")]
        [Min(0f)]
        [SerializeField] private float atItsDimmest = 0.5f;

        [Tooltip("How bright it gets at the top of a flicker. Larger than the dimmest.")]
        [Min(0f)]
        [SerializeField] private float atItsBrightest = 0.65f;

        [Tooltip("The flame moves as it flickers — the light shifts a hair off centre. On for " +
                 "fire, off for a lamp.")]
        [SerializeField] private bool theFlameMoves = true;

        [Tooltip("Things near it throw shadows. Every one of the game's own lamps and torches " +
                 "does.")]
        [SerializeField] private bool itCastsShadows = true;

        [Tooltip("How high above the floor the light sits, in tiles. A torch, a lamp and a " +
                 "campfire all sit at 0.75; a street lamp is at 2.2 and a crystal lamp at 2.7.")]
        [Min(0f)]
        [SerializeField] private float howHighAboveTheFloor = 0.75f;

        [Tooltip("Nudges the light off the middle of the tile, front to back. Only three of the " +
                 "game's own lights do it, because their lamp head hangs out past the tile: the " +
                 "paper lantern sits at -0.31, the crystal lamp at -0.15 and the street lamp at " +
                 "-0.4. Every other light in the game sits at 0, and so does this until you " +
                 "change it.")]
        [SerializeField] private float howFarFrontToBack;

        /// <summary>Whether it lights the floor where it stands.</summary>
        public bool GivesOffLight
        {
            get { return givesOffLight; }
        }

        /// <summary>Which of the game's lights the numbers were copied from, if any.</summary>
        public DimensionLightLike LightLike
        {
            get { return lightLike; }
        }

        public Color LightColor
        {
            get { return lightColor; }
        }

        public float HowFarItReaches
        {
            get { return howFarItReaches < 0f ? 0f : howFarItReaches; }
        }

        public float HowBright
        {
            get { return howBright < 0f ? 0f : howBright; }
        }

        public bool ItFlickers
        {
            get { return itFlickers; }
        }

        public float AtItsDimmest
        {
            get { return atItsDimmest < 0f ? 0f : atItsDimmest; }
        }

        public float AtItsBrightest
        {
            get { return atItsBrightest < 0f ? 0f : atItsBrightest; }
        }

        public bool TheFlameMoves
        {
            get { return theFlameMoves; }
        }

        public bool ItCastsShadows
        {
            get { return itCastsShadows; }
        }

        /// <summary>How high above the floor the light sits, in tiles.</summary>
        /// <remarks>
        /// Clamped at the floor the way its four neighbours are, and for the same reason they are:
        /// a light placed under the floor lights nothing a player can see, and there is nothing
        /// below zero any of the game's own six lit prefabs uses. It is the only number in this
        /// block that used to have no floor at all.
        /// </remarks>
        public float HowHighAboveTheFloor
        {
            get { return howHighAboveTheFloor < 0f ? 0f : howHighAboveTheFloor; }
        }

        /// <summary>How far off the middle of the tile the light sits, front to back.</summary>
        /// <remarks>
        /// Not clamped, and it is the one number here that should not be: the three lights in the
        /// game that use it all use a NEGATIVE value, so a floor at zero would take the whole
        /// control away.
        /// </remarks>
        public float HowFarFrontToBack
        {
            get { return howFarFrontToBack; }
        }

        /// <summary>The brightness the light will actually be sitting at once the game has it.</summary>
        /// <remarks>
        /// NOT THE SAME AS THE AUTHORED BRIGHTNESS WHEN IT FLICKERS, and that is the game's doing,
        /// not ours. <c>LightFlickerEffect.Awake</c> assigns
        /// <c>flickeringLight.intensity = (minIntensity + maxIntensity) * 0.5f</c> before anything
        /// else reads it, so whatever brightness is baked onto a flickering light is thrown away on
        /// the first frame. Baking the value the game is going to settle on means the prefab an
        /// author opens says the same thing the world does, and it means "how bright" is never a
        /// number that quietly does nothing: it is read when the light is steady, and the two
        /// flicker bounds are read when it is not.
        /// </remarks>
        public float BrightnessTheGameSettlesOn
        {
            get
            {
                return itFlickers
                    ? (AtItsDimmest + AtItsBrightest) * 0.5f
                    : HowBright;
            }
        }

        /// <summary>Whether the two flicker bounds are the wrong way round.</summary>
        /// <remarks>
        /// Worth saying out loud rather than silently swapping: an author who typed them backwards
        /// meant something, and a light that quietly corrects itself teaches nothing.
        /// </remarks>
        public bool ItsDimmestIsBrighterThanItsBrightest
        {
            get { return itFlickers && AtItsDimmest > AtItsBrightest; }
        }

        /// <summary>Whether it was set to give off light that reaches nowhere.</summary>
        public bool ItLightsNothingBecauseItReachesNothing
        {
            get { return givesOffLight && HowFarItReaches <= 0f; }
        }

        /// <summary>Whether it was set to give off light with no brightness in it.</summary>
        /// <remarks>
        /// The other half of the same mistake as reaching nowhere, and it was not caught before:
        /// a light with a reach and no brightness generates the whole subtree, lights nothing, and
        /// looks in the prefab exactly like a light that works. Which number is read depends on
        /// whether it flickers, so this asks
        /// <see cref="BrightnessTheGameSettlesOn"/> rather than any one field — a flickering light
        /// with both bounds at zero is just as dark as a steady one at zero.
        /// </remarks>
        public bool ItLightsNothingBecauseItIsNotBrightEnough
        {
            get { return givesOffLight && BrightnessTheGameSettlesOn <= 0f; }
        }

        /// <summary>
        /// The nine values the running game reads, with every rule about which answer wins already
        /// applied.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE ONE CONVERSION, SO THE TWO HALVES CANNOT DRIFT. The light is written twice: once
        /// into the prefab at generation, and once per entity while the game runs, because the
        /// prefab is shared by every object of its behaviour. Both halves take their numbers from
        /// here, so "a steady light is a flicker with both ends the same" and "a flickering light
        /// is baked at the midpoint the game will settle on" are each stated once.
        /// </para>
        /// </remarks>
        public ExpandNullforge.Objects.DimensionEmittedLightNumbers TheNumbersTheGameRunsWith()
        {
            float dimmest = itFlickers ? AtItsDimmest : HowBright;
            float brightest = itFlickers ? AtItsBrightest : HowBright;

            return new ExpandNullforge.Objects.DimensionEmittedLightNumbers(
                lightColor,
                BrightnessTheGameSettlesOn,
                HowFarItReaches,
                itCastsShadows,
                dimmest,
                brightest,
                itFlickers && theFlameMoves,
                HowHighAboveTheFloor,
                HowFarFrontToBack);
        }

        /// <summary>
        /// Copies the chosen preset's numbers in, once, the first time it is chosen.
        /// </summary>
        /// <remarks>
        /// Called from the asset's <c>OnValidate</c>, which Unity runs whenever the inspector
        /// writes a field — so choosing a light in the dropdown fills the rest of the fold in the
        /// same frame. It runs once per choice: after the copy, the numbers belong to the author,
        /// and picking a different light is the only thing that replaces them.
        /// </remarks>
        public void CopyThePresetInIfItChanged()
        {
            if (lightLike == lightLikeAlreadyCopied)
            {
                return;
            }

            lightLikeAlreadyCopied = lightLike;
            if (lightLike == DimensionLightLike.Custom)
            {
                return;
            }

            Preset preset = PresetFor(lightLike);
            lightColor = preset.Colour;
            howFarItReaches = preset.Range;
            howBright = preset.Brightness;
            itFlickers = true;
            atItsDimmest = preset.Dimmest;
            atItsBrightest = preset.Brightest;
            theFlameMoves = preset.FlameMoves;
            itCastsShadows = true;
            howHighAboveTheFloor = preset.HeightAboveTheFloor;

            // The placement is copied as well as the numbers. Three of the six sit off the middle
            // of their tile and copying only the height would have made "Light like: PaperLantern"
            // something other than the paper lantern's light.
            howFarFrontToBack = preset.FrontToBack;
        }

        /// <summary>One of the game's own lights, read out of its prefab.</summary>
        public readonly struct Preset
        {
            public Preset(
                Color colour,
                float brightness,
                float range,
                float dimmest,
                float brightest,
                bool flameMoves,
                float heightAboveTheFloor,
                float frontToBack)
            {
                Colour = colour;
                Brightness = brightness;
                Range = range;
                Dimmest = dimmest;
                Brightest = brightest;
                FlameMoves = flameMoves;
                HeightAboveTheFloor = heightAboveTheFloor;
                FrontToBack = frontToBack;
            }

            public Color Colour { get; }

            /// <summary>The prefab's own <c>Light.m_Intensity</c>.</summary>
            public float Brightness { get; }

            /// <summary>The prefab's own <c>Light.m_Range</c>, in tiles.</summary>
            public float Range { get; }

            /// <summary><c>LightFlickerEffect.minIntensity</c>.</summary>
            public float Dimmest { get; }

            /// <summary><c>LightFlickerEffect.maxIntensity</c>.</summary>
            public float Brightest { get; }

            /// <summary><c>LightFlickerEffect.enableMovement</c>.</summary>
            public bool FlameMoves { get; }

            /// <summary>The local Y the prefab gives its <c>LightOptimizer</c> node.</summary>
            public float HeightAboveTheFloor { get; }

            /// <summary>The local Z of that same node.</summary>
            /// <remarks>
            /// Zero on three of the six and negative on the other three. It is carried here
            /// because a preset that copied the height and dropped this would place a paper
            /// lantern's light a third of a tile away from where the game puts it.
            /// </remarks>
            public float FrontToBack { get; }
        }

        /// <summary>
        /// The numbers behind each named light, measured off the game's own prefabs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY ROW NAMES ITS SOURCE so that the day Pugstorm retunes a torch, the disagreement is
        /// findable rather than mysterious. The corpus is the ripped reference prefabs; the line
        /// numbers are of those files.
        /// </para>
        /// <para>
        /// A preset is only ever a starting point — nothing reads it after the copy, and nothing in
        /// generation branches on which one was chosen.
        /// </para>
        /// </remarks>
        public static Preset PresetFor(DimensionLightLike lightLike)
        {
            switch (lightLike)
            {
                case DimensionLightLike.Torch:
                    // Torch.prefab:873-875 (colour, intensity, range), :941-943 (flicker),
                    // :808 (LightOptimizer m_LocalPosition {x: 0, y: 0.75, z: 0}).
                    return new Preset(
                        new Color(1f, 0.93333334f, 0.8f, 1f), 0.65f, 5f, 0.5f, 0.65f, true,
                        0.75f, 0f);

                case DimensionLightLike.Campfire:
                    // Campfire.prefab:432-434, :500-502,
                    // :367 (LightOptimizer {x: 0, y: 0.75, z: 0}).
                    return new Preset(
                        new Color(1f, 0.8352941f, 0.5254902f, 1f), 0.65f, 7f, 0.6f, 0.7f, true,
                        0.75f, 0f);

                case DimensionLightLike.Lamp:
                    // Lamp.prefab:664-666, :732-734,
                    // :599 (LightOptimizer {x: 0, y: 0.75, z: 0}).
                    return new Preset(
                        new Color(1f, 1f, 1f, 1f), 0.75f, 7f, 0.7f, 0.8f, false,
                        0.75f, 0f);

                case DimensionLightLike.PaperLantern:
                    // ChineseLantern.prefab:374-376, :442-444,
                    // :309 (LightOptimizer {x: 0, y: 0.75, z: -0.3125}).
                    return new Preset(
                        new Color(1f, 1f, 1f, 1f), 0.75f, 6f, 0.7f, 0.8f, false,
                        0.75f, -0.3125f);

                case DimensionLightLike.CrystalLamp:
                    // CrystalLamp.prefab:7831-7833, :7899-7901,
                    // :7766 (LightOptimizer {x: 0, y: 2.7, z: -0.15}).
                    return new Preset(
                        new Color(0.5254902f, 0.81942016f, 1f, 1f), 0.65f, 6f, 0.6f, 0.7f, true,
                        2.7f, -0.15f);

                case DimensionLightLike.LampPost:
                    // LampPost.prefab:436-438, :504-506,
                    // :371 (LightOptimizer {x: 0, y: 2.2, z: -0.4}).
                    return new Preset(
                        new Color(0.8537736f, 0.99561495f, 1f, 1f), 0.65f, 6f, 0.6f, 0.7f, true,
                        2.2f, -0.4f);

                default:
                    // Custom, and anything a later enum value adds before this table knows about
                    // it: the torch, because it is the light this game is about.
                    return new Preset(
                        new Color(1f, 0.93333334f, 0.8f, 1f), 0.65f, 5f, 0.5f, 0.65f, true,
                        0.75f, 0f);
            }
        }

        /// <summary>Every named light this offers, in the order the dropdown shows them.</summary>
        /// <remarks>
        /// <c>Custom</c> is left out on purpose: it names no measurement, so a test walking this
        /// list has nothing to check it against.
        /// </remarks>
        public static DimensionLightLike[] EveryNamedLight()
        {
            return new[]
            {
                DimensionLightLike.Torch,
                DimensionLightLike.Campfire,
                DimensionLightLike.Lamp,
                DimensionLightLike.PaperLantern,
                DimensionLightLike.CrystalLamp,
                DimensionLightLike.LampPost
            };
        }
    }
}
