using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.UI.ViewModels;

public class AircraftTrackViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private double _heading;
    private decimal _altitude;
    private decimal _groundSpeed;
    private bool _isSelected;
    private bool _hasConflict;
    private bool _isAirborne;

    public Aircraft Aircraft { get; }
    public string Callsign => Aircraft.Callsign;
    public string ModelName => Aircraft.Model?.ModelName ?? "B738";
    public string ShortModel => Aircraft.Model?.IconType ?? "B738";
    public bool IsMriya => Resources.AviationIcons.IsMriya(ShortModel);
    public string RadarModelLabel => IsMriya ? "Ан-225 «Мрія»" : ShortModel;
    public Geometry IconGeometry => Resources.AviationIcons.ForAircraft(ShortModel);

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public double Heading
    {
        get => _heading;
        set
        {
            if (SetProperty(ref _heading, value))
            {
                OnPropertyChanged(nameof(VectorEndX));
                OnPropertyChanged(nameof(VectorEndY));
            }
        }
    }

    public decimal Altitude
    {
        get => _altitude;
        set
        {
            if (SetProperty(ref _altitude, value))
            {
                OnPropertyChanged(nameof(FlightLevelText));
            }
        }
    }

    public decimal GroundSpeed
    {
        get => _groundSpeed;
        set => SetProperty(ref _groundSpeed, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasConflict
    {
        get => _hasConflict;
        set => SetProperty(ref _hasConflict, value);
    }

    public bool IsAirborne
    {
        get => _isAirborne;
        private set => SetProperty(ref _isAirborne, value);
    }

    public string FlightLevelText => $"FL{(int)(Altitude / 100)}";

    // Direction vector and 34px hit target share the same center.
    public double VectorEndX => 17.0 + 35.0 * Math.Sin(Heading * Math.PI / 180.0);
    public double VectorEndY => 17.0 - 35.0 * Math.Cos(Heading * Math.PI / 180.0);

    public AircraftTrackViewModel(Aircraft aircraft)
    {
        Aircraft = aircraft;
        UpdateFromModel();
    }

    private readonly DrawingGroup _trailHistory = new();
    private int _drawnPoints;
    private TrailPoint _firstPoint;

    internal void DrawTrail(DrawingContext frame)
    {
        var points = Aircraft.TrailPoints;
        if (points.Count == 0)
        {
            if (_drawnPoints > 0) _trailHistory.Children.Clear();
            _drawnPoints = 0;
            return;
        }
        if (!IsAirborne && Aircraft.Operations.LandedAt == null) return;
        // The tracker drops its oldest point when the history reaches capacity.
        if (points[0] != _firstPoint || points.Count < _drawnPoints)
        {
            _trailHistory.Children.Clear();
            _drawnPoints = 0;
        }
        if (points.Count != _drawnPoints)
        {
            using var history = _trailHistory.Append();
            for (int i = Math.Max(1, _drawnPoints); i < points.Count; i++)
            {
                var first = points[i - 1];
                var second = points[i];
                if ((first.X < 5 && first.Y < 5) || (second.X < 5 && second.Y < 5)) continue;
                history.DrawLine(RadarViewModel.TrailPen(first.Altitude, second.Altitude),
                    new(first.X, first.Y), new(second.X, second.Y));
            }
            _firstPoint = points[0];
            _drawnPoints = points.Count;
        }
        frame.DrawDrawing(_trailHistory);
        var last = points[^1];
        if (IsAirborne && last.X > 5 && last.Y > 5 && X > 5 && Y > 5 &&
            (Math.Abs(last.X - X) > 0.5 || Math.Abs(last.Y - Y) > 0.5))
            frame.DrawLine(RadarViewModel.TrailPen(last.Altitude, Altitude), new(last.X, last.Y), new(X, Y));
    }

    public void UpdateFromModel()
    {
        X = (double)Aircraft.CoordX;
        Y = (double)Aircraft.CoordY;
        Heading = Aircraft.Heading;
        Altitude = Aircraft.Altitude;
        GroundSpeed = Aircraft.GroundSpeed;
        IsAirborne = Aircraft.IsAirborne;
    }
}

public class AirportMarkerViewModel : ViewModelBase
{
    private bool _isSelected;
    public Airport Airport { get; init; } = null!;
    public string IcaoCode => Airport.IcaoCode;
    public string Name => Airport.Name;
    public double X => (double)Airport.CoordX;
    public double Y => (double)Airport.CoordY;
    public string Subtitle { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class TerrainPatchViewModel
{
    public static Brush WaterFill { get; } = CreateFill("#1E3A52");
    public PointCollection Points { get; }
    public Brush Fill { get; }
    public TerrainPatchViewModel(TerrainPatch patch)
    {
        Points = new PointCollection(patch.Points.Select(p => new System.Windows.Point(p.X, p.Y)));
        Points.Freeze();
        string color = patch.Kind switch
        {
            "LAND" => "#284E38",
            "SAND" => "#9E8758",
            "MOUNTAIN" => "#615C52",
            "MOUNTAIN_RIDGE" => "#888074",
            _ => "#37684B"
        };
        Fill = patch.Kind == "RIVER" ? WaterFill : CreateFill(color);
    }

    private static Brush CreateFill(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public class WeatherZoneViewModel(WeatherZone zone)
{
    public double X => (double)(zone.CenterX - zone.Radius);
    public double Y => (double)(zone.CenterY - zone.Radius);
    public double Diameter => (double)(zone.Radius * 2);
    public string Name => zone.Name;
    public string Description => $"{zone.ZoneType} (FL{zone.MinAltitude / 100:0} - FL{zone.MaxAltitude / 100:0})";
}
public class RadarViewModel : ViewModelBase
{
    private static readonly Dictionary<(Color, Color), Pen> Pens = [];

    public static Color GetAltitudeColor(decimal altitude)
    {
        double alt = (double)Math.Clamp(altitude, 4000m, 36000m);
        double t = (alt - 4000.0) / (36000.0 - 4000.0);

        // Continuous smooth interpolation:
        // t = 0.0 -> #FCC419 (252, 196, 25)
        // t = 0.5 -> #F76707 (247, 103, 7)
        // t = 1.0 -> #7048E8 (112, 72, 232)
        byte r, g, b;
        if (t <= 0.5)
        {
            double k = t / 0.5;
            r = (byte)Math.Round(252.0 + (247.0 - 252.0) * k);
            g = (byte)Math.Round(196.0 + (103.0 - 196.0) * k);
            b = (byte)Math.Round(25.0 + (7.0 - 25.0) * k);
        }
        else
        {
            double k = (t - 0.5) / 0.5;
            r = (byte)Math.Round(247.0 + (112.0 - 247.0) * k);
            g = (byte)Math.Round(103.0 + (72.0 - 103.0) * k);
            b = (byte)Math.Round(7.0 + (232.0 - 7.0) * k);
        }

        return Color.FromRgb(r, g, b);
    }

    internal static Pen TrailPen(decimal alt1, decimal alt2)
    {
        Color c1 = GetAltitudeColor(alt1);
        Color c2 = GetAltitudeColor(alt2);

        if (Pens.TryGetValue((c1, c2), out var cached)) return cached;

        Brush brush;
        if (c1 == c2)
        {
            var solid = new SolidColorBrush(c1);
            solid.Freeze();
            brush = solid;
        }
        else
        {
            var grad = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            grad.GradientStops.Add(new GradientStop(c1, 0.0));
            grad.GradientStops.Add(new GradientStop(c2, 1.0));
            grad.Freeze();
            brush = grad;
        }

        var pen = new Pen(brush, 3.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        Pens.Add((c1, c2), pen);
        return pen;
    }

    private bool _showAirportLabels = true;
    private AircraftTrackViewModel? _selectedTrack;
    private AirportMarkerViewModel? _selectedAirport;

    public ObservableCollection<AircraftTrackViewModel> Tracks { get; } = [];
    public ObservableCollection<AirportMarkerViewModel> Airports { get; } = [];
    public ObservableCollection<TerrainPatchViewModel> Terrain { get; } = [];
    public ObservableCollection<WeatherZoneViewModel> WeatherZones { get; } = [];
    public DrawingGroup TrailDrawing { get; } = new();

    public bool ShowAirportLabels
    {
        get => _showAirportLabels;
        set => SetProperty(ref _showAirportLabels, value);
    }

    public AircraftTrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (ReferenceEquals(_selectedTrack, value)) return;
            if (_selectedTrack != null) _selectedTrack.IsSelected = false;
            if (SetProperty(ref _selectedTrack, value))
            {
                if (_selectedTrack != null) _selectedTrack.IsSelected = true;
                TrackSelected?.Invoke(_selectedTrack?.Aircraft);
            }
        }
    }

    public AirportMarkerViewModel? SelectedAirport
    {
        get => _selectedAirport;
        set
        {
            if (ReferenceEquals(_selectedAirport, value)) return;
            if (_selectedAirport != null) _selectedAirport.IsSelected = false;
            if (SetProperty(ref _selectedAirport, value))
            {
                if (_selectedAirport != null) _selectedAirport.IsSelected = true;
                AirportSelected?.Invoke(_selectedAirport?.Airport);
            }
        }
    }

    public event Action<Aircraft?>? TrackSelected;
    public event Action<Airport?>? AirportSelected;
    public ICommand SelectTrackCommand { get; }
    public ICommand SelectAirportCommand { get; }
    public ICommand ClearSelectionCommand { get; }

    public RadarViewModel()
    {
        SelectTrackCommand = new RelayCommand(p =>
        {
            if (p is AircraftTrackViewModel { IsAirborne: true } track)
            {
                SelectedAirport = null;
                SelectedTrack = track;
            }
        });

        SelectAirportCommand = new RelayCommand(p =>
        {
            if (p is AirportMarkerViewModel marker)
            {
                SelectedTrack = null;
                SelectedAirport = marker;
            }
        });

        ClearSelectionCommand = new RelayCommand(() =>
        {
            SelectedTrack = null;
            SelectedAirport = null;
        });
    }

    public void RefreshSector(WorldSector sector)
    {
        Airports.Clear();
        Terrain.Clear();
        WeatherZones.Clear();
        foreach (var patch in sector.Terrain) Terrain.Add(new TerrainPatchViewModel(patch));
        foreach (var zone in sector.WeatherZones.Where(z => z.IsActive)) WeatherZones.Add(new WeatherZoneViewModel(zone));

        foreach (var apt in sector.Airports)
        {
            string sub = $"Capacity: {apt.MaxCapacity} | RWY {string.Join(", ", apt.Runways.Select(r => r.Designator))}";

            Airports.Add(new AirportMarkerViewModel
            {
                Airport = apt,
                Subtitle = sub
            });
        }

        SelectedAirport = null;
        RefreshAircraft(sector.Aircraft);
    }

    public void RefreshAircraft(IReadOnlyList<Aircraft> aircraft)
    {
        Tracks.Clear();
        foreach (var ac in aircraft)
        {
            var tvm = new AircraftTrackViewModel(ac);
            Tracks.Add(tvm);
        }

        SelectedTrack = SelectedAirport == null ? Tracks.FirstOrDefault(track => track.IsAirborne) : null;
        UpdateTrailDrawing();
    }

    public void RefreshAirportsList(IReadOnlyList<Airport> airports)
    {
        string? selectedIcao = SelectedAirport?.IcaoCode;
        Airports.Clear();
        foreach (var apt in airports)
        {
            Airports.Add(new AirportMarkerViewModel
            {
                Airport = apt,
                Subtitle = $"Elevation: 500ft | Capacity: {apt.MaxCapacity}"
            });
        }
        SelectedAirport = Airports.FirstOrDefault(airport => airport.IcaoCode == selectedIcao);
    }

    public void SyncPositions(IReadOnlySet<int>? conflicts = null)
    {
        foreach (var track in Tracks)
        {
            track.UpdateFromModel();
            track.HasConflict = conflicts?.Contains(track.Aircraft.Id) == true;
        }
        if (SelectedTrack is { IsAirborne: false }) SelectedTrack = null;
        UpdateTrailDrawing();
    }

    private void UpdateTrailDrawing()
    {
        using var frame = TrailDrawing.Open();
        foreach (var track in Tracks) track.DrawTrail(frame);
    }
}
