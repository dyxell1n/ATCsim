using ATCsim.Core.Entities;

namespace ATCsim.Core.WorldGeneration;

public record WorldSector
{
    public long Seed { get; set; }

    public List<Airport> Airports { get; set; } = [];
    public List<Aircraft> Aircraft { get; set; } = [];
    public List<WeatherZone> WeatherZones { get; set; } = [];
    public List<Waypoint> Waypoints { get; set; } = [];
    public List<TerrainPatch> Terrain { get; set; } = [];
}

public record Waypoint(string Name, decimal CoordX, decimal CoordY);

public record MapPoint(double X, double Y);

public record TerrainPatch(string Kind, List<MapPoint> Points);