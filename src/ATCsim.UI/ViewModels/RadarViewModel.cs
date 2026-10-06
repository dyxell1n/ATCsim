using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using ATCsim.Core.Entities;
using ATCsim.Core.Services;
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

    public Aircraft Aircraft { get; }
    public string Callsign => Aircraft.Callsign;
    public string ModelName => Aircraft.Model?.ModelName ?? "B738";
    public string ShortModel => Aircraft.Model?.IconType ?? "B738";

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
        set => SetProperty(ref _heading, value);
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

    public string FlightLevelText => $"FL{(int)(Altitude / 100)}";

    // Light forward direction vector endpoint relative to center (14, 14)
    public double VectorEndX => 14.0 + 35.0 * Math.Sin(Heading * Math.PI / 180.0);
    public double VectorEndY => 14.0 - 35.0 * Math.Cos(Heading * Math.PI / 180.0);

    public AircraftTrackViewModel(Aircraft aircraft)
    {
        Aircraft = aircraft;
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
        X = (double)Aircraft.CoordX;
        Y = (double)Aircraft.CoordY;
        Heading = Aircraft.Heading;
        Altitude = Aircraft.Altitude;
        GroundSpeed = Aircraft.GroundSpeed;
        OnPropertyChanged(nameof(VectorEndX));
        OnPropertyChanged(nameof(VectorEndY));
    }
}

public class TrailSegmentViewModel
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public Brush StrokeBrush { get; init; } = Brushes.Orange;
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

public class RadarViewModel : ViewModelBase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Brush> BrushCache = new();

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

    public static Brush GetAltitudeBrush(decimal alt1, decimal alt2)
    {
        Color c1 = GetAltitudeColor(alt1);
        Color c2 = GetAltitudeColor(alt2);

        int key = (c1.R << 24) | (c1.G << 16) | (c2.R << 8) | c2.B;
        if (BrushCache.TryGetValue(key, out var cached))
            return cached;

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

        BrushCache.TryAdd(key, brush);
        return brush;
    }

    private readonly SimulationEngine _engine;
    private AircraftTrackViewModel? _selectedTrack;
    private AirportMarkerViewModel? _selectedAirport;

    public ObservableCollection<AircraftTrackViewModel> Tracks { get; } = [];
    public ObservableCollection<AirportMarkerViewModel> Airports { get; } = [];
    public ObservableCollection<Waypoint> Waypoints { get; } = [];
    public ObservableCollection<TrailSegmentViewModel> AllTrailSegments { get; } = [];

    public AircraftTrackViewModel? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
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

    public RadarViewModel(SimulationEngine engine)
    {
        _engine = engine;
        SelectTrackCommand = new RelayCommand(p =>
        {
            if (p is AircraftTrackViewModel track)
            {
                SelectedTrack = track;
            }
        });

        SelectAirportCommand = new RelayCommand(p =>
        {
            if (p is AirportMarkerViewModel marker)
            {
                SelectedAirport = marker;
            }
            else if (p is Airport airport)
            {
                var markerItem = Airports.FirstOrDefault(a => a.Airport.Id == airport.Id || a.IcaoCode == airport.IcaoCode);
                if (markerItem != null) SelectedAirport = markerItem;
            }
        });
    }

    public void RefreshSector(WorldSector sector)
    {
        Tracks.Clear();
        Airports.Clear();
        Waypoints.Clear();
        AllTrailSegments.Clear();

        foreach (var apt in sector.Airports)
        {
            string sub = apt.IcaoCode switch
            {
                "UKLL" => "Elevation: 1069ft | Capacity: 8/15",
                "UKBB" => "Elevation: 427ft | RWY 36R Available",
                "UKKK" => "Elevation: 587ft | RWY 08 Available",
                "UKOO" => "Elevation: 172ft | RWY 16 Available",
                "UKHH" => "Elevation: 508ft | RWY 07 Available",
                _ => $"Capacity: {apt.MaxCapacity}"
            };

            Airports.Add(new AirportMarkerViewModel
            {
                Airport = apt,
                Subtitle = sub
            });
        }

        foreach (var wp in sector.Waypoints)
        {
            Waypoints.Add(wp);
        }

        foreach (var ac in sector.Aircraft)
        {
            var tvm = new AircraftTrackViewModel(ac);
            Tracks.Add(tvm);
        }

        SelectedTrack = Tracks.FirstOrDefault();
        SelectedAirport = Airports.FirstOrDefault();
        UpdateTrailSegments();
    }

    public void RefreshAirportsList(IReadOnlyList<Airport> airports)
    {
        Airports.Clear();
        foreach (var apt in airports)
        {
            Airports.Add(new AirportMarkerViewModel
            {
                Airport = apt,
                Subtitle = $"Elevation: 500ft | Capacity: {apt.MaxCapacity}"
            });
        }
    }

    public void SyncPositions()
    {
        foreach (var track in Tracks)
        {
            track.UpdateFromModel();
        }
        UpdateTrailSegments();
    }

    private void UpdateTrailSegments()
    {
        AllTrailSegments.Clear();
        foreach (var track in Tracks)
        {
            var pts = track.Aircraft.TrailPoints;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p1 = pts[i];
                var p2 = pts[i + 1];

                // Filter out any corrupt or default (0, 0) top-left coordinates
                if (p1.X < 5.0 && p1.Y < 5.0) continue;
                if (p2.X < 5.0 && p2.Y < 5.0) continue;

                AllTrailSegments.Add(new TrailSegmentViewModel
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    StrokeBrush = GetAltitudeBrush(p1.Altitude, p2.Altitude)
                });
            }

            if (pts.Count > 0)
            {
                var lastPt = pts[^1];
                if (lastPt.X > 5.0 && lastPt.Y > 5.0 && track.X > 5.0 && track.Y > 5.0)
                {
                    if (Math.Abs(lastPt.X - track.X) > 0.5 || Math.Abs(lastPt.Y - track.Y) > 0.5)
                    {
                        AllTrailSegments.Add(new TrailSegmentViewModel
                        {
                            X1 = lastPt.X,
                            Y1 = lastPt.Y,
                            X2 = track.X,
                            Y2 = track.Y,
                            StrokeBrush = GetAltitudeBrush(lastPt.Altitude, track.Altitude)
                        });
                    }
                }
            }
        }
    }

    public void MarkConflict(string callsign1, string callsign2)
    {
        foreach (var track in Tracks)
        {
            if (track.Callsign == callsign1 || track.Callsign == callsign2)
            {
                track.HasConflict = true;
            }
        }
    }
}
