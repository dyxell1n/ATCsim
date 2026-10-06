using System.Windows;
using System.Windows.Input;
using ATCsim.Core.Entities;
using ATCsim.Core.Services;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Repositories;

namespace ATCsim.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SimulationEngine _engine;
    private readonly RepositoryManager _repoManager;
    private readonly IWorldGenerator _worldGenerator;

    private string _seedInput = "84920417";
    private string _systemStatusText = "ONLINE (60 FPS)";
    private string _airspaceText = "UKRAINE FIR";
    private string _activeTargetsText = "4 AIRBORNE";
    private string _simTimeText = "12:44:15 UTC";
    private string _windText = "310° @ 14 KTS";
    private string _speedLabel = "1x";
    private string _databaseStatusText = "DB: ONLINE (SQLite • 9 Tables)";
    private bool _isRunning;

    // Flight route banner properties
    private string _depIcao = "UKLL";
    private string _depName = "Lviv Danylo Halytskyi";
    private string _depTimes = "STD 12:15 UTC • ATD 12:18 UTC";
    private string _arrIcao = "UKBB";
    private string _arrName = "Kyiv Boryspil";
    private string _arrTimes = "STA 13:20 UTC • RWY 36R (Open)";
    private string _flightCallsign = "UKR102";
    private string _flightModel = "Boeing 737-800";
    private string _flightStatusBadge = "ON TIME (+0m)";
    private string _flightEta = "ETA 13:20 UTC (38m remaining)";
    private string _flightRoute = "UKLL ➔ TMR ➔ SLV ➔ KVR ➔ UKBB";
    private double _flightProgressPercent = 55.0;
    private string _flightDistanceFlown = "260 km flown (55%)";
    private string _flightDistanceRemaining = "210 km remaining | Total: 470 km";

    private GridLength _flownGridLength = new(55, GridUnitType.Star);
    private GridLength _remainingGridLength = new(45, GridUnitType.Star);

    public RadarViewModel Radar { get; }
    public FlightInspectorVM Inspector { get; }
    public LogsViewModel Logs { get; }

    public string SeedInput
    {
        get => _seedInput;
        set => SetProperty(ref _seedInput, value);
    }

    public string SystemStatusText
    {
        get => _systemStatusText;
        set => SetProperty(ref _systemStatusText, value);
    }

    public string AirspaceText
    {
        get => _airspaceText;
        set => SetProperty(ref _airspaceText, value);
    }

    public string ActiveTargetsText
    {
        get => _activeTargetsText;
        set => SetProperty(ref _activeTargetsText, value);
    }

    public string SimTimeText
    {
        get => _simTimeText;
        set => SetProperty(ref _simTimeText, value);
    }

    public string WindText
    {
        get => _windText;
        set => SetProperty(ref _windText, value);
    }

    public string SpeedLabel
    {
        get => _speedLabel;
        set => SetProperty(ref _speedLabel, value);
    }

    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        set => SetProperty(ref _databaseStatusText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public string DepIcao
    {
        get => _depIcao;
        set => SetProperty(ref _depIcao, value);
    }

    public string DepName
    {
        get => _depName;
        set => SetProperty(ref _depName, value);
    }

    public string DepTimes
    {
        get => _depTimes;
        set => SetProperty(ref _depTimes, value);
    }

    public string ArrIcao
    {
        get => _arrIcao;
        set => SetProperty(ref _arrIcao, value);
    }

    public string ArrName
    {
        get => _arrName;
        set => SetProperty(ref _arrName, value);
    }

    public string ArrTimes
    {
        get => _arrTimes;
        set => SetProperty(ref _arrTimes, value);
    }

    public string FlightCallsign
    {
        get => _flightCallsign;
        set => SetProperty(ref _flightCallsign, value);
    }

    public string FlightModel
    {
        get => _flightModel;
        set => SetProperty(ref _flightModel, value);
    }

    public string FlightStatusBadge
    {
        get => _flightStatusBadge;
        set => SetProperty(ref _flightStatusBadge, value);
    }

    public string FlightEta
    {
        get => _flightEta;
        set => SetProperty(ref _flightEta, value);
    }

    public string FlightRoute
    {
        get => _flightRoute;
        set => SetProperty(ref _flightRoute, value);
    }

    public double FlightProgressPercent
    {
        get => _flightProgressPercent;
        set
        {
            if (SetProperty(ref _flightProgressPercent, value))
            {
                double val = Math.Clamp(value, 1.0, 99.0);
                FlownGridLength = new GridLength(val, GridUnitType.Star);
                RemainingGridLength = new GridLength(100.0 - val, GridUnitType.Star);
            }
        }
    }

    public GridLength FlownGridLength
    {
        get => _flownGridLength;
        private set => SetProperty(ref _flownGridLength, value);
    }

    public GridLength RemainingGridLength
    {
        get => _remainingGridLength;
        private set => SetProperty(ref _remainingGridLength, value);
    }

    public string FlightDistanceFlown
    {
        get => _flightDistanceFlown;
        set => SetProperty(ref _flightDistanceFlown, value);
    }

    public string FlightDistanceRemaining
    {
        get => _flightDistanceRemaining;
        set => SetProperty(ref _flightDistanceRemaining, value);
    }

    public ICommand RunCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ToggleSpeedCommand { get; }
    public ICommand GenerateWorldCommand { get; }
    public ICommand AddAircraftCommand { get; }
    public ICommand AddZoneCommand { get; }
    public ICommand AddAirportCommand { get; }
    public ICommand EditAirportCommand { get; }
    public ICommand ExitAndSaveCommand { get; }

    public MainViewModel()
        : this(new SimulationEngine(), new RepositoryManager(), new WorldGenerator())
    {
    }

    public MainViewModel(SimulationEngine engine, RepositoryManager repoManager, IWorldGenerator worldGenerator)
    {
        _engine = engine;
        _repoManager = repoManager;
        _worldGenerator = worldGenerator;

        Radar = new RadarViewModel(_engine);
        Logs = new LogsViewModel();
        Inspector = new FlightInspectorVM(_engine, (cmd, readback) =>
        {
            string cs = Radar.SelectedTrack?.Callsign ?? "UKR102";
            int planeId = Radar.SelectedTrack?.Aircraft.Id ?? 1;

            var logEntry = new CommunicationLog
            {
                AirportId = 1,
                AircraftId = planeId,
                SenderType = "ATC",
                Message = $"{cs}, {cmd}.",
                Timestamp = DateTime.UtcNow
            };

            // Write live to SQLite database
            _repoManager.Logs.AddCommunicationLog(logEntry);
            UpdateDatabaseStats();

            Logs.AddLog("• ATC RADIO", "UKLL TOWER", $"{cs}, {cmd}.", isRadio: true);
            Logs.AddLog("• READBACK", "PILOT " + cs, $"{readback}, {cs}.", isRadio: true);
        });

        Radar.TrackSelected += aircraft =>
        {
            if (aircraft != null)
            {
                Inspector.SetSelectedAircraft(aircraft);
                UpdateFlightRouteBanner(aircraft);
            }
        };

        Radar.AirportSelected += airport =>
        {
            if (airport != null)
            {
                Inspector.SelectedAirport = Inspector.AvailableAirports.FirstOrDefault(a => a.Icao == airport.IcaoCode);
                Inspector.ActiveTab = "AIRPORT";
            }
        };

        RunCommand = new RelayCommand(() =>
        {
            _engine.Start();
            IsRunning = true;
            _repoManager.Logs.AddSimLog(new SimLog
            {
                WorldSeed = _engine.CurrentSeed,
                EventType = "SIM_START",
                Details = "Simulation loop started by operator.",
                SimTime = _engine.SimTime
            });
            UpdateDatabaseStats();
        });

        PauseCommand = new RelayCommand(() =>
        {
            _engine.Pause();
            IsRunning = false;
            _repoManager.Logs.AddSimLog(new SimLog
            {
                WorldSeed = _engine.CurrentSeed,
                EventType = "SIM_PAUSE",
                Details = "Simulation loop paused by operator.",
                SimTime = _engine.SimTime
            });
            UpdateDatabaseStats();
        });

        ToggleSpeedCommand = new RelayCommand(() =>
        {
            double next = _engine.TimeScale switch
            {
                1.0 => 2.0,
                2.0 => 4.0,
                4.0 => 8.0,
                _ => 1.0
            };
            _engine.SetTimeScale(next);
            SpeedLabel = $"{(int)next}x";
        });

        GenerateWorldCommand = new RelayCommand(GenerateFromSeed);

        AddAircraftCommand = new RelayCommand(ExecuteAddAircraft);
        AddZoneCommand = new RelayCommand(ExecuteAddZone);
        AddAirportCommand = new RelayCommand(ExecuteAddAirport);
        EditAirportCommand = new RelayCommand(ExecuteEditAirport);
        ExitAndSaveCommand = new RelayCommand(ExecuteExitAndSave);

        // Wire simulation engine events
        _engine.StateUpdated += OnSimulationStateUpdated;
        _engine.ConflictDetected += alert =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Radar.MarkConflict(alert.Aircraft1.Callsign, alert.Aircraft2.Callsign);
                Logs.AddLog(
                    "• LOSS OF SEPARATION",
                    "SAFETY ALERT",
                    $"TRAFFIC ALERT: {alert.Aircraft1.Callsign} and {alert.Aircraft2.Callsign} separation {alert.HorizontalDistanceNm} NM / {alert.VerticalDistanceFt} ft!",
                    isAlert: true);

                _repoManager.Logs.AddSimLog(new SimLog
                {
                    WorldSeed = _engine.CurrentSeed,
                    EventType = "COLLISION_WARNING",
                    AircraftId = alert.Aircraft1.Id,
                    Details = $"Separation conflict with {alert.Aircraft2.Callsign} (H: {alert.HorizontalDistanceNm} NM, V: {alert.VerticalDistanceFt} ft)",
                    SimTime = _engine.SimTime
                });
                UpdateDatabaseStats();
            });
        };

        _engine.Radio.MessageLogged += log =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Logs.AddLog(log);
            });
        };

        // Initialize sector baseline from database or world generator
        LoadInitialBaseline();
    }

    private void ExecuteAddAircraft()
    {
        var dlg = new Views.Dialogs.AddAircraftWindow(_engine.Airports)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() == true && dlg.CreatedAircraft != null)
        {
            var plane = dlg.CreatedAircraft;
            plane.Id = _engine.ActiveAircraft.Count + 1;
            _engine.AddAircraft(plane);

            var trackVm = new AircraftTrackViewModel(plane);
            Radar.Tracks.Add(trackVm);
            Radar.SelectedTrack = trackVm;

            // Sync to SQLite database
            _repoManager.Aircraft.UpdateTelemetry(plane.Id, plane.CoordX, plane.CoordY, plane.Altitude, plane.FuelRemaining);
            UpdateDatabaseStats();
            UpdateFlightRouteBanner(plane);
        }
    }

    private void ExecuteAddZone()
    {
        var dlg = new Views.Dialogs.AddZoneWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() == true && dlg.CreatedZone != null)
        {
            var newZone = dlg.CreatedZone;
            newZone.Id = _engine.WeatherZones.Count + 1;
            _engine.AddWeatherZone(newZone);
            Logs.AddLog("• METEO", "RADAR CENTER", $"New weather restriction {newZone.Name} ({newZone.ZoneType}) activated.", isRadio: false);
            UpdateDatabaseStats();
        }
    }

    private void ExecuteAddAirport()
    {
        var dlg = new Views.Dialogs.AddAirportWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() == true && dlg.CreatedAirport != null)
        {
            var apt = dlg.CreatedAirport;
            apt.Id = _engine.Airports.Count + 1;
            if (apt.Runways.Count > 0) apt.Runways[0].AirportId = apt.Id;

            _engine.AddAirport(apt);
            _repoManager.Airports.AddAirport(apt);
            Radar.RefreshAirportsList(_engine.Airports);
            Inspector.LoadAirports(_engine.Airports);
            UpdateDatabaseStats();
        }
    }

    private void ExecuteEditAirport()
    {
        var selected = Inspector.SelectedAirport;
        if (selected == null) return;

        var targetApt = _engine.Airports.FirstOrDefault(a => a.IcaoCode == selected.Icao);
        if (targetApt == null) return;

        var dlg = new Views.Dialogs.EditAirportWindow(targetApt)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dlg.ShowDialog() == true)
        {
            _engine.UpdateAirport(targetApt);
            _repoManager.Airports.UpdateAirport(targetApt);
            Radar.RefreshAirportsList(_engine.Airports);
            Inspector.LoadAirports(_engine.Airports);
            UpdateDatabaseStats();
        }
    }

    private void ExecuteExitAndSave()
    {
        _engine.Pause();
        _repoManager.SaveSessionProgress(
            _engine.CurrentSeed,
            _engine.SimTime,
            _engine.TimeScale,
            _engine.Airports,
            _engine.ActiveAircraft,
            _engine.WeatherZones);

        _repoManager.Logs.AddSimLog(new SimLog
        {
            WorldSeed = _engine.CurrentSeed,
            EventType = "SESSION_SAVED_EXIT",
            Details = $"Session progress saved: {_engine.ActiveAircraft.Count} aircraft, {_engine.Airports.Count} airports.",
            SimTime = _engine.SimTime
        });

        MessageBox.Show(
            "Прогрес симуляції (позиції літаків, історія траєкторій, аеропорти та логи) успішно збережено в базі даних SQLite!\n\nПрограма завершує роботу.",
            "ATCsim - Збереження та вихід",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Application.Current?.Shutdown();
    }

    private void UpdateFlightRouteBanner(Aircraft ac)
    {
        var depApt = _engine.Airports.FirstOrDefault(a => a.IcaoCode == ac.DepartureAirportIcao);
        var arrApt = _engine.Airports.FirstOrDefault(a => a.IcaoCode == ac.ArrivalAirportIcao);

        DepIcao = ac.DepartureAirportIcao;
        DepName = depApt?.Name ?? "Origin Aerodrome";
        ArrIcao = ac.ArrivalAirportIcao;
        ArrName = arrApt?.Name ?? "Destination Aerodrome";

        FlightCallsign = ac.Callsign;
        FlightModel = ac.Model?.ModelName ?? "Boeing 737-800";
        FlightRoute = $"{ac.DepartureAirportIcao} ➔ {ac.ArrivalAirportIcao}";

        double totalDist = Math.Sqrt(Math.Pow(ac.DestinationX - ac.OriginX, 2) + Math.Pow(ac.DestinationY - ac.OriginY, 2));
        if (totalDist <= 0) totalDist = 450.0;

        double flownDist = Math.Sqrt(Math.Pow((double)ac.CoordX - ac.OriginX, 2) + Math.Pow((double)ac.CoordY - ac.OriginY, 2));
        double remDist = Math.Sqrt(Math.Pow(ac.DestinationX - (double)ac.CoordX, 2) + Math.Pow(ac.DestinationY - (double)ac.CoordY, 2));

        double pct = Math.Clamp((flownDist / totalDist) * 100.0, 5.0, 95.0);
        FlightProgressPercent = pct;
        FlownGridLength = new GridLength(Math.Max(1.0, pct), GridUnitType.Star);
        RemainingGridLength = new GridLength(Math.Max(1.0, 100.0 - pct), GridUnitType.Star);

        int flownKm = (int)(flownDist);
        int remKm = (int)(remDist);
        int totalKm = (int)(totalDist);
        FlightDistanceFlown = $"{flownKm} km flown ({(int)pct}%)";
        FlightDistanceRemaining = $"{remKm} km remaining | Total: {totalKm} km";
    }

    private void LoadInitialBaseline()
    {
        if (_repoManager.HasSavedSession())
        {
            var saved = _repoManager.LoadSavedSession();
            if (saved.HasValue && saved.Value.Aircraft.Count > 0)
            {
                var (seed, simTime, timeScale, aircraftList) = saved.Value;
                var airports = _repoManager.Airports.GetAll();

                var savedSector = _worldGenerator.GenerateSector(seed);
                savedSector.Aircraft = aircraftList;
                if (airports.Count > 0) savedSector.Airports = airports;

                _engine.LoadSector(savedSector);
                _engine.SetTimeScale(timeScale);
                Radar.RefreshSector(savedSector);
                Inspector.LoadAirports(_engine.Airports);

                if (savedSector.Aircraft.Count > 0)
                {
                    Inspector.SetSelectedAircraft(savedSector.Aircraft[0]);
                    UpdateFlightRouteBanner(savedSector.Aircraft[0]);
                }

                Logs.AddLog("• RESTORE", "STORAGE ENGINE", $"Попередню збережену сесію (Seed: {seed}) завантажено з бази даних SQLite.", isRadio: false);
                UpdateDatabaseStats();
                return;
            }
        }

        long defaultSeed = 84920417;
        if (long.TryParse(SeedInput, out long s)) defaultSeed = s;

        // Generate baseline sector
        var sector = _worldGenerator.GenerateSector(defaultSeed);
        _engine.LoadSector(sector);
        Radar.RefreshSector(sector);

        // Sync complete sector directly into SQLite database
        _repoManager.SyncSectorToDatabase(sector);
        UpdateDatabaseStats();

        // Load airports into Inspector for airport tab
        Inspector.LoadAirports(_repoManager.Airports.GetAll());

        if (sector.Aircraft.Count > 0)
        {
            Inspector.SetSelectedAircraft(sector.Aircraft[0]);
            UpdateFlightRouteBanner(sector.Aircraft[0]);
        }

        // Load historical communication logs from SQLite
        var dbLogs = _repoManager.Logs.GetCommunicationLogs(10);
        foreach (var log in dbLogs)
        {
            Logs.AddLog(log);
        }
    }

    private void GenerateFromSeed()
    {
        if (!long.TryParse(SeedInput, out long seed))
        {
            seed = DateTime.UtcNow.Ticks % 100000000;
            SeedInput = seed.ToString();
        }

        var sector = _worldGenerator.GenerateSector(seed);
        _engine.LoadSector(sector);
        Radar.RefreshSector(sector);

        // Sync freshly generated sector to SQLite
        _repoManager.SyncSectorToDatabase(sector);
        UpdateDatabaseStats();

        Inspector.LoadAirports(_repoManager.Airports.GetAll());

        if (sector.Aircraft.Count > 0)
        {
            Inspector.SetSelectedAircraft(sector.Aircraft[0]);
            UpdateFlightRouteBanner(sector.Aircraft[0]);
        }

        Logs.AddLog("• WORLD GEN", "SEED ENGINE", $"Procedural sector generated with seed {seed}. Saved to SQLite.", isRadio: false);
    }

    private void UpdateDatabaseStats()
    {
        int count = _repoManager.GetTotalRecordsCount();
        DatabaseStatusText = $"DB: ONLINE (SQLite • 9 Tables • {count} Records)";
    }

    private void OnSimulationStateUpdated()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            SimTimeText = _engine.SimTime.ToString("HH:mm:ss") + " UTC";
            ActiveTargetsText = $"{_engine.ActiveAircraft.Count(a => a.IsActive)} AIRBORNE";
            WindText = $"{_engine.Weather.CurrentWindDirection:D3}° @ {(int)_engine.Weather.CurrentWindSpeed} KTS";

            Radar.SyncPositions();

            // Calculate flight progress for currently selected target (or first active)
            var activeTarget = Radar.SelectedTrack?.Aircraft ?? _engine.ActiveAircraft.FirstOrDefault();
            if (activeTarget != null)
            {
                UpdateFlightRouteBanner(activeTarget);
            }

            if (Radar.SelectedTrack != null)
            {
                Inspector.UpdateTelemetryDisplay(Radar.SelectedTrack.Aircraft);
            }
        });
    }
}
