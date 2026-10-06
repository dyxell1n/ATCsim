namespace ATCsim.Core.Entities;

/// <summary>
/// Point meteorological measurement data.
/// Mirrors the 'weather' table in the relational data model.
/// </summary>
public class WeatherInfo
{
    public int Id { get; set; }
    public decimal CoordX { get; set; }
    public decimal CoordY { get; set; }
    public decimal WindSpeed { get; set; } // knots
    public int WindDirection { get; set; } // 0 - 360 degrees
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
