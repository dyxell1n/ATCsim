using ATCsim.Core.Entities;

namespace ATCsim.Core.WorldGeneration;

/// <summary>
/// Container for procedural or static sector layout generated for a given seed.
/// </summary>
public record WorldSector
{
    public long Seed { get; set; }
    public List<Airport> Airports { get; set; } = [];
    public List<Aircraft> Aircraft { get; set; } = [];
    public List<WeatherZone> WeatherZones { get; set; } = [];
    public List<Waypoint> Waypoints { get; set; } = [];
}

public record Waypoint(string Name, decimal CoordX, decimal CoordY);
