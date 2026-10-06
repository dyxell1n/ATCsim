namespace ATCsim.Core.Entities;

/// <summary>
/// Represents a physical runway of an airport.
/// Mirrors the 'runways' table in the relational data model.
/// </summary>
public class Runway
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public string Designator { get; set; } = string.Empty;
    public int Angle { get; set; } // 0 - 360 degrees
    public decimal Length { get; set; } // meters
    public bool IsAvailable { get; set; } = true;
}
