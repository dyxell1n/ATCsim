using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.UI.Resources;

namespace ATCsim.UI.ViewModels;

internal sealed class AirportDiagram
{
    private static readonly Point Center = new(112, 72);
    private static readonly Point TaxiJoin = new(214, 72);
    private static readonly Rect Apron = new(214, 26, 96, 94);
    private static readonly Brush Background = ColorBrush(0xF1F3F5);
    private static readonly Brush Pavement = ColorBrush(0xDEE2E6);
    private static readonly Brush RunwaySurface = ColorBrush(0x495057);
    private static readonly Brush ClosedSurface = ColorBrush(0xADB5BD);
    private static readonly Brush Ink = ColorBrush(0x212529);
    private static readonly Brush Parked = ColorBrush(0x495057);
    private static readonly Brush Fueling = ColorBrush(0x9C6500);
    private static readonly Brush Moving = ColorBrush(0x2B8A3E);
    private static readonly Brush Guide = ColorBrush(0x868E96);
    private static readonly Pen Taxiway = MakePen(Pavement, 9);
    private static readonly Pen ParkOutline = MakePen(ClosedSurface, 0.75);
    private static readonly Pen Centerline = MakePen(Brushes.White, 0.9);
    private static readonly Typeface Typeface = new("Segoe UI");

    public DrawingGroup StaticDrawing { get; } = new();
    public DrawingGroup TrafficDrawing { get; } = new();
    public int ParkedCount { get; private set; }
    public int FuelingCount { get; private set; }
    public int MovingCount { get; private set; }
    public string MovingCallsigns { get; private set; } = string.Empty;

    private Dictionary<string, (Point Center, double HalfLength)> _runwayPixels = new(StringComparer.Ordinal);

    public void DrawLayout(Airport? airport)
    {
        using var dc = StaticDrawing.Open();
        _runwayPixels = new(StringComparer.Ordinal);
        if (airport == null) return;
        dc.DrawRectangle(Background, null, new Rect(0, 0, 320, 144));
        dc.DrawRectangle(Pavement, ParkOutline, Apron);
        for (int i = 0; i < Math.Min(airport.MaxCapacity, 24); i++)
            dc.DrawRectangle(null, ParkOutline, new Rect(Stand(i) - new Vector(6, 5), new Size(12, 10)));

        Text(dc, "APRON", new(Apron.Left + 4, 9), 9, Guide);
        Text(dc, "N ↑", new(8, 8), 10, Ink);
        var groups = airport.Runways.GroupBy(RunwayLayout.Strip).ToArray();
        double longest = Math.Max(1, (double)airport.Runways.Select(r => r.Length).DefaultIfEmpty().Max());
        var strips = groups.Select(group =>
        {
            double length = Math.Clamp((double)group.Max(r => r.Length) / longest * 48, 19, 48);
            Point center = Along(Center, group.Key.Axis + 90, group.Key.Lane * 14);
            return (Runways: group, Axis: group.Key.Axis, Center: center, HalfLength: length);
        }).ToArray();
        foreach (var strip in strips)
            foreach (var runway in strip.Runways)
                dc.DrawLine(Taxiway, Along(strip.Center, runway.Angle, strip.HalfLength * 0.58), TaxiJoin);

        foreach (var strip in strips)
        {
            var start = Along(strip.Center, strip.Axis + 180, strip.HalfLength);
            var end = Along(strip.Center, strip.Axis, strip.HalfLength);
            var pen = MakePen(strip.Runways.Any(runway => runway.IsAvailable) ? RunwaySurface : ClosedSurface, 12);
            dc.DrawLine(pen, start, end);
            for (int i = -4; i <= 4; i++)
            {
                var dash = Along(strip.Center, strip.Axis, i * strip.HalfLength / 5);
                dc.DrawLine(Centerline, Along(dash, strip.Axis + 180, 1.7), Along(dash, strip.Axis, 1.7));
            }

            foreach (var runway in strip.Runways)
            {
                _runwayPixels[runway.Designator] = (strip.Center, strip.HalfLength);
                var label = Along(strip.Center, runway.Angle + 180, strip.HalfLength + 11);
                Text(dc, runway.Designator, label, 9, runway.IsAvailable ? Ink : Guide, centered: true);
            }
        }
        if (groups.Length == 0) Text(dc, "No runways", new(80, 54), 11, Ink);

    }

    public void DrawTraffic(Airport? airport, IReadOnlyList<Aircraft> aircraft, DateTime time)
    {
        using var dc = TrafficDrawing.Open();
        ParkedCount = MovingCount = FuelingCount = 0;
        MovingCallsigns = string.Empty;
        if (airport == null) return;

        var moving = new List<string>();
        int standIndex = 0;
        int standCapacity = Math.Clamp(airport.MaxCapacity, 0, 24);
        foreach (var plane in aircraft)
        {
            var ops = plane.Operations;
            bool arrived = ops.Phase == FlightPhase.Landed && plane.ArrivalAirportIcao == airport.IcaoCode;
            bool scheduled = plane.IsActive && plane.PlannedDepartureSpeed.HasValue &&
                ops.Phase != FlightPhase.Departing && plane.DepartureAirportIcao == airport.IcaoCode;
            bool departing = ops.Phase == FlightPhase.Departing && plane.DepartureAirportIcao == airport.IcaoCode;
            if (!arrived && !scheduled && !departing) continue;

            int slot = standIndex;
            var stand = Stand(Math.Min(slot, 23));
            if (!departing) standIndex++;
            double elapsed = ops.LandedAt is { } landing ? (time - landing).TotalSeconds : double.PositiveInfinity;
            bool taxiing = arrived && elapsed >= 0 && elapsed < FlightOperationsState.TaxiInSeconds;
            if (!departing && !taxiing)
            {
                ParkedCount++;
                bool refueling = ops.Phase == FlightPhase.Refueling;
                if (refueling) FuelingCount++;
                if (slot < standCapacity) Plane(dc, plane, stand, 0, refueling ? Fueling : Parked, 10);
                continue;
            }

            MovingCount++;
            moving.Add(plane.Callsign);
            var (axis, runway) = _runwayPixels.GetValueOrDefault(ops.RunwayDesignator ?? string.Empty, (Center, 38));
            Point position;
            double heading;
            if (departing)
            {
                var assigned = airport.Runways.Find(candidate => candidate.Designator == ops.RunwayDesignator);
                var origin = assigned == null ? new Vector2D((double)airport.CoordX, (double)airport.CoordY)
                    : RunwayLayout.Center(airport, assigned);
                double distance = origin.DistanceTo(new((double)plane.CoordX, (double)plane.CoordY));
                position = Along(axis, ops.RunwayHeading, (Math.Clamp(distance / 3, 0, 1) * 2 - 1) * runway * 0.85);
                heading = ops.RunwayHeading;
            }
            else
            {
                var exit = Along(axis, ops.RunwayHeading, runway * 0.58);
                var destination = slot < standCapacity ? stand : new Point(Apron.Right - 5, Apron.Bottom - 5);
                double progress = elapsed / FlightOperationsState.TaxiInSeconds;
                (position, heading) = progress switch
                {
                    < 0.35 => Travel(Along(axis, ops.RunwayHeading + 180, runway * 0.85), exit, progress / 0.35),
                    < 0.78 => Travel(exit, TaxiJoin, (progress - 0.35) / 0.43),
                    _ => Travel(TaxiJoin, destination, (progress - 0.78) / 0.22)
                };
            }

            Plane(dc, plane, position, heading, Moving, 14);
        }

        int overflow = Math.Max(0, standIndex - standCapacity);
        if (overflow > 0)
            Text(dc, $"+{overflow}", new(Apron.Left + 5, Apron.Bottom + 6), 9, Ink);
        MovingCallsigns = string.Join(", ", moving.Take(4));
    }

    private static Point Stand(int index) => new(Apron.Left + 10 + index % 6 * 15, Apron.Top + 13 + index / 6 * 21);

    private static Point Along(Point origin, double heading, double distance)
    {
        double radians = heading * Math.PI / 180;
        return origin + new Vector(Math.Sin(radians) * distance, -Math.Cos(radians) * distance);
    }

    private static (Point, double) Travel(Point start, Point end, double progress) =>
        (start + (end - start) * progress, KinematicsCalculator.CalculateBearing(start.X, start.Y, end.X, end.Y));

    private static void Plane(DrawingContext dc, Aircraft aircraft, Point position, double heading, Brush fill, double size)
    {
        var geometry = AviationIcons.ForAircraft(aircraft.Model?.IconType);
        var bounds = geometry.Bounds;
        double scale = size / Math.Max(bounds.Width, bounds.Height);
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2));
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new RotateTransform(heading));
        transform.Children.Add(new TranslateTransform(position.X, position.Y));
        dc.PushTransform(transform);
        dc.DrawGeometry(fill, null, geometry);
        dc.Pop();
    }

    private static void Text(DrawingContext dc, string value, Point position, double size, Brush brush, bool centered = false)
    {
        var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, size, brush, 1);
        dc.DrawText(text, centered ? position - new Vector(text.Width / 2, text.Height / 2) : position);
    }

    private static Brush ColorBrush(int rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(Brush brush, double width)
    {
        var pen = new Pen(brush, width);
        pen.Freeze();
        return pen;
    }
}
