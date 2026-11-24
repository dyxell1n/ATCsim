using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Services;

namespace ATCsim.UI.ViewModels;

public class AirportDisplayModel
{
    public required Airport Airport { get; init; }
    public string Icao { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RunwaysText { get; init; } = string.Empty;
    public string CapacityText { get; init; } = string.Empty;
    public string CoordinatesText { get; init; } = string.Empty;
    public string ElevationText { get; init; } = string.Empty;
    public string Status { get; init; } = "OPEN";
}

public class FlightInspectorVM : ViewModelBase
{
    private readonly SimulationEngine _engine;
    private readonly AirportDiagram _airportDiagram = new();
    private string _groundTrafficSummary = string.Empty;

    public Drawing AirportLayout => _airportDiagram.StaticDrawing;
    public Drawing AirportTraffic => _airportDiagram.TrafficDrawing;
    public string GroundTrafficSummary
    {
        get => _groundTrafficSummary;
        private set => SetProperty(ref _groundTrafficSummary, value);
    }

    public void RefreshAirportTraffic()
    {
        if (!IsAirportTab || SelectedAirport == null) return;
        _airportDiagram.DrawTraffic(SelectedAirport.Airport, _engine.ActiveAircraft, _engine.SimTime);
        string callsigns = _airportDiagram.MovingCallsigns;
        GroundTrafficSummary = $"{_airportDiagram.ParkedCount} parked • {_airportDiagram.MovingCount} moving" +
            (callsigns.Length > 0 ? $" ({callsigns})" : string.Empty) +
            (_airportDiagram.FuelingCount > 0 ? $" • {_airportDiagram.FuelingCount} refueling" : string.Empty);
    }

    private Aircraft? _selectedAircraft;
    private AirportDisplayModel? _selectedAirport;
    private string _activeTab = "AIRCRAFT";

    // Aircraft properties
    private string _callsign = "UKR102";
    private string _modelName = "Boeing 737-800";
    private string _operatorName = "Ukraine Int.";
    private string _status = "ACTIVE";
    private string _altitudeFormatted = "28,000 ft";
    private string _flFormatted = "FL280";
    private string _groundSpeedFormatted = "420 kts";
    private string _kmhFormatted = "778 km/h";
    private string _headingFormatted = "066°";
    private string _headingDirection = "East-North-East";
    private string _fuelFormatted = "4,850 kg";
    private string _fuelPercent = "78% (3h 40m)";

    public ObservableCollection<AirportDisplayModel> AvailableAirports { get; } = [];

    public bool IsAircraftTab => ActiveTab == "AIRCRAFT";
    public bool IsAirportTab => ActiveTab == "AIRPORT";

    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(IsAircraftTab));
                OnPropertyChanged(nameof(IsAirportTab));
                if (IsAirportTab) _engine.ReadState(RefreshAirportTraffic);
            }
        }
    }

    public Aircraft? SelectedAircraft => _selectedAircraft;
    public bool IsMriya => Resources.AviationIcons.IsMriya(_selectedAircraft?.Model?.IconType);

    public AirportDisplayModel? SelectedAirport
    {
        get => _selectedAirport;
        set
        {
            if (!SetProperty(ref _selectedAirport, value)) return;
            _airportDiagram.DrawLayout(value?.Airport);
            _engine.ReadState(RefreshAirportTraffic);
        }
    }

    public string Callsign
    {
        get => _callsign;
        set => SetProperty(ref _callsign, value);
    }

    public string ModelName
    {
        get => _modelName;
        set => SetProperty(ref _modelName, value);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string AltitudeFormatted
    {
        get => _altitudeFormatted;
        set => SetProperty(ref _altitudeFormatted, value);
    }

    public string FlFormatted
    {
        get => _flFormatted;
        set => SetProperty(ref _flFormatted, value);
    }

    public string GroundSpeedFormatted
    {
        get => _groundSpeedFormatted;
        set => SetProperty(ref _groundSpeedFormatted, value);
    }

    public string KmhFormatted
    {
        get => _kmhFormatted;
        set => SetProperty(ref _kmhFormatted, value);
    }

    public string HeadingFormatted
    {
        get => _headingFormatted;
        set => SetProperty(ref _headingFormatted, value);
    }

    public string HeadingDirection
    {
        get => _headingDirection;
        set => SetProperty(ref _headingDirection, value);
    }

    public string FuelFormatted
    {
        get => _fuelFormatted;
        set => SetProperty(ref _fuelFormatted, value);
    }

    public string FuelPercent
    {
        get => _fuelPercent;
        set => SetProperty(ref _fuelPercent, value);
    }

    public ICommand TurnHeadingCommand { get; }
    public ICommand ClimbDescendCommand { get; }
    public ICommand DirectToUkbbCommand { get; }
    public ICommand HoldingPatternCommand { get; }
    public ICommand SwitchToAircraftTabCommand { get; }
    public ICommand SwitchToAirportTabCommand { get; }

    public FlightInspectorVM(SimulationEngine engine)
    {
        _engine = engine;

        SwitchToAircraftTabCommand = new RelayCommand(() => ActiveTab = "AIRCRAFT");
        SwitchToAirportTabCommand = new RelayCommand(() => ActiveTab = "AIRPORT");

        TurnHeadingCommand = new RelayCommand(ExecuteTurnHeading);
        ClimbDescendCommand = new RelayCommand(ExecuteClimbDescend);
        DirectToUkbbCommand = new RelayCommand(ExecuteDirectToUkbb);
        HoldingPatternCommand = new RelayCommand(ExecuteHoldingPattern);
    }

    public void LoadAirports(IReadOnlyList<Airport> airports)
    {
        string? selectedIcao = SelectedAirport?.Icao;
        AvailableAirports.Clear();
        foreach (var apt in airports)
        {
            string rwys = apt.Runways.Count > 0
                ? string.Join(", ", apt.Runways.Select(r => $"RWY {r.Designator} ({r.Angle}°, {r.Length}m)"))
                : "No runways";

            string elev = apt.IcaoCode switch
            {
                "UKLL" => "1,069 ft (326 m)",
                "UKBB" => "427 ft (130 m)",
                "UKKK" => "587 ft (179 m)",
                "UKOO" => "172 ft (52 m)",
                "UKHH" => "508 ft (155 m)",
                _ => "500 ft (152 m)"
            };

            var adm = new AirportDisplayModel
            {
                Airport = apt,
                Icao = apt.IcaoCode,
                Name = apt.Name,
                RunwaysText = rwys,
                CapacityText = $"{apt.MaxCapacity} stands",
                CoordinatesText = $"X: {apt.CoordX}, Y: {apt.CoordY}",
                ElevationText = elev,
                Status = "OPERATIONAL"
            };
            AvailableAirports.Add(adm);
        }

        SelectedAirport = AvailableAirports.FirstOrDefault(airport => airport.Icao == selectedIcao);
    }

    public void SetSelectedAircraft(Aircraft? aircraft)
    {
        if (!SetProperty(ref _selectedAircraft, aircraft, nameof(SelectedAircraft))) return;
        OnPropertyChanged(nameof(IsMriya));
        if (aircraft == null) return;

        Callsign = aircraft.Callsign;
        ModelName = aircraft.Model?.ModelName ?? "Boeing 737-800";
        OperatorName = IsMriya ? "Antonov Airlines" :
                       aircraft.Callsign.StartsWith("UKR") ? "Ukraine Int." :
                       aircraft.Callsign.StartsWith("WZZ") ? "Wizz Air" :
                       aircraft.Callsign.StartsWith("LOT") ? "LOT Polish Airlines" : "Turkish Airlines";

        UpdateTelemetryDisplay(aircraft);
    }

    public void UpdateTelemetryDisplay(Aircraft aircraft)
    {
        Status = aircraft.Operations.Phase switch
        {
            FlightPhase.ArrivalQueue or FlightPhase.Holding => "HOLDING",
            FlightPhase.Departing => "DEPARTING",
            FlightPhase.Refueling => "REFUELING",
            FlightPhase.Approach => "APPROACH",
            FlightPhase.Final => "FINAL",
            FlightPhase.MissedApproach => "GOING AROUND",
            FlightPhase.Landed => "LANDED",
            _ => aircraft.IsAirborne ? aircraft.Operations.ArrivalDescentStarted && !aircraft.HasManualHeadingOverride
                ? "DESCENDING" : "EN ROUTE" : "AWAITING DEPARTURE"
        };
        if (_selectedAircraft != null && _selectedAircraft.Id != aircraft.Id)
            return;

        int alt = (int)aircraft.Altitude;
        AltitudeFormatted = $"{alt:N0} ft";
        FlFormatted = $"FL{alt / 100}";

        int spd = (int)aircraft.GroundSpeed;
        GroundSpeedFormatted = $"{spd} kts";
        KmhFormatted = $"{(int)(spd * 1.852)} km/h";

        int hdg = (int)Math.Round(aircraft.Heading);
        HeadingFormatted = $"{hdg:D3}°";
        HeadingDirection = GetCompassLabel(hdg);

        int fuel = (int)aircraft.FuelRemaining;
        FuelFormatted = $"{fuel:N0} kg";
        decimal capacity = AircraftPerformance.FuelCapacity(aircraft.Model);
        decimal percent = Math.Clamp(aircraft.FuelRemaining, 0m, capacity) / capacity * 100m;
        decimal hourlyBurn = AircraftPerformance.FuelBurnPerHour(aircraft.Model);
        int remainingMinutes = hourlyBurn > 0
            ? (int)Math.Min(int.MaxValue, (double)aircraft.FuelRemaining / (double)hourlyBurn * 60)
            : 0;
        FuelPercent = $"{percent:0}% ({remainingMinutes / 60}h {remainingMinutes % 60}m)";
    }

    private void ExecuteTurnHeading()
    {
        if (_selectedAircraft == null) return;
        _engine.VectorAircraft(_selectedAircraft, _selectedAircraft.Heading + 30);
    }

    private void ExecuteClimbDescend()
    {
        if (_selectedAircraft == null) return;
        decimal ceiling = _selectedAircraft.Model?.MaxAltitude > 0 ? _selectedAircraft.Model.MaxAltitude : 32000m;
        decimal upper = Math.Min(32000m, ceiling);
        decimal lower = Math.Min(18000m, Math.Floor(upper * 0.6m / 1000m) * 1000m);
        decimal newAlt = _selectedAircraft.Altitude >= (upper + lower) / 2m ? lower : upper;
        _engine.ChangeAircraftAltitude(_selectedAircraft, newAlt);
    }

    private void ExecuteDirectToUkbb()
    {
        if (_selectedAircraft == null) return;

        _engine.ResumeAircraftRoute(_selectedAircraft);
    }

    private void ExecuteHoldingPattern()
    {
        if (_selectedAircraft == null) return;
        _engine.HoldAircraft(_selectedAircraft);
    }

    private static string GetCompassLabel(int degrees) => degrees switch
    {
        >= 338 or < 23 => "North",
        >= 23 and < 68 => "North-East",
        >= 68 and < 113 => "East",
        >= 113 and < 158 => "South-East",
        >= 158 and < 203 => "South",
        >= 203 and < 248 => "South-West",
        >= 248 and < 293 => "West",
        _ => "North-West"
    };
}

