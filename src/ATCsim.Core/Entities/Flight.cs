namespace ATCsim.Core.Entities;

/// <summary>
/// Scheduled or active commercial/general aviation flight.
/// Mirrors the 'flights' table in the relational data model.
/// </summary>
public class Flight
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public int AircraftId { get; set; }
    public int DepartureAirportId { get; set; }
    public int ArrivalAirportId { get; set; }
    public string Status { get; set; } = "SCHEDULED"; // SCHEDULED, EN_ROUTE, LANDED, CANCELLED
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public DateTime? EstimatedArrivalTime { get; set; }
    public int DelayMinutes { get; set; }
    public bool IsDelayed { get; set; }

    public Aircraft? Aircraft { get; set; }
    public Airport? DepartureAirport { get; set; }
    public Airport? ArrivalAirport { get; set; }
}
