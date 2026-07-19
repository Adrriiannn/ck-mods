using System;

namespace ExpandNullforge.Api
{
    [Flags]
    public enum DimensionCapabilityFlags
    {
        None = 0,
        LocalCoordinates = 1 << 0,
        AbsoluteCoordinates = 1 << 1,
        PlayerContext = 1 << 2,
        PlayerTravel = 1 << 3,
        Portals = 1 << 4,
        Map = 1 << 5,
        Minimap = 1 << 6,
        Markers = 1 << 7,
        Generation = 1 << 8,
        AreaLoading = 1 << 9,
        SimulationLoading = 1 << 10,
        Persistence = 1 << 11,
        Multiplayer = 1 << 12,
        Respawn = 1 << 13,
        Zones = 1 << 14,
        Bosses = 1 << 15,
        CustomBiomeLookup = 1 << 16,
        AutomationInterop = 1 << 17,
        CoordinateInterop = 1 << 18
    }
}
