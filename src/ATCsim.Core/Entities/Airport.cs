namespace ATCsim.Core.Entities;

/// <summary>
/// Represents an aerodrome in the airspace.
/// Mirrors the 'airports' table in the relational data model.
/// </summary>
public class Airport
{
    public int Id { get; set; }
    public string IcaoCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CoordX { get; set; }
    public decimal CoordY { get; set; }
    public int MaxCapacity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Runway> Runways { get; set; } = [];
}
