using System.Windows;
using System.Windows.Input;
using ATCsim.Core.Entities;
using ATCsim.Core.Services;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Repositories;

namespace ATCsim.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly SimulationEngine _engine;
    private readonly RepositoryManager _repoManager;
    private readonly IWorldGenerator _worldGenerator;
    private int _terrainVersion = WorldSector.CurrentTerrainVersion;

    private string _seedInput = "84920417";
    private string _activeTargetsText = "0 AIRBORNE";
    private string _simTimeText = "12:44:15 UTC";
    private string _windText = "310° @ 14 KTS";
    private string _speedLabel = "1x";

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
    private string _flightDistanceFlown = "260 km flown (55%)";
    private string _flightDistanceRemaining = "210 km remaining | Total: 470 km";

    private GridLength _flownGridLength = new(55, GridUnitType.Star);
    private GridLength _remainingGridLength = new(45, GridUnitType.Star);

    public RadarViewModel Radar { get; }
    public FlightInspectorVM Inspector { get; }
    public LogsViewModel Logs { get; }
    public bool HasSelectedTrack => Radar.SelectedTrack != null;
    public bool IsMriyaSelected => Radar.SelectedTrack?.IsMriya == true;
    public System.Windows.Media.Geometry FlightIconGeometry =>
        Radar.SelectedTrack?.IconGeometry ?? Resources.AviationIcons.ForAircraft(null);

    public string SeedInput
    {
        get => _seedInput;
        set => SetProperty(ref _seedInput, value);
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
        private set => SetProperty(ref _speedLabel, value);
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
    public ICommand RandomSeedCommand { get; }
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

        Radar = new RadarViewModel();
        Logs = new LogsViewModel();
        Inspector = new FlightInspectorVM(_engine);

        Radar.TrackSelected += aircraft => _engine.ReadState(() =>
        {
            Inspector.SetSelectedAircraft(aircraft);
            OnPropertyChanged(nameof(HasSelectedTrack));
            OnPropertyChanged(nameof(IsMriyaSelected));
            if (aircraft != null)
            {
                Inspector.ActiveTab = "AIRCRAFT";
                UpdateFlightRouteBanner(aircraft);
            }
        });

        Radar.AirportSelected += airport =>
        {
            Inspector.SelectedAirport = airport == null
                ? null
                : Inspector.AvailableAirports.FirstOrDefault(a => a.Icao == airport.IcaoCode);
            if (airport != null) Inspector.ActiveTab = "AIRPORT";
        };

        Inspector.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FlightInspectorVM.SelectedAirport)
                && Inspector.SelectedAirport?.Icao != Radar.SelectedAirport?.IcaoCode)
            {
                var marker = Radar.Airports.FirstOrDefault(a => a.IcaoCode == Inspector.SelectedAirport?.Icao);
                if (marker != null) Radar.SelectAirportCommand.Execute(marker);
                else Radar.SelectedAirport = null;
            }
        };

        RunCommand = new RelayCommand(() =>
        {
            _engine.Start();
            _repoManager.Logs.AddSimLog(new SimLog
            {
                WorldSeed = _engine.CurrentSeed,
                EventType = "SIM_START",
                Details = "Simulation loop started by operator.",
                SimTime = _engine.SimTime
            });
        });

        PauseCommand = new RelayCommand(() =>
        {
            _engine.Pause();
            _repoManager.Logs.AddSimLog(new SimLog
            {
                WorldSeed = _engine.CurrentSeed,
                EventType = "SIM_PAUSE",
                Details = "Simulation loop paused by operator.",
                SimTime = _engine.SimTime
            });
        });

        ToggleSpeedCommand = new RelayCommand(() => SetPlaybackSpeed(_engine.TimeScale switch
        {
            < 2 => 2,
            < 4 => 4,
            < 8 => 8,
            < SimulationEngine.MaxTimeScale => SimulationEngine.MaxTimeScale,
            _ => 1
        }));

        GenerateWorldCommand = new RelayCommand(GenerateFromSeed);
        RandomSeedCommand = new RelayCommand(() =>
        {
            SeedInput = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(sizeof(long))).ToString();
            GenerateFromSeed();
        });

        AddAircraftCommand = new RelayCommand(ExecuteAddAircraft);
        AddZoneCommand = new RelayCommand(ExecuteAddZone);
        AddAirportCommand = new RelayCommand(ExecuteAddAirport);
        EditAirportCommand = new RelayCommand(ExecuteEditAirport);
        ExitAndSaveCommand = new RelayCommand(ExecuteExitAndSave);

        // Wire simulation engine events
        _engine.StateUpdated += OnSimulationStateUpdated;
        _engine.ConflictDetected += alert => _pendingAlerts.Enqueue(new SimLog
        {
            WorldSeed = _engine.CurrentSeed,
            SimTime = _engine.SimTime,
            EventType = "COLLISION_WARNING",
            AircraftId = alert.Aircraft1.Id,
            Details = $"{alert.Aircraft1.Callsign} and {alert.Aircraft2.Callsign}: {alert.HorizontalDistanceNm} NM / {alert.VerticalDistanceFt} ft."
        });
        _engine.Radio.MessageLogged += log =>
        {
            _pendingRadio.Enqueue(log);
            OnSimulationStateUpdated();
        };
        ImportSaveCommand = new RelayCommand(() => TransferFile(import: true, preset: false));
        ExportSaveCommand = new RelayCommand(() => TransferFile(import: false, preset: false));
        ImportPresetCommand = new RelayCommand(() => TransferFile(import: true, preset: true));
        ExportPresetCommand = new RelayCommand(() => TransferFile(import: false, preset: true));

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
            if (_engine.ActiveAircraft.Any(a => string.Equals(a.Callsign, plane.Callsign, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("An aircraft with this callsign is already on the radar.");
                return;
            }
            _repoManager.ImportSector(new WorldSector { Seed = _engine.CurrentSeed, Aircraft = [plane] });
            _engine.AddAircraft(plane);

            var trackVm = new AircraftTrackViewModel(plane);
            Radar.Tracks.Add(trackVm);
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
            newZone.Id = _engine.WeatherZones.Select(zone => zone.Id).DefaultIfEmpty().Max() + 1;
            _repoManager.SyncSectorToDatabase(new WorldSector { Seed = _engine.CurrentSeed, WeatherZones = [newZone] });
            _engine.AddWeatherZone(newZone);
            Radar.WeatherZones.Add(new WeatherZoneViewModel(newZone));
            Logs.AddLog("• METEO", "RADAR CENTER", $"New weather restriction {newZone.Name} ({newZone.ZoneType}) activated.", isRadio: false);
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
            if (_engine.Airports.Any(a => string.Equals(a.IcaoCode, apt.IcaoCode, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("An airport with this ICAO code is already on the map.");
                return;
            }
            _repoManager.ImportSector(new WorldSector { Airports = [apt] });
            _engine.AddAirport(apt);
            Radar.RefreshAirportsList(_engine.Airports);
            Inspector.LoadAirports(_engine.Airports);
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
            _repoManager.Airports.UpdateAirport(dlg.EditedAirport);
            _engine.UpdateAirport(dlg.EditedAirport);
            Radar.RefreshAirportsList(_engine.Airports);
            Inspector.LoadAirports(_engine.Airports);
        }
    }

    private void ExecuteExitAndSave()
    {
        _engine.Pause();
        FlushLogs();
        _repoManager.SaveSessionProgress(
            _engine.CurrentSeed,
            _engine.SimTime,
            _engine.TimeScale,
            _engine.Airports,
            _engine.ActiveAircraft,
            _engine.WeatherZones,
            _terrainVersion);

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
        OnPropertyChanged(nameof(FlightIconGeometry));
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

        double pct = Math.Clamp((flownDist / totalDist) * 100.0, 0.0, 100.0);
        FlownGridLength = new GridLength(pct, GridUnitType.Star);
        RemainingGridLength = new GridLength(100.0 - pct, GridUnitType.Star);

        int flownKm = (int)(flownDist);
        int remKm = (int)(remDist);
        int totalKm = (int)(totalDist);
        FlightDistanceFlown = $"{flownKm} km flown ({(int)pct}%)";
        FlightDistanceRemaining = $"{remKm} km remaining | Total: {totalKm} km";
    }

    private void SetPlaybackSpeed(double scale)
    {
        _engine.SetTimeScale(scale);
        SpeedLabel = $"{_engine.TimeScale:0.##}x";
    }

    private void LoadInitialBaseline()
    {
        var saved = _repoManager.LoadSavedSession();
        if (saved.HasValue)
        {
            var (seed, simTime, timeScale, aircraftList, terrainVersion, weatherZones, savedAirports) = saved.Value;
            SeedInput = seed.ToString();
            _terrainVersion = terrainVersion;
            var airports = savedAirports ?? _repoManager.Airports.GetAll();

            var savedSector = _worldGenerator.GenerateSector(seed);
            if (_terrainVersion == WorldSector.LegacyTerrainVersion)
            {
                _worldGenerator.RestoreLegacyTerrain(savedSector);
            }
            savedSector.Aircraft = aircraftList;
            if (savedAirports != null || airports.Count > 0) savedSector.Airports = airports;
            if (weatherZones != null) savedSector.WeatherZones = weatherZones;

            _engine.LoadSector(savedSector, simTime);
            SetPlaybackSpeed(timeScale);
            RefreshRadarSector(savedSector);

            Logs.AddLog("• RESTORE", "STORAGE ENGINE", $"Попередню збережену сесію (Seed: {seed}) завантажено з бази даних SQLite.", isRadio: false);
            return;
        }

        long defaultSeed = 84920417;
        if (long.TryParse(SeedInput, out long s)) defaultSeed = s;

        // Generate baseline sector
        var sector = _worldGenerator.GenerateSector(defaultSeed);
        _repoManager.ImportSector(sector);
        _engine.LoadSector(sector);
        RefreshRadarSector(sector);

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
            MessageBox.Show("Введіть ціле число від -9223372036854775808 до 9223372036854775807.",
                "Некоректний seed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _engine.Pause();

        var sector = _worldGenerator.GenerateSector(seed);
        _repoManager.ImportSector(sector);
        _engine.LoadSector(sector);
        _terrainVersion = WorldSector.CurrentTerrainVersion;
        RefreshRadarSector(sector);

        _repoManager.SaveSessionProgress(seed, _engine.SimTime, _engine.TimeScale,
            sector.Airports, sector.Aircraft, sector.WeatherZones, _terrainVersion);

        Logs.AddLog("• WORLD GEN", "SEED ENGINE", $"Procedural sector generated with seed {seed}. Saved to SQLite.", isRadio: false);
    }

    private void RefreshRadarSector(WorldSector sector)
    {
        Radar.RefreshSector(sector);
        Inspector.LoadAirports(_engine.Airports);
        UpdateAirborneCount();
    }

    private void UpdateAirborneCount() =>
        ActiveTargetsText = $"{_engine.ActiveAircraft.Count(a => a.IsAirborne)} AIRBORNE";

    private readonly System.Collections.Concurrent.ConcurrentQueue<CommunicationLog> _pendingRadio = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<SimLog> _pendingAlerts = new();
    private int _refreshQueued;

    private void OnSimulationStateUpdated()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        dispatcher.InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            _engine.ReadState(() =>
            {
                SimTimeText = _engine.SimTime.ToString("HH:mm:ss") + " UTC";
                UpdateAirborneCount();
                WindText = $"{_engine.Weather.CurrentWindDirection:D3}° @ {(int)_engine.Weather.CurrentWindSpeed} KTS";
                Radar.SyncPositions(_engine.ConflictingAircraft);
                Inspector.RefreshAirportTraffic();
                if (Radar.SelectedTrack is { Aircraft: var aircraft })
                {
                    UpdateFlightRouteBanner(aircraft);
                    Inspector.UpdateTelemetryDisplay(aircraft);
                }
            });
            FlushLogs();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void FlushLogs()
    {
        var radio = new List<CommunicationLog>();
        var alerts = new List<SimLog>();
        while (_pendingRadio.TryDequeue(out var log))
        {
            Logs.AddLog(log);
            if (log.AirportId > 0 && log.SenderType is "ATC" or "AIRCRAFT") radio.Add(log);
        }
        while (_pendingAlerts.TryDequeue(out var alert))
        {
            Logs.AddLog("• LOSS OF SEPARATION", "SAFETY ALERT", alert.Details, isAlert: true, time: alert.SimTime);
            alerts.Add(alert);
        }
        if (radio.Count + alerts.Count == 0) return;
        _repoManager.Logs.AddBatch(radio, alerts);
    }
}
