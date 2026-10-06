using ATCsim.Core.Entities;

namespace ATCsim.Data.Dto;

/// <summary>
/// Data Transfer Object for exporting/importing session state or historical flight telemetry.
/// </summary>
public record SessionSnapshotDto
{
    public long WorldSeed { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public List<Airport> Airports { get; init; } = [];
    public List<Aircraft> ActiveAircraft { get; init; } = [];
    public List<CommunicationLog> CommunicationLogs { get; init; } = [];
}
