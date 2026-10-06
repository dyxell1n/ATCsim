namespace ATCsim.Core.Entities;

/// <summary>
/// Physical aircraft active in the airspace.
/// Mirrors the 'aircrafts' table in the relational data model.
/// </summary>
public class Aircraft
{
    public int Id { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public int ModelId { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal CoordX { get; set; }
    public decimal CoordY { get; set; }
    public decimal Altitude { get; set; } // feet (e.g. 28000 for FL280)
    public decimal FuelRemaining { get; set; } // kilograms

    // Kinematics and flight dynamics in simulation
    public double Heading { get; set; } // 0 - 359 degrees
    public decimal GroundSpeed { get; set; } // knots
    public double TargetHeading { get; set; }
    public decimal TargetAltitude { get; set; }

    // Route and Navigation Destination
    public string DepartureAirportIcao { get; set; } = "UKLL";
    public string ArrivalAirportIcao { get; set; } = "UKBB";
    public double OriginX { get; set; } = 140.0;
    public double OriginY { get; set; } = 430.0;
    public double DestinationX { get; set; } = 580.0;
    public double DestinationY { get; set; } = 260.0;
    public bool HasManualHeadingOverride { get; set; }
    public double TrailTimer { get; set; }

    // Breadcrumb trail points recorded during flight (X, Y, Altitude)
    public List<TrailPoint> TrailPoints { get; set; } = [];

    public AircraftModel? Model { get; set; }
}

public record struct TrailPoint(double X, double Y, decimal Altitude);

