namespace ATCsim.Core.Entities;

/// <summary>
/// Technical specifications and catalog data of an aircraft type.
/// Mirrors the 'aircraft_models' table in the relational data model.
/// </summary>
public class AircraftModel
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // HEAVY, PASSENGER, REGIONAL, LIGHT
    public decimal CruiseSpeed { get; set; } // knots
    public decimal MaxAltitude { get; set; } // feet
    public decimal FuelCapacity { get; set; } // kilograms
    public string IconType { get; set; } = "B738";
}
