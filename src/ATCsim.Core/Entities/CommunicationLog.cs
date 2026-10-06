namespace ATCsim.Core.Entities;

/// <summary>
/// Historical record of radio transmission exchanges between ATC and flight crews.
/// Mirrors the 'communication_logs' table in the relational data model.
/// </summary>
public class CommunicationLog
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public int AircraftId { get; set; }
    public string SenderType { get; set; } = string.Empty; // "ATC" or "AIRCRAFT"
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Optional navigation helpers
    public string? Callsign { get; set; }
    public string? AirportIcao { get; set; }
}
