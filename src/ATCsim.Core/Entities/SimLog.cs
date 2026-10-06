namespace ATCsim.Core.Entities;

/// <summary>
/// Audit trail for simulation engine events, decisions, and system alerts.
/// Mirrors the 'sim_logs' table in the relational data model.
/// </summary>
public class SimLog
{
    public int Id { get; set; }
    public long WorldSeed { get; set; }
    public string EventType { get; set; } = string.Empty; // COLLISION_WARNING, TAKEOFF, LANDING, COMMAND, SYSTEM
    public int? FlightId { get; set; }
    public int? AircraftId { get; set; }
    public int? AirportId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime SimTime { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
