using ATCsim.Core.CollisionDetection;
using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Weather;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Core.Services;

/// <summary>
/// Central simulation loop running off the UI thread with deterministic delta time.
/// Coordinates kinematics, separation checks, and weather.
/// </summary>
public class SimulationEngine : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();
    private readonly AirportTrafficControl _trafficControl;

    public long CurrentSeed { get; private set; } = 84920417;
    public bool IsRunning { get; private set; }
    public const double MaxTimeScale = 16.0;
    public double TimeScale { get; private set; } = 1.0;
    public DateTime SimTime { get; private set; } = DateTime.UtcNow;

    public AircraftTracker Tracker { get; } = new();
    public CollisionDetector CollisionDetector { get; } = new();
    public WeatherService Weather { get; } = new();
    public RadioDispatcherService Radio { get; } = new();

    public List<Airport> Airports { get; private set; } = [];
    public List<Aircraft> ActiveAircraft { get; private set; } = [];
    public List<WeatherZone> WeatherZones { get; private set; } = [];
    public List<Waypoint> Waypoints { get; private set; } = [];

    private HashSet<(int, int)> _conflictPairs = [];
    private int _tickInProgress;
    private readonly HashSet<int> _conflictingAircraft = [];
    public IReadOnlySet<int> ConflictingAircraft => _conflictingAircraft;
    public event Action? StateUpdated;
    public event Action<CollisionAlert>? ConflictDetected;
    public event Action<Exception>? UpdateFailed;

    public SimulationEngine()
    {
        _trafficControl = new(Radio, Weather, CollisionDetector, () => SimTime);
        _timer = new System.Timers.Timer(100); // 10 ticks per second (100ms)
        _timer.Elapsed += OnTimerTick;
    }

    public void LoadSector(WorldSector sector, DateTime? simTime = null)
    {
        lock (_lock)
        {
            CurrentSeed = sector.Seed;
            SimTime = simTime ?? DateTime.UtcNow;
            ResetTraffic();
            Airports = [.. sector.Airports];
            ActiveAircraft = [.. sector.Aircraft];
            WeatherZones = [.. sector.WeatherZones];
            Waypoints = [.. sector.Waypoints];
            Weather.ActiveZones = WeatherZones;
        }

        Radio.AddSystemLog("ATC RADAR", $"Airspace sector loaded (Seed: {CurrentSeed}). {Airports.Count} airports, {ActiveAircraft.Count(a => a.IsAirborne)} active airborne tracks.");
        StateUpdated?.Invoke();
    }

    public void ReplaceAircraft(List<Aircraft> aircraft)
    {
        lock (_lock)
        {
            ResetTraffic();
            ActiveAircraft = [.. aircraft];
        }
        StateUpdated?.Invoke();
    }

    private void ResetTraffic()
    {
        _trafficControl.Reset();
        _conflictPairs.Clear();
        _conflictingAircraft.Clear();
    }

    public void ReadState(Action read)
    {
        lock (_lock) read();
    }

    public void AddAircraft(Aircraft aircraft)
    {
        lock (_lock)
        {
            ActiveAircraft.Add(aircraft);
        }

        string message = aircraft.PlannedDepartureSpeed.HasValue
            ? $"{aircraft.Callsign} is awaiting departure at {aircraft.DepartureAirportIcao}."
            : $"RADAR CONTACT: {aircraft.Callsign} ({aircraft.Model?.ModelName ?? "B738"}) entered sector at FL{(int)(aircraft.Altitude / 100)}.";
        Radio.AddSystemLog("ATC RADAR", message);
        StateUpdated?.Invoke();
    }

    public void AddWeatherZone(WeatherZone zone)
    {
        lock (_lock)
        {
            WeatherZones.Add(zone);
            Weather.ActiveZones = WeatherZones;
        }

        Radio.AddSystemLog("METEO ADVISORY", $"Hazard area '{zone.Name}' updated ({zone.ZoneType}).");
        StateUpdated?.Invoke();
    }

    public void AddAirport(Airport airport)
    {
        lock (_lock)
        {
            Airports.Add(airport);
        }

        Radio.AddSystemLog("ATC RADAR", $"New airport registered: {airport.IcaoCode} ({airport.Name}).");
        StateUpdated?.Invoke();
    }

    public void UpdateAirport(Airport airport)
    {
        lock (_lock)
        {
            var existing = Airports.FirstOrDefault(a => a.IcaoCode == airport.IcaoCode || a.Id == airport.Id);
            if (existing != null)
            {
                existing.Name = airport.Name;
                existing.CoordX = airport.CoordX;
                existing.CoordY = airport.CoordY;
                existing.MaxCapacity = airport.MaxCapacity;
                existing.Runways = airport.Runways;

                foreach (var aircraft in ActiveAircraft)
                {
                    if (aircraft.PlannedDepartureSpeed is not { } speed ||
                        (aircraft.DepartureAirportIcao != existing.IcaoCode && aircraft.ArrivalAirportIcao != existing.IcaoCode))
                    {
                        continue;
                    }

                    var departure = Airports.Find(a => a.IcaoCode == aircraft.DepartureAirportIcao);
                    var arrival = Airports.Find(a => a.IcaoCode == aircraft.ArrivalAirportIcao);
                    if (departure != null && arrival != null)
                        AircraftTracker.ScheduleDeparture(aircraft, departure, arrival, aircraft.TargetAltitude, speed);
                }
            }
        }

        Radio.AddSystemLog("ATC RADAR", $"Airport updated: {airport.IcaoCode} ({airport.Name}).");
        StateUpdated?.Invoke();
    }

    public void Start()
    {
        lock (_lock)
        {
            IsRunning = true;
            _timer.Start();
        }
        Radio.AddSystemLog("SIM ENGINE", "Simulation running (realtime tracking active).");
        StateUpdated?.Invoke();
    }

    public void Pause()
    {
        lock (_lock)
        {
            IsRunning = false;
            _timer.Stop();
        }
        Radio.AddSystemLog("SIM ENGINE", "Simulation paused.");
        StateUpdated?.Invoke();
    }

    public void SetTimeScale(double scale)
    {
        if (!double.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        lock (_lock) TimeScale = Math.Clamp(scale, 1.0, MaxTimeScale);
        Radio.AddSystemLog("SIM ENGINE", $"Simulation speed set to {TimeScale}x.");
        StateUpdated?.Invoke();
    }

    private void OnTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (Interlocked.Exchange(ref _tickInProgress, 1) != 0) return;
        try
        {
            lock (_lock)
            {
                if (!IsRunning) return;
                AdvanceTraffic(TimeSpan.FromMilliseconds(_timer.Interval).TotalSeconds * TimeScale);
            }
            StateUpdated?.Invoke();
        }
        catch (Exception exception)
        {
            lock (_lock)
            {
                IsRunning = false;
                _timer.Stop();
            }
            UpdateFailed?.Invoke(exception);
        }
        finally
        {
            Volatile.Write(ref _tickInProgress, 0);
        }
    }

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        lock (_lock) AdvanceTraffic(seconds);
        StateUpdated?.Invoke();
    }

    private void AdvanceTraffic(double seconds)
    {
        while (seconds > 0)
        {
            double step = Math.Min(seconds, 1);
            seconds -= step;
            SimTime = SimTime.AddSeconds(step);
            _trafficControl.BeginStep(ActiveAircraft, step);
            foreach (var aircraft in ActiveAircraft)
                _trafficControl.Update(aircraft, ActiveAircraft, Airports, SimTime, step, Tracker);
            UpdateConflicts();
        }
    }

    private void UpdateConflicts()
    {
        var current = new HashSet<(int, int)>();
        _conflictingAircraft.Clear();
        foreach (var alert in CollisionDetector.CheckSeparation(ActiveAircraft))
        {
            var pair = (alert.Aircraft1.Id, alert.Aircraft2.Id);
            current.Add(pair);
            _conflictingAircraft.Add(pair.Item1);
            _conflictingAircraft.Add(pair.Item2);
            if (!_conflictPairs.Contains(pair)) ConflictDetected?.Invoke(alert);
        }
        _conflictPairs = current;
    }

    public void VectorAircraft(Aircraft aircraft, double heading)
    {
        if (!double.IsFinite(heading)) throw new ArgumentOutOfRangeException(nameof(heading));
        lock (_lock) _trafficControl.SetHeading(aircraft, ArrivalAirport(aircraft), heading);
    }

    public void ResumeAircraftRoute(Aircraft aircraft)
    {
        lock (_lock) _trafficControl.ResumeRoute(aircraft, ArrivalAirport(aircraft));
    }

    public void HoldAircraft(Aircraft aircraft)
    {
        lock (_lock)
            if (ArrivalAirport(aircraft) != null) _trafficControl.RequestHolding(aircraft);
    }

    public void ChangeAircraftAltitude(Aircraft aircraft, decimal altitude)
    {
        lock (_lock) _trafficControl.ChangeAltitude(aircraft, ArrivalAirport(aircraft), altitude);
    }

    private Airport? ArrivalAirport(Aircraft aircraft) => Airports.Find(airport => airport.IcaoCode == aircraft.ArrivalAirportIcao);

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

