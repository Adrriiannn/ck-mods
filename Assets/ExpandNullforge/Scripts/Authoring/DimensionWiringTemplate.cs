using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What part an object plays in Core Keeper's wiring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured across the 125 vanilla prefabs that carry <c>ElectricityAuthoring</c>. The roles are
    /// not a taxonomy invented here — they fall straight out of which fields each prefab sets, and the
    /// split is unusually clean:
    /// </para>
    /// <list type="bullet">
    /// <item><c>isWire</c> is set on exactly ONE prefab in the whole game, <c>ElectricalWire</c>.</item>
    /// <item><c>isLever</c> is set on 15 — the lever, the pressure plate, both proximity sensors, the
    /// dynamos and the powered doors. Everything a player or a creature can trip.</item>
    /// <item><c>sourceEnergy &gt; 0</c> marks what actually emits power, at 1, 5, 8, 13, 20 or 25.</item>
    /// <item><c>circuitType</c> is <c>None</c> on 113 of them; only Delay and Condition circuits are
    /// anything else.</item>
    /// </list>
    /// <para>
    /// Worth knowing that these are not exclusive in vanilla: the lever is a switch AND a source
    /// (<c>sourceEnergy</c> 13) AND blocks current. So the role picks the shape and the fields below
    /// stay reachable, rather than the role locking them.
    /// </para>
    /// </remarks>
    public enum DimensionWiringRole
    {
        /// <summary>Not part of the wiring at all. The default.</summary>
        NotWired = 0,

        /// <summary>It produces power on its own, like a generator.</summary>
        PowerSource = 1,

        /// <summary>Something trips it and it sends a signal — a lever, a plate, a sensor.</summary>
        Switch = 2,

        /// <summary>It carries current between other things.</summary>
        Wire = 3,

        /// <summary>It delays or conditions the signal passing through it.</summary>
        LogicCircuit = 4,

        /// <summary>It does something when powered — a door, a drill, a spawner.</summary>
        PoweredDevice = 5
    }

    /// <summary>
    /// How a signal travels through a wire.
    /// </summary>
    /// <remarks>
    /// Core Keeper's <c>CircuitConnectionMode</c>. The shapes are what a player sees when they lay
    /// wire and it joins up: a straight run, a corner, a T-junction, a crossing.
    /// </remarks>
    public enum DimensionWiringShape
    {
        /// <summary>Joins however the wire around it happens to run.</summary>
        Automatic = 0,

        /// <summary>Only along the direction it was placed.</summary>
        AlongItsDirection = 1,

        /// <summary>A crossing.</summary>
        Cross = 2,

        /// <summary>A T-junction.</summary>
        TJunction = 4,

        /// <summary>A corner.</summary>
        Corner = 8,

        /// <summary>A straight run.</summary>
        Straight = 16
    }

    /// <summary>
    /// What an object does with electricity, if anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Composable rather than an asset of its own, because being wired is something an object IS
    /// alongside everything else it is — a powered door is still a door, a drill is still a machine
    /// you place and break. It sits beside a workbench's crafting or a container's inventory the same
    /// way.
    /// </para>
    /// <para>
    /// Every field maps to one on <c>ElectricityAuthoring</c>, which has only seven. The whole of
    /// Core Keeper's wiring is a small component and a lot of systems reading it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionWiringTemplate
    {
        [Tooltip("What part it plays in a circuit. NotWired leaves it out of the wiring entirely.")]
        [SerializeField] private DimensionWiringRole role = DimensionWiringRole.NotWired;

        [Tooltip("How much power it puts into the circuit. Vanilla sources use 1, 5, 8, 13, 20 or 25.")]
        [Min(0)]
        [SerializeField] private int powerProduced;

        [Tooltip("Current cannot pass through it. True on levers, sensors, generators and logic circuits.")]
        [SerializeField] private bool blocksCurrent;

        [Tooltip("Which way it faces in the circuit, for things that care about direction.")]
        [SerializeField] private Vector2Int direction = Vector2Int.zero;

        [Tooltip("How a wire joins up with the wire around it.")]
        [SerializeField] private DimensionWiringShape shape = DimensionWiringShape.Automatic;

        [Tooltip("A logic circuit that holds the signal before passing it on, rather than testing a condition.")]
        [SerializeField] private bool delaysInsteadOfConditions;

        public DimensionWiringRole Role
        {
            get { return role; }
        }

        /// <summary>Whether it takes part in the wiring at all.</summary>
        public bool IsWired
        {
            get { return role != DimensionWiringRole.NotWired; }
        }

        /// <summary>Power it produces, which only a source or a switch actually emits.</summary>
        /// <remarks>
        /// Reported as zero for the roles that cannot emit, so a leftover number on an object that was
        /// changed from a generator into a door does not quietly keep powering the circuit.
        /// </remarks>
        public int PowerProduced
        {
            get
            {
                if (role != DimensionWiringRole.PowerSource && role != DimensionWiringRole.Switch)
                {
                    return 0;
                }

                return powerProduced < 0 ? 0 : powerProduced;
            }
        }

        /// <summary>Whether current stops at it.</summary>
        /// <remarks>
        /// A wire that blocks current is a contradiction — it is the one thing whose whole job is to
        /// pass current along — so the answer is forced for that role rather than trusted.
        /// </remarks>
        public bool BlocksCurrent
        {
            get { return role != DimensionWiringRole.Wire && blocksCurrent; }
        }

        public Vector2Int Direction
        {
            get { return direction; }
        }

        public DimensionWiringShape Shape
        {
            get { return role == DimensionWiringRole.Wire ? shape : DimensionWiringShape.Automatic; }
        }

        /// <summary>Whether it is the kind of thing a player or creature trips.</summary>
        public bool IsSwitch
        {
            get { return role == DimensionWiringRole.Switch; }
        }

        public bool IsWire
        {
            get { return role == DimensionWiringRole.Wire; }
        }

        public bool DelaysInsteadOfConditions
        {
            get { return delaysInsteadOfConditions; }
        }

        /// <summary>Whether a source was set up to produce nothing.</summary>
        /// <remarks>
        /// It still places, still looks like a generator, and powers nothing — the kind of mistake that
        /// only shows up when somebody wires a room to it and wonders why the door will not open.
        /// </remarks>
        public bool ProducesNoPower
        {
            get { return role == DimensionWiringRole.PowerSource && PowerProduced == 0; }
        }
    }
}
