namespace ATCsim.Core.Entities;

/// <summary>
/// Geospatial hazard or restricted airspace polygon/circle.
/// Mirrors the 'weather_zones' table in the relational data model.
/// </summary>
public class WeatherZone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ZoneType { get; set; } = "STORM"; // STORM, NO_FLY_ZONE, MILITARY_DANGER
    public decimal CenterX { get; set; }
    public decimal CenterY { get; set; }
    public decimal Radius { get; set; } // nautical miles
    public decimal? MinAltitude { get; set; } // feet
    public decimal? MaxAltitude { get; set; } // feet
    public bool IsActive { get; set; } = true;
}
