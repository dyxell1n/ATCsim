using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;

namespace ATCsim.Core.Services;

internal sealed class TrafficSeparation
{
    private const double LookAhead = 60;
    private const double CellSize = 20;
    private const double SeparationDistance = CollisionDetection.CollisionDetector.HorizontalSeparationMinNm;
    private const double SeparationAltitude = (double)CollisionDetection.CollisionDetector.VerticalSeparationMinFt;
    private const double PlanningDistance = SeparationDistance + 1;
    private const double PlanningAltitude = SeparationAltitude + 250;
    private const double EmergencyDistance = 0.5;
    private const double EmergencyAltitude = 250;
    private static readonly double[] Turns = [30, 60, 90, -30, -60, -90, 120, -120, 179];
    private readonly Dictionary<(int, int), List<Contact>> _cells = [];
    private readonly List<Contact> _nearby = [];
    private double _maxSpeed;
    private double _step;
    private Vector2D _wind;

    private readonly record struct Contact(Aircraft Aircraft, Vector2D Position, Vector2D Velocity,
        double Altitude, double VerticalSpeed);

    private void Capture(IReadOnlyList<Aircraft> traffic)
    {
        _cells.Clear();
        _maxSpeed = 0;
        foreach (var aircraft in traffic)
        {
            if (!aircraft.IsAirborne) continue;
            var position = AirspaceNavigator.Position(aircraft);
            var cell = Cell(position);
            if (!_cells.TryGetValue(cell, out var entries)) _cells.Add(cell, entries = []);
            double speed = (double)aircraft.GroundSpeed / 3600;
            _maxSpeed = Math.Max(_maxSpeed, speed);
            entries.Add(new(aircraft, position, Velocity(aircraft.Heading, speed), (double)aircraft.Altitude,
                Rate(aircraft.Altitude, AircraftTracker.AvailableAltitude(aircraft, aircraft.TargetAltitude))));
        }
    }

    public (double Heading, decimal Altitude)? Resolve(Aircraft aircraft, Vector2D position, double heading,
        decimal targetAltitude, decimal speed, bool onRunwayCourse, decimal? verticalRate, Func<double, decimal, bool> clearOfZones)
    {
        double speedNm = (double)speed / 3600;
        double range = PlanningDistance + (speedNm + _maxSpeed) * LookAhead;
        var (minX, minY) = Cell(new(position.X - range, position.Y - range));
        var (maxX, maxY) = Cell(new(position.X + range, position.Y + range));
        _nearby.Clear();
        double rangeSquared = range * range;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!_cells.TryGetValue((x, y), out var entries)) continue;
            foreach (var contact in entries)
            {
                if (ReferenceEquals(aircraft, contact.Aircraft)) continue;
                if (Math.Abs(contact.Altitude - (double)aircraft.Altitude) > PlanningAltitude + LookAhead * 50) continue;
                var offset = Subtract(contact.Position, position);
                if (Dot(offset, offset) <= rangeSquared) _nearby.Add(contact);
            }
        }
        if (_nearby.Count == 0) return (heading, targetAltitude);

        double baseline = Score(heading, targetAltitude);

        var best = (Heading: heading, Altitude: targetAltitude);
        double bestScore = baseline;
        // Leave a stabilized approach or takeoff course alone unless the conflict is already close.
        if (baseline < 1 && (!onRunwayCourse || baseline < 0.25))
        {
            double prefer = 1;
            if (_nearby.FirstOrDefault(contact => Threat(contact, heading, targetAltitude) < 1) is { Aircraft: { } other })
                prefer = aircraft.Altitude > other.Altitude ||
                    (aircraft.Altitude == other.Altitude && aircraft.Id > other.Id) ? 1 : -1;
            decimal ceiling = aircraft.Model is { MaxAltitude: > 0 } model ? model.MaxAltitude : 45000m;
            decimal avoidAltitude = Math.Min(ceiling, Math.Max(1000m, aircraft.Altitude + (decimal)prefer * 2000));
            if (!onRunwayCourse) Consider(heading, avoidAltitude);
            foreach (double turn in Turns)
            {
                Consider(heading + turn, targetAltitude);
                if (bestScore >= 1) break;
            }
            if (bestScore < 1)
                foreach (double turn in Turns)
                {
                    Consider(heading + turn, avoidAltitude);
                    if (bestScore >= 1) break;
                }
        }

        // Recheck against movements already committed this tick before applying the maneuver.
        var nextVelocity = Velocity(KinematicsCalculator.UpdateHeading(aircraft.Heading, best.Heading, _step), speedNm);
        var nextPosition = new Vector2D(Math.Round(position.X + (nextVelocity.X + _wind.X) * _step, 4),
            Math.Round(position.Y + (nextVelocity.Y + _wind.Y) * _step, 4));
        decimal nextAltitude = AircraftTracker.NextAltitude(aircraft, best.Altitude, _step, verticalRate);
        foreach (var contact in _nearby)
        {
            var other = contact.Aircraft;
            if (!other.IsAirborne) continue;
            var neighbor = AirspaceNavigator.Position(other);
            var start = Subtract(neighbor, position);
            var end = Subtract(neighbor, nextPosition);
            double startVertical = (double)(other.Altitude - aircraft.Altitude);
            double endVertical = (double)(other.Altitude - nextAltitude);
            var displacement = Subtract(end, start);
            double verticalChange = endVertical - startVertical;
            bool currentlySeparated = Dot(start, start) >= SeparationDistance * SeparationDistance ||
                Math.Abs(startVertical) >= SeparationAltitude;
            if (currentlySeparated && ClosestDistanceSquared(start, displacement, startVertical, verticalChange,
                1, SeparationAltitude) < SeparationDistance * SeparationDistance)
                return null;
            if (ClosestDistanceSquared(start, displacement, startVertical, verticalChange,
                1, EmergencyAltitude) < EmergencyDistance * EmergencyDistance && Dot(end, end) <= Dot(start, start) &&
                Math.Abs(endVertical) <= Math.Abs(startVertical))
                return null;
        }

        return best;

        void Consider(double course, decimal altitude)
        {
            if (bestScore >= 1) return;
            course = KinematicsCalculator.NormalizeCompass(course);
            double score = Score(course, altitude);
            if (score <= bestScore + 0.0001 || !clearOfZones(course, altitude)) return;
            best = (course, altitude);
            bestScore = score;
        }

        double Score(double course, decimal altitude)
        {
            double score = 1;
            foreach (var contact in _nearby)
                score = Math.Min(score, Threat(contact, course, altitude));
            return score;
        }

        double Threat(Contact contact, double course, decimal altitude)
        {
            var offset = Subtract(contact.Position, position);
            var velocity = Velocity(course, speedNm);
            // Account for the distance travelled during the turn at three degrees per second.
            double turnTime = Math.Abs(KinematicsCalculator.NormalizeAngle(course - aircraft.Heading)) / 3;
            double midCourse = aircraft.Heading + KinematicsCalculator.NormalizeAngle(course - aircraft.Heading) / 2;
            var turning = Velocity(midCourse, speedNm);
            velocity = new(velocity.X + (turning.X - velocity.X) * turnTime / LookAhead,
                velocity.Y + (turning.Y - velocity.Y) * turnTime / LookAhead);
            double verticalSpeed = Rate(aircraft.Altitude, AircraftTracker.AvailableAltitude(aircraft, altitude));
            return ClosestDistanceSquared(offset, Subtract(contact.Velocity, velocity),
                contact.Altitude - (double)aircraft.Altitude, contact.VerticalSpeed - verticalSpeed,
                LookAhead, PlanningAltitude) / (PlanningDistance * PlanningDistance);
        }
    }

    public void BeginStep(IReadOnlyList<Aircraft> traffic, double seconds, decimal windSpeed, int windDirection)
    {
        _step = seconds;
        _wind = Velocity(windDirection + 180, (double)windSpeed / 3600);
        Capture(traffic);
    }

    private static double ClosestDistanceSquared(Vector2D offset, Vector2D velocity, double altitude,
        double verticalSpeed, double horizon, double altitudeLimit)
    {
        double start = 0;
        double end = horizon;
        if (verticalSpeed == 0)
        {
            if (Math.Abs(altitude) >= altitudeLimit) return double.PositiveInfinity;
        }
        else
        {
            double enter = (-altitudeLimit - altitude) / verticalSpeed;
            double exit = (altitudeLimit - altitude) / verticalSpeed;
            start = Math.Max(0, Math.Min(enter, exit));
            end = Math.Min(horizon, Math.Max(enter, exit));
            if (start >= end) return double.PositiveInfinity;
        }
        double speedSquared = Dot(velocity, velocity);
        double closest = speedSquared == 0 ? start : Math.Clamp(-Dot(offset, velocity) / speedSquared, start, end);
        var delta = new Vector2D(offset.X + velocity.X * closest, offset.Y + velocity.Y * closest);
        return Dot(delta, delta);
    }

    private static (int, int) Cell(Vector2D point) =>
        ((int)Math.Floor(point.X / CellSize), (int)Math.Floor(point.Y / CellSize));

    private static double Rate(decimal altitude, decimal target) =>
        Math.Clamp((double)(target - altitude) / LookAhead, -25, 25);

    private static Vector2D Velocity(double heading, double speed) => AirspaceNavigator.Along(default, heading, speed);
    private static Vector2D Subtract(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    private static double Dot(Vector2D a, Vector2D b) => a.X * b.X + a.Y * b.Y;
}
