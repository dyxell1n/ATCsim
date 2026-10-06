using ATCsim.Core.Entities;

namespace ATCsim.Core.Services;

/// <summary>
/// Manages ICAO-compliant radiotelephony communications between ATC and aircraft.
/// </summary>
public class RadioDispatcherService
{
    private readonly List<CommunicationLog> _logs = [];

    public event Action<CommunicationLog>? MessageLogged;

    public IReadOnlyList<CommunicationLog> Logs => _logs;

    public CommunicationLog DispatchAtcInstruction(Aircraft aircraft, string instruction, string airportIcao = "UKLL TOWER")
    {
        var log = new CommunicationLog
        {
            Id = _logs.Count + 1,
            AircraftId = aircraft.Id,
            Callsign = aircraft.Callsign,
            SenderType = "ATC",
            AirportIcao = airportIcao,
            Message = $"{aircraft.Callsign}, {instruction}",
            Timestamp = DateTime.UtcNow
        };

        _logs.Add(log);
        MessageLogged?.Invoke(log);
        return log;
    }

    public CommunicationLog GeneratePilotReadback(Aircraft aircraft, string readbackMessage)
    {
        var log = new CommunicationLog
        {
            Id = _logs.Count + 1,
            AircraftId = aircraft.Id,
            Callsign = aircraft.Callsign,
            SenderType = "AIRCRAFT",
            AirportIcao = "PILOT " + aircraft.Callsign,
            Message = $"{readbackMessage}, {aircraft.Callsign}",
            Timestamp = DateTime.UtcNow
        };

        _logs.Add(log);
        MessageLogged?.Invoke(log);
        return log;
    }

    public void AddSystemLog(string category, string message)
    {
        var log = new CommunicationLog
        {
            Id = _logs.Count + 1,
            SenderType = "SYSTEM",
            AirportIcao = category,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        _logs.Add(log);
        MessageLogged?.Invoke(log);
    }
}
