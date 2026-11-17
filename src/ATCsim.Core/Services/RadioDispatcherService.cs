using ATCsim.Core.Entities;

namespace ATCsim.Core.Services;

/// <summary>
/// Records simulation radio calls and pilot readbacks.
/// </summary>
public class RadioDispatcherService
{
    private readonly List<CommunicationLog> _logs = [];
    private readonly object _gate = new();

    public event Action<CommunicationLog>? MessageLogged;

    public IReadOnlyList<CommunicationLog> Logs
    {
        get { lock (_gate) return _logs.ToArray(); }
    }

    public CommunicationLog DispatchAtcInstruction(Aircraft aircraft, string instruction,
        string airportIcao = "SECTOR CONTROL", int airportId = 0, DateTime? time = null)
    {
        var log = new CommunicationLog
        {
            AirportId = airportId,
            AircraftId = aircraft.Id,
            Callsign = aircraft.Callsign,
            SenderType = "ATC",
            AirportIcao = airportIcao,
            Message = $"{aircraft.Callsign}, {instruction}",
            Timestamp = time ?? DateTime.UtcNow
        };

        return Publish(log);
    }

    public CommunicationLog GeneratePilotReadback(Aircraft aircraft, string readbackMessage,
        int airportId = 0, DateTime? time = null)
    {
        var log = new CommunicationLog
        {
            AirportId = airportId,
            AircraftId = aircraft.Id,
            Callsign = aircraft.Callsign,
            SenderType = "AIRCRAFT",
            AirportIcao = "PILOT " + aircraft.Callsign,
            Message = $"{readbackMessage}, {aircraft.Callsign}",
            Timestamp = time ?? DateTime.UtcNow
        };

        return Publish(log);
    }

    public void AddSystemLog(string category, string message)
    {
        var log = new CommunicationLog
        {
            SenderType = "SYSTEM",
            AirportIcao = category,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        Publish(log);
    }

    private CommunicationLog Publish(CommunicationLog log)
    {
        lock (_gate)
        {
            log.Id = _logs.Count + 1;
            _logs.Add(log);
        }
        MessageLogged?.Invoke(log);
        return log;
    }
}
