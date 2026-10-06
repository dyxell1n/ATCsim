using System.Collections.ObjectModel;
using System.Windows.Input;
using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Services;

namespace ATCsim.UI.ViewModels;

public class AirportDisplayModel
{
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
    private readonly Action<string, string> _onInstructionSent;

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
            }
        }
    }

    public Aircraft? SelectedAircraft => _selectedAircraft;

    public AirportDisplayModel? SelectedAirport
    {
        get => _selectedAirport;
        set => SetProperty(ref _selectedAirport, value);
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

    public FlightInspectorVM(SimulationEngine engine, Action<string, string> onInstructionSent)
    {
        _engine = engine;
        _onInstructionSent = onInstructionSent;

        SwitchToAircraftTabCommand = new RelayCommand(() => ActiveTab = "AIRCRAFT");
        SwitchToAirportTabCommand = new RelayCommand(() => ActiveTab = "AIRPORT");

        TurnHeadingCommand = new RelayCommand(ExecuteTurnHeading);
        ClimbDescendCommand = new RelayCommand(ExecuteClimbDescend);
        DirectToUkbbCommand = new RelayCommand(ExecuteDirectToUkbb);
        HoldingPatternCommand = new RelayCommand(ExecuteHoldingPattern);
    }

    public void LoadAirports(IReadOnlyList<Airport> airports)
    {
        AvailableAirports.Clear();
        foreach (var apt in airports)
        {
            string rwys = apt.Runways.Count > 0
                ? string.Join(", ", apt.Runways.Select(r => $"RWY {r.Designator} ({r.Angle}°, {r.Length}m)"))
                : "RWY 36R (360°, 4000m)";

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

        SelectedAirport = AvailableAirports.FirstOrDefault();
    }

    public void SetSelectedAircraft(Aircraft? aircraft)
    {
        _selectedAircraft = aircraft;
        if (aircraft == null) return;

        Callsign = aircraft.Callsign;
        ModelName = aircraft.Model?.ModelName ?? "Boeing 737-800";
        OperatorName = aircraft.Callsign.StartsWith("UKR") ? "Ukraine Int." :
                       aircraft.Callsign.StartsWith("WZZ") ? "Wizz Air" :
                       aircraft.Callsign.StartsWith("LOT") ? "LOT Polish Airlines" : "Turkish Airlines";
        Status = aircraft.IsActive ? "ACTIVE" : "INACTIVE";

        UpdateTelemetryDisplay(aircraft);
    }

    public void UpdateTelemetryDisplay(Aircraft aircraft)
    {
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
        FuelPercent = $"{Math.Clamp(fuel / 50, 10, 100)}% ({fuel / 1200}h {(fuel % 1200) / 20}m)";
    }

    private void ExecuteTurnHeading()
    {
        if (_selectedAircraft == null) return;
        double newHdg = (_selectedAircraft.Heading + 30) % 360;
        _selectedAircraft.TargetHeading = newHdg;

        string cmd = $"turn right heading {(int)newHdg:D3}";
        string readback = $"Right heading {(int)newHdg:D3}";
        _engine.Radio.DispatchAtcInstruction(_selectedAircraft, cmd);
        _engine.Radio.GeneratePilotReadback(_selectedAircraft, readback);
        _onInstructionSent(cmd, readback);
    }

    private void ExecuteClimbDescend()
    {
        if (_selectedAircraft == null) return;
        decimal newAlt = _selectedAircraft.Altitude >= 24000m ? 18000m : 32000m;
        _selectedAircraft.TargetAltitude = newAlt;

        string verb = newAlt < _selectedAircraft.Altitude ? "descend" : "climb";
        string cmd = $"{verb} FL{(int)(newAlt / 100)}";
        string readback = $"{char.ToUpper(verb[0])}{verb[1..]}ing FL{(int)(newAlt / 100)}";
        _engine.Radio.DispatchAtcInstruction(_selectedAircraft, cmd);
        _engine.Radio.GeneratePilotReadback(_selectedAircraft, readback);
        _onInstructionSent(cmd, readback);
    }

    private void ExecuteDirectToUkbb()
    {
        if (_selectedAircraft == null) return;

        // Calculate bearing towards UKBB (580, 260)
        double directBearing = KinematicsCalculator.CalculateBearing(
            (double)_selectedAircraft.CoordX, (double)_selectedAircraft.CoordY, 580.0, 260.0);

        _selectedAircraft.TargetHeading = directBearing;

        string cmd = $"proceed direct UKBB, heading {(int)directBearing:D3}";
        string readback = $"Direct UKBB, heading {(int)directBearing:D3}";
        _engine.Radio.DispatchAtcInstruction(_selectedAircraft, cmd);
        _engine.Radio.GeneratePilotReadback(_selectedAircraft, readback);
        _onInstructionSent(cmd, readback);
    }

    private void ExecuteHoldingPattern()
    {
        if (_selectedAircraft == null) return;
        _selectedAircraft.TargetHeading = (_selectedAircraft.Heading + 180) % 360;

        string cmd = "hold as published, expect further clearance";
        string readback = "Holding as published, wilco";
        _engine.Radio.DispatchAtcInstruction(_selectedAircraft, cmd);
        _engine.Radio.GeneratePilotReadback(_selectedAircraft, readback);
        _onInstructionSent(cmd, readback);
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
