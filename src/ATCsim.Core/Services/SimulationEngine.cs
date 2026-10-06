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

    public long CurrentSeed { get; private set; } = 84920417;
    public bool IsRunning { get; private set; }
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

    public event Action? StateUpdated;
    public event Action<CollisionAlert>? ConflictDetected;

    public SimulationEngine()
    {
        _timer = new System.Timers.Timer(100); // 10 ticks per second (100ms)
        _timer.Elapsed += OnTimerTick;
        CollisionDetector.SeparationAlertTriggered += alert => ConflictDetected?.Invoke(alert);
    }

    public void LoadSector(WorldSector sector)
    {
        lock (_lock)
        {
            CurrentSeed = sector.Seed;
            Airports = [.. sector.Airports];
            ActiveAircraft = [.. sector.Aircraft];
            WeatherZones = [.. sector.WeatherZones];
            Waypoints = [.. sector.Waypoints];
            Weather.ActiveZones = WeatherZones;
        }

        Radio.AddSystemLog("ATC RADAR", $"Airspace sector loaded (Seed: {CurrentSeed}). {Airports.Count} airports, {ActiveAircraft.Count} active airborne tracks.");
        StateUpdated?.Invoke();
    }

    public void AddAircraft(Aircraft aircraft)
    {
        lock (_lock)
        {
            ActiveAircraft.Add(aircraft);
        }

        Radio.AddSystemLog("ATC RADAR", $"RADAR CONTACT: {aircraft.Callsign} ({aircraft.Model?.ModelName ?? "B738"}) entered sector at FL{(int)(aircraft.Altitude / 100)}.");
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
            }
        }

        Radio.AddSystemLog("ATC RADAR", $"Airport updated: {airport.IcaoCode} ({airport.Name}).");
        StateUpdated?.Invoke();
    }

    public void Start()
    {
        IsRunning = true;
        _timer.Start();
        Radio.AddSystemLog("SIM ENGINE", "Simulation running (realtime tracking active).");
        StateUpdated?.Invoke();
    }

    public void Pause()
    {
        IsRunning = false;
        _timer.Stop();
        Radio.AddSystemLog("SIM ENGINE", "Simulation paused.");
        StateUpdated?.Invoke();
    }

    public void TogglePlayback()
    {
        if (IsRunning) Pause();
        else Start();
    }

    public void SetTimeScale(double scale)
    {
        TimeScale = Math.Clamp(scale, 1.0, 8.0);
        Radio.AddSystemLog("SIM ENGINE", $"Simulation speed set to {TimeScale}x.");
        StateUpdated?.Invoke();
    }

    private void OnTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!IsRunning) return;

        // Visual simulation step: scaled so aircraft move smoothly across the map
        double dtSeconds = 0.1 * 10.0 * TimeScale;

        lock (_lock)
        {
            SimTime = SimTime.AddSeconds(dtSeconds);

            foreach (var aircraft in ActiveAircraft)
            {
                Tracker.UpdateAircraftPosition(aircraft, Weather, dtSeconds);
            }

            CollisionDetector.CheckSeparation(ActiveAircraft);
        }

        StateUpdated?.Invoke();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
