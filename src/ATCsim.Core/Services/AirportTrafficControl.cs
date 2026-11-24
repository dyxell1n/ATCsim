using ATCsim.Core.CollisionDetection;
using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Weather;

namespace ATCsim.Core.Services;

internal sealed class AirportTrafficControl(RadioDispatcherService radio, WeatherService weather,
    CollisionDetector separation, Func<DateTime> clock)
{
    private const int FinalDistance = 10;
    private const int ApproachEntryDistance = 14;
    private const decimal DescentFeetPerSecond = 25m;
    private const decimal GlideSlope = 318m;
    private const decimal InitialApproachAltitude = FinalDistance * GlideSlope;
    private readonly TrafficSeparation _trafficSeparation = new();
    private readonly HashSet<Aircraft> _avoidingTraffic = [];
    private readonly Dictionary<Aircraft, string> _lastAdvisory = [];

    public void Reset()
    {
        _lastAdvisory.Clear();
        _avoidingTraffic.Clear();
    }

    public void BeginStep(IReadOnlyList<Aircraft> traffic, double seconds) =>
        _trafficSeparation.BeginStep(traffic, seconds, weather.CurrentWindSpeed, weather.CurrentWindDirection);

    public void Update(Aircraft aircraft, IReadOnlyList<Aircraft> traffic, IReadOnlyList<Airport> airports,
        DateTime time, double seconds, AircraftTracker tracker)
    {
        if (!aircraft.IsActive)
        {
            if (aircraft.TrailPoints.Count > 0 && aircraft.Operations.LandedAt is { } touchdown &&
                time - touchdown >= TimeSpan.FromSeconds(5))
                aircraft.TrailPoints.Clear();
            return;
        }

        var ops = aircraft.Operations;
        Airport? departure = airports.FirstOrDefault(airport => airport.IcaoCode == aircraft.DepartureAirportIcao);
        Airport? arrival = airports.FirstOrDefault(airport => airport.IcaoCode == aircraft.ArrivalAirportIcao);
        Vector2D position = AirspaceNavigator.Position(aircraft);
        Vector2D destination = arrival == null ? new(aircraft.DestinationX, aircraft.DestinationY) : At(arrival);

        if (aircraft.PlannedDepartureSpeed is { } speed)
        {
            if (departure == null) return;
            bool clearanceValid = ops.Phase == FlightPhase.Departing && departure.Runways.Any(runway =>
                runway.IsAvailable && runway.Designator == ops.RunwayDesignator && runway.Angle == ops.RunwayHeading);
            if (!clearanceValid)
            {
                if (ops.Phase == FlightPhase.Departing)
                {
                    ops.Phase = FlightPhase.EnRoute;
                    ops.RunwayClearAt = null;
                }
                if (!PrepareFuel(aircraft, departure, destination, speed, seconds, traffic) ||
                    !ReleaseDeparture(aircraft, departure, destination, speed, traffic, time)) return;
            }
            position = AirspaceNavigator.Position(aircraft);
        }
        else if (!aircraft.IsAirborne) return;

        if (ops.Phase == FlightPhase.Departing && departure != null)
        {
            var runwayCenter = AssignedRunwayCenter(departure, ops);
            Vector2D runwayExit = AirspaceNavigator.Along(runwayCenter, ops.RunwayHeading, 3);
            if (position.DistanceTo(runwayCenter) < 2.9)
            {
                Fly(aircraft, runwayExit, departure, seconds, tracker, straightIn: true);
                return;
            }
            ops.Phase = FlightPhase.EnRoute;
            Say(aircraft, departure, "contact departure", "Contacting departure");
        }

        if (ops.Phase == FlightPhase.MissedApproach && arrival != null)
        {
            var threshold = AssignedRunwayCenter(arrival, ops);
            var missedFix = AirspaceNavigator.Along(threshold, ops.RunwayHeading, 8);
            bool fixClear = AirspaceNavigator.IsClear(missedFix, missedFix, aircraft.Altitude,
                aircraft.TargetAltitude, weather.ActiveZones, 5);
            double progress = ((position.X - threshold.X) * (missedFix.X - threshold.X) +
                (position.Y - threshold.Y) * (missedFix.Y - threshold.Y)) / 8;
            if (fixClear ? progress < 7.75 : position.DistanceTo(threshold) < 14)
            {
                if (!fixClear)
                    missedFix = AirspaceNavigator.HeadingGoal(position, ops.RunwayHeading, weather.ActiveZones);
                Fly(aircraft, missedFix, arrival, seconds, tracker);
                return;
            }
            ops.Phase = FlightPhase.ArrivalQueue;
        }

        if ((ops.Phase is FlightPhase.Approach or FlightPhase.Final) && arrival != null)
        {
            AdvanceApproach(aircraft, arrival, position, time, seconds, tracker);
            return;
        }

        if (arrival != null)
        {
            double distance = position.DistanceTo(destination);
            double arrivalRange = Math.Max(32, RestrictedExtent(destination, aircraft.Altitude) + 25);
            if (ops.Phase is FlightPhase.ArrivalQueue or FlightPhase.Holding ||
                !aircraft.HasManualHeadingOverride && distance <= arrivalRange)
            {
                SequenceArrival(aircraft, arrival, traffic, position, time, seconds, tracker);
                return;
            }
            if (!aircraft.HasManualHeadingOverride && !ops.ArrivalDescentStarted &&
                aircraft.Altitude > InitialApproachAltitude &&
                distance <= arrivalRange + DescentDistance(aircraft, aircraft.GroundSpeed))
            {
                ops.ArrivalDescentStarted = true;
                Say(aircraft, arrival, "start descent for arrival", "Leaving cruise altitude for arrival");
            }
        }

        Vector2D goal = aircraft.HasManualHeadingOverride
            ? AirspaceNavigator.HeadingGoal(position, aircraft.TargetHeading, weather.ActiveZones)
            : destination;
        decimal? descent = ops.ArrivalDescentStarted && !aircraft.HasManualHeadingOverride
            ? Math.Min(aircraft.TargetAltitude > 0 ? aircraft.TargetAltitude : aircraft.Altitude, InitialApproachAltitude)
            : null;
        Fly(aircraft, goal, arrival ?? departure, seconds, tracker, altitudeOverride: descent);
    }

    public bool SetHeading(Aircraft aircraft, Airport? airport, double heading)
    {
        if (!CanVector(aircraft)) return false;
        aircraft.Operations.ManualHold = false;
        aircraft.Operations.HoldingPattern = null;
        aircraft.Operations.ArrivalQueuedAt = null;
        aircraft.Operations.Phase = FlightPhase.EnRoute;
        aircraft.HasManualHeadingOverride = true;
        aircraft.TargetHeading = KinematicsCalculator.NormalizeCompass(heading);
        int spokenHeading = (int)Math.Round(aircraft.TargetHeading) % 360;
        if (spokenHeading == 0) spokenHeading = 360;
        Say(aircraft, airport, $"fly heading {spokenHeading:000}", $"Heading {spokenHeading:000}");
        return true;
    }

    public void ResumeRoute(Aircraft aircraft, Airport? airport)
    {
        if (!CanVector(aircraft)) return;
        aircraft.HasManualHeadingOverride = false;
        aircraft.Operations.ManualHold = false;
        Say(aircraft, airport, $"resume own navigation to {aircraft.ArrivalAirportIcao}",
            $"Resuming own navigation to {aircraft.ArrivalAirportIcao}");
    }

    public void RequestHolding(Aircraft aircraft)
    {
        if (!CanVector(aircraft)) return;
        aircraft.HasManualHeadingOverride = false;
        aircraft.Operations.ManualHold = true;
        aircraft.Operations.Phase = FlightPhase.ArrivalQueue;
    }

    public void ChangeAltitude(Aircraft aircraft, Airport? airport, decimal altitude)
    {
        if (!aircraft.IsAirborne || aircraft.Operations.Phase != FlightPhase.EnRoute) return;
        string verb = altitude < aircraft.Altitude ? "descend" : "climb";
        aircraft.TargetAltitude = altitude;
        Say(aircraft, airport, $"{verb} and maintain {altitude:0} feet", $"{verb} and maintain {altitude:0} feet");
    }

    private bool PrepareFuel(Aircraft aircraft, Airport airport, Vector2D destination, decimal speed, double seconds,
        IReadOnlyList<Aircraft> traffic)
    {
        var origin = At(airport);
        var ops = aircraft.Operations;
        decimal required = ops.DepartureFuelKg ??= AircraftPerformance.DepartureFuel(aircraft.Model,
            origin.DistanceTo(destination), speed, weather.CurrentWindSpeed, weather.CurrentWindDirection,
            AirspaceNavigator.Bearing(origin, destination),
            traffic.Count(other => other != aircraft && other.IsActive &&
                other.ArrivalAirportIcao == aircraft.ArrivalAirportIcao));
        decimal capacity = AircraftPerformance.FuelCapacity(aircraft.Model);
        if (required > capacity)
        {
            Advisory(aircraft, airport, $"hold position, route needs {required:0} kg of fuel", "Route exceeds fuel capacity");
            return false;
        }
        if (aircraft.FuelRemaining >= required) return true;

        if (ops.Phase != FlightPhase.Refueling)
        {
            ops.Phase = FlightPhase.Refueling;
            radio.AddSystemLog("GROUND", $"{aircraft.Callsign} at {airport.IcaoCode}: refueling to {required:0} kg.");
        }
        aircraft.FuelRemaining = Math.Min(required, aircraft.FuelRemaining + capacity / 90m * (decimal)seconds);
        if (aircraft.FuelRemaining < required) return false;

        ops.Phase = FlightPhase.EnRoute;
        radio.AddSystemLog("GROUND", $"{aircraft.Callsign} at {airport.IcaoCode}: {required:0} kg loaded including reserve.");
        return true;
    }

    private bool ReleaseDeparture(Aircraft aircraft, Airport airport, Vector2D destination, decimal speed,
        IReadOnlyList<Aircraft> traffic, DateTime time)
    {
        var course = AirspaceNavigator.Bearing(At(airport), destination);
        var runways = AvailableRunways(airport, course)
            .Where(candidate => ClearForDeparture(candidate, airport, speed)).ToArray();
        var runway = runways.FirstOrDefault(candidate =>
            !RunwayBusy(airport, candidate, traffic, time, aircraft) &&
            separation.CanDepart(aircraft, traffic, RunwayLayout.Center(airport, candidate)) &&
            !traffic.Any(other => other.IsAirborne && other.ArrivalAirportIcao == airport.IcaoCode &&
                other.Operations.ArrivalQueuedAt.HasValue && !other.Operations.ManualHold &&
                FindApproachRunway(other, airport) is { } arrival &&
                RunwayLayout.SharesProtection(candidate, arrival.Runway) &&
                (FuelEndurance(other) <= 0.5m || AirspaceNavigator.Position(other).DistanceTo(
                    AirspaceNavigator.Along(RunwayLayout.Center(airport, arrival.Runway),
                        arrival.Runway.Angle + 180, arrival.EntryDistance)) <= 12)));
        if (runway == null)
        {
            string message = runways.Length == 0 ? "hold position, departure unavailable"
                : $"hold short of runway {runways[0].Designator}";
            Advisory(aircraft, airport, message, runways.Length == 0 ? "Holding position" : $"Holding short of runway {runways[0].Designator}");
            return false;
        }

        var center = RunwayLayout.Center(airport, runway);
        aircraft.CoordX = (decimal)center.X;
        aircraft.CoordY = (decimal)center.Y;
        var ops = aircraft.Operations;
        ops.Phase = FlightPhase.Departing;
        ops.RunwayDesignator = runway.Designator;
        ops.RunwayHeading = runway.Angle;
        var clearanceSeconds = Math.Max(60, 3 * 3600 / Math.Max(60, (double)speed));
        ops.RunwayClearAt = time.AddSeconds(clearanceSeconds + WakeSeconds(aircraft));
        aircraft.Heading = aircraft.TargetHeading = runway.Angle % 360;
        _lastAdvisory.Remove(aircraft);
        Say(aircraft, airport, $"runway {runway.Designator}, cleared for takeoff",
            $"Cleared for takeoff runway {runway.Designator}");
        return true;
    }

    private void SequenceArrival(Aircraft aircraft, Airport airport, IReadOnlyList<Aircraft> traffic,
        Vector2D position, DateTime time, double seconds, AircraftTracker tracker)
    {
        var ops = aircraft.Operations;
        ops.ArrivalQueuedAt ??= time;
        if (ops.HoldingPattern is { } activePattern && !IsHoldingPatternClear(activePattern, ops.HoldingAltitude))
            ops.HoldingPattern = null;
        var approach = ops.ManualHold ? null : FindApproachRunway(aircraft, airport,
            candidate => !RunwayBusy(airport, candidate, traffic, time, aircraft, arriving: true));
        if (approach is { } clearance && !traffic.Any(other => other != aircraft && other.IsAirborne &&
            other.ArrivalAirportIcao == airport.IcaoCode && !other.Operations.ManualHold &&
            other.Operations.ArrivalQueuedAt is { } otherTime &&
            EarlierArrival(other, otherTime, aircraft, ops.ArrivalQueuedAt.Value) &&
            FindApproachRunway(other, airport, candidate => RunwayLayout.SharesProtection(candidate, clearance.Runway) &&
                !RunwayBusy(airport, candidate, traffic, time, other, arriving: true)) != null))
        {
            ops.Phase = FlightPhase.Approach;
            ops.RunwayDesignator = clearance.Runway.Designator;
            ops.RunwayHeading = clearance.Runway.Angle;
            ops.ApproachEntryDistanceNm = clearance.EntryDistance;
            ops.ApproachAligned = clearance.StraightIn;
            ops.HoldingPattern = null;
            aircraft.TargetAltitude = InitialApproachAltitude;
            _lastAdvisory.Remove(aircraft);
            Say(aircraft, airport, $"cleared approach runway {clearance.Runway.Designator}, maintain {InitialApproachAltitude:0} feet until established",
                $"Cleared approach runway {clearance.Runway.Designator}, {InitialApproachAltitude:0} feet until established");
            AdvanceApproach(aircraft, airport, position, time, seconds, tracker);
            return;
        }

        if (ops.HoldingPattern == null)
        {
            ops.Phase = FlightPhase.Holding;
            ops.HoldingAltitude = HoldingAltitude(aircraft, traffic);
            ops.HoldingPattern = BuildHoldingPattern(aircraft, airport, position, ops.HoldingAltitude, traffic);
            ops.HoldingLeg = 0;
            if (ops.HoldingPattern != null)
            {
                Vector2D fix = ops.HoldingPattern[0];
                double course = AirspaceNavigator.Bearing(ops.HoldingPattern[^1], fix);
                string holding = $"hold at fix {fix.X:0}/{fix.Y:0}, inbound course {course:000}, right turns, maintain {ops.HoldingAltitude:0} feet";
                Say(aircraft, airport, $"{holding}, expect further clearance {time.AddMinutes(4):HHmm}", holding);
            }
        }

        aircraft.TargetAltitude = ops.HoldingAltitude;
        ReduceSpeed(aircraft, TerminalSpeed(aircraft, final: false), seconds);
        if (ops.HoldingPattern is not { } pattern)
        {
            Advisory(aircraft, airport, "approach unavailable, remain outside restricted airspace", "Remaining clear of restricted airspace");
            Fly(aircraft, AirspaceNavigator.HeadingGoal(position, aircraft.Heading, weather.ActiveZones), airport, seconds, tracker);
            return;
        }

        Vector2D nextFix = pattern[ops.HoldingLeg];
        if (position.DistanceTo(nextFix) < 0.35)
        {
            ops.HoldingLeg = (ops.HoldingLeg + 1) % pattern.Length;
            nextFix = pattern[ops.HoldingLeg];
        }
        Fly(aircraft, nextFix, airport, seconds, tracker);
    }

    private static decimal FuelEndurance(Aircraft aircraft)
    {
        decimal burn = AircraftPerformance.FuelBurnPerHour(aircraft.Model);
        return burn > 0 ? aircraft.FuelRemaining / burn : decimal.MaxValue;
    }

    private static bool EarlierArrival(Aircraft other, DateTime otherTime, Aircraft aircraft, DateTime queuedAt)
    {
        decimal otherEndurance = FuelEndurance(other);
        decimal endurance = FuelEndurance(aircraft);
        if (otherEndurance != endurance && Math.Min(otherEndurance, endurance) <= 0.5m)
            return otherEndurance < endurance;
        return otherTime < queuedAt || otherTime == queuedAt && other.Id < aircraft.Id;
    }

    private void AdvanceApproach(Aircraft aircraft, Airport airport, Vector2D position,
        DateTime time, double seconds, AircraftTracker tracker)
    {
        var ops = aircraft.Operations;
        Vector2D threshold = AssignedRunwayCenter(airport, ops);
        Vector2D finalFix = AirspaceNavigator.Along(threshold, ops.RunwayHeading + 180, FinalDistance);
        if (!airport.Runways.Any(runway => runway.IsAvailable && runway.Designator == ops.RunwayDesignator && runway.Angle == ops.RunwayHeading)
            || !AirspaceNavigator.IsClear(finalFix, threshold, InitialApproachAltitude, 0, weather.ActiveZones, 0.25))
        {
            GoAround(aircraft, airport, time);
            Fly(aircraft, AirspaceNavigator.Along(threshold, ops.RunwayHeading, 8), airport, seconds, tracker);
            return;
        }

        ReduceSpeed(aircraft, TerminalSpeed(aircraft, final: true), seconds);
        if (ops.Phase == FlightPhase.Approach)
        {
            aircraft.TargetAltitude = InitialApproachAltitude;
            if (!ops.ApproachAligned)
            {
                Vector2D entry = AirspaceNavigator.Along(threshold, ops.RunwayHeading + 180, ops.ApproachEntryDistanceNm ?? ApproachEntryDistance);
                if (position.DistanceTo(entry) > 0.65)
                {
                    Fly(aircraft, entry, airport, seconds, tracker);
                    return;
                }
                ops.ApproachAligned = true;
            }
            if (position.DistanceTo(finalFix) > 0.3)
            {
                Fly(aircraft, finalFix, airport, seconds, tracker);
                return;
            }
            if (Math.Abs(aircraft.Altitude - InitialApproachAltitude) > 250 ||
                Math.Abs(KinematicsCalculator.NormalizeAngle(aircraft.Heading - ops.RunwayHeading)) > 30)
            {
                GoAround(aircraft, airport, time);
                return;
            }
            ops.Phase = FlightPhase.Final;
            Say(aircraft, airport, $"runway {ops.RunwayDesignator}, cleared to land", $"Cleared to land runway {ops.RunwayDesignator}");
        }

        double distance = position.DistanceTo(threshold);
        if (distance <= 0.25 && aircraft.Altitude <= 120)
        {
            if (!AirspaceNavigator.IsClear(position, threshold, aircraft.Altitude, 0, weather.ActiveZones))
            {
                GoAround(aircraft, airport, time);
                return;
            }
            aircraft.TrailPoints.Add(new(threshold.X, threshold.Y, 0));
            ops.LandedAt = time;
            aircraft.CoordX = (decimal)threshold.X;
            aircraft.CoordY = (decimal)threshold.Y;
            aircraft.GroundSpeed = aircraft.Altitude = aircraft.TargetAltitude = 0;
            aircraft.IsActive = false;
            ops.Phase = FlightPhase.Landed;
            ops.RunwayClearAt = time.AddSeconds(FlightOperationsState.TaxiInSeconds + WakeSeconds(aircraft));
            Say(aircraft, airport, "vacate runway when able", "Vacating runway");
            return;
        }
        if (distance < 0.3 && aircraft.Altitude > 120)
        {
            GoAround(aircraft, airport, time);
            return;
        }

        aircraft.TargetAltitude = (decimal)distance * GlideSlope;
        Fly(aircraft, threshold, airport, seconds, tracker, straightIn: true, final: true);
    }

    private void GoAround(Aircraft aircraft, Airport airport, DateTime time)
    {
        var ops = aircraft.Operations;
        ops.Phase = FlightPhase.MissedApproach;
        ops.ArrivalQueuedAt = time;
        aircraft.TargetAltitude = 4000;
        Say(aircraft, airport, "go around, climb and maintain 4000 feet", "Going around, climbing to 4000 feet");
    }

    private void Fly(Aircraft aircraft, Vector2D goal, Airport? airport, double seconds,
        AircraftTracker tracker, bool straightIn = false, bool final = false, decimal? altitudeOverride = null)
    {
        Vector2D position = AirspaceNavigator.Position(aircraft);
        decimal speed = aircraft.PlannedDepartureSpeed ?? aircraft.GroundSpeed;
        decimal targetAltitude = AircraftTracker.AvailableAltitude(aircraft, altitudeOverride ?? aircraft.TargetAltitude);
        decimal? verticalRate = final ? 1500m : null;
        double margin = Math.Max(5, (double)(speed + weather.CurrentWindSpeed) / 3600 * 24 + 3);
        var waypoint = straightIn ? goal : AirspaceNavigator.NextWaypoint(position, goal, aircraft.Altitude,
            targetAltitude, weather.ActiveZones, margin);
        if (waypoint == null || waypoint != goal)
            Advisory(aircraft, airport, "deviate to remain clear of restricted airspace or weather", "Deviating to remain clear");
        else if (_lastAdvisory.TryGetValue(aircraft, out var last) && last.StartsWith("deviate", StringComparison.Ordinal))
        {
            _lastAdvisory.Remove(aircraft);
            Say(aircraft, airport, "resume own navigation", "Resuming own navigation");
        }

        double requestedHeading = waypoint.HasValue
            ? AirspaceNavigator.WindCorrectedHeading(position, waypoint.Value, speed, weather.CurrentWindSpeed, weather.CurrentWindDirection)
            : AirspaceNavigator.EscapeHeading(position, aircraft.Altitude, aircraft.Heading, weather.ActiveZones);
        if (aircraft.HasManualHeadingOverride && waypoint == goal) requestedHeading = aircraft.TargetHeading;

        double? zoneHeading = ChooseSafeHeading(requestedHeading, targetAltitude);
        if (!zoneHeading.HasValue && targetAltitude != aircraft.Altitude)
        {
            targetAltitude = aircraft.Altitude;
            zoneHeading = ChooseSafeHeading(requestedHeading, targetAltitude);
        }
        if (!zoneHeading.HasValue)
        {
            // A newly activated zone can leave the nose pointing into the obstacle.
            // Finish turning before committing any more translation.
            double escape = AirspaceNavigator.EscapeHeading(position, aircraft.Altitude, requestedHeading, weather.ActiveZones);
            aircraft.Heading = KinematicsCalculator.UpdateHeading(aircraft.Heading, escape, seconds);
            return;
        }

        var maneuver = _trafficSeparation.Resolve(aircraft, position, zoneHeading.Value, targetAltitude,
            speed, straightIn, verticalRate, Clear);
        if (maneuver == null || maneuver.Value != (zoneHeading.Value, targetAltitude))
        {
            if (_avoidingTraffic.Add(aircraft))
                Say(aircraft, airport, "traffic, maintain separation", "Maneuvering for traffic");
        }
        else
            _avoidingTraffic.Remove(aircraft);
        if (maneuver is not { } clearance)
        {
            aircraft.Heading = KinematicsCalculator.UpdateHeading(aircraft.Heading, aircraft.Heading + 90, seconds);
            return;
        }

        double originalHeading = aircraft.TargetHeading;
        decimal originalAltitude = aircraft.TargetAltitude;
        aircraft.TargetHeading = clearance.Heading;
        aircraft.TargetAltitude = clearance.Altitude;
        tracker.UpdateAircraftPosition(aircraft, weather, seconds, followDestination: false, verticalRate: verticalRate);
        aircraft.TargetAltitude = originalAltitude;
        if (aircraft.HasManualHeadingOverride) aircraft.TargetHeading = originalHeading;

        double? ChooseSafeHeading(double heading, decimal altitude)
        {
            if (Clear(heading, altitude)) return heading;
            // Try both fully available turn directions before intermediate offsets.
            double right = KinematicsCalculator.NormalizeCompass(aircraft.Heading + 90);
            if (Clear(right, altitude)) return right;
            double left = KinematicsCalculator.NormalizeCompass(aircraft.Heading - 90);
            if (Clear(left, altitude)) return left;
            return null;
        }

        bool Clear(double course, decimal altitude)
        {
            double actualHeading = KinematicsCalculator.UpdateHeading(aircraft.Heading, course, seconds);
            var (x, y) = KinematicsCalculator.CalculateNextPosition(position.X, position.Y, (double)speed,
                actualHeading, (double)weather.CurrentWindSpeed, weather.CurrentWindDirection, seconds);
            return AirspaceNavigator.IsClear(position, new(Math.Round(x, 4), Math.Round(y, 4)), aircraft.Altitude,
                AircraftTracker.NextAltitude(aircraft, altitude, seconds, verticalRate), weather.ActiveZones, allowExit: true);
        }
    }

    private readonly record struct Approach(Runway Runway, double EntryDistance, bool StraightIn);

    private Approach? FindApproachRunway(Aircraft aircraft, Airport airport, Func<Runway, bool>? eligible = null)
    {
        var position = AirspaceNavigator.Position(aircraft);
        foreach (var candidate in AvailableRunways(airport, AirspaceNavigator.Bearing(position, At(airport))))
        {
            if (eligible != null && !eligible(candidate)) continue;
            var threshold = RunwayLayout.Center(airport, candidate);
            var finalFix = AirspaceNavigator.Along(threshold, candidate.Angle + 180, FinalDistance);
            if (!AirspaceNavigator.IsClear(finalFix, threshold, InitialApproachAltitude, 0, weather.ActiveZones, 0.25)) continue;

            decimal approachSpeed = Math.Min(aircraft.GroundSpeed, TerminalSpeed(aircraft, final: false));
            double slowdown = (double)(aircraft.GroundSpeed - approachSpeed);
            double descent = DescentDistance(aircraft, approachSpeed) + slowdown * slowdown / 14400;
            double inboundHeading = AirspaceNavigator.Bearing(position, finalFix);
            double fixDistance = position.DistanceTo(finalFix);
            bool straightIn = fixDistance >= descent &&
                (fixDistance <= 0.3 || Math.Abs(KinematicsCalculator.NormalizeAngle(inboundHeading - candidate.Angle)) < 20) &&
                Math.Abs(KinematicsCalculator.NormalizeAngle(aircraft.Heading - candidate.Angle)) < 45 &&
                AirspaceNavigator.IsClear(position, finalFix, aircraft.Altitude, InitialApproachAltitude, weather.ActiveZones, 2);
            if (straightIn) return new(candidate, ApproachEntryDistance, true);

            double entryDistance = Math.Max(ApproachEntryDistance, FinalDistance + descent + 2);
            var entry = AirspaceNavigator.Along(threshold, candidate.Angle + 180, entryDistance);
            if (!AirspaceNavigator.IsClear(entry, finalFix, aircraft.Altitude, InitialApproachAltitude,
                weather.ActiveZones, 2)) continue;
            if (AirspaceNavigator.NextWaypoint(position, entry, aircraft.Altitude, InitialApproachAltitude,
                weather.ActiveZones, 5) != null)
                return new(candidate, entryDistance, false);
        }
        return null;
    }

    private bool ClearForDeparture(Runway runway, Airport airport, decimal speed)
    {
        var center = RunwayLayout.Center(airport, runway);
        return AirspaceNavigator.IsClear(center, AirspaceNavigator.Along(center, runway.Angle, 3), 0,
            (decimal)(3 * 3600 / Math.Max(60, (double)speed)) * DescentFeetPerSecond, weather.ActiveZones, 1);
    }

    private Vector2D[]? BuildHoldingPattern(Aircraft aircraft, Airport airport, Vector2D position, decimal altitude,
        IReadOnlyList<Aircraft> traffic)
    {
        var threshold = At(airport);
        double bearing = AirspaceNavigator.Bearing(threshold, position);
        double outsideAirspace = RestrictedExtent(threshold, Math.Max(aircraft.Altitude, altitude)) + 20;
        var nearbyPatterns = traffic.Where(other => other != aircraft && other.Operations.Phase == FlightPhase.Holding &&
            Math.Abs(other.Operations.HoldingAltitude - altitude) < CollisionDetector.VerticalSeparationMinFt)
            .Select(other => other.Operations.HoldingPattern).OfType<Vector2D[]>().Where(pattern => pattern.Length > 0)
            .Select(Bounds).ToArray();
        foreach (double radius in new[] { 18, 25, 35, 50, Math.Max(65, outsideAirspace),
            Math.Max(85, outsideAirspace + 20), Math.Max(105, outsideAirspace + 40) })
        {
            for (int turn = 0; turn < 12; turn++)
            {
                double outbound = bearing + 30 * turn;
                Vector2D fix = AirspaceNavigator.Along(threshold, outbound, radius);
                double inbound = outbound + 180;
                double width = Math.Max(4, (double)TerminalSpeed(aircraft, final: false) / 35);
                Vector2D turnOut = AirspaceNavigator.Along(fix, inbound + 90, width);
                Vector2D outboundEnd = AirspaceNavigator.Along(turnOut, outbound, 6);
                Vector2D inboundStart = AirspaceNavigator.Along(fix, outbound, 6);
                Vector2D[] legs = [fix, turnOut, outboundEnd, inboundStart];
                var (left, top, right, bottom) = Bounds(legs);
                const double margin = CollisionDetector.HorizontalSeparationMinNm + 1;
                if (nearbyPatterns.Any(other => left - margin < other.Right && right + margin > other.Left &&
                    top - margin < other.Bottom && bottom + margin > other.Top)) continue;
                if (IsHoldingPatternClear(legs, altitude) &&
                    AirspaceNavigator.NextWaypoint(position, fix, aircraft.Altitude, altitude, weather.ActiveZones, 5) != null)
                    return legs;
            }
        }
        return null;
    }

    private static (double Left, double Top, double Right, double Bottom) Bounds(Vector2D[] pattern) =>
        (pattern.Min(point => point.X), pattern.Min(point => point.Y),
            pattern.Max(point => point.X), pattern.Max(point => point.Y));

    private bool IsHoldingPatternClear(Vector2D[] legs, decimal altitude)
    {
        for (int index = 0; index < legs.Length; index++)
            if (!AirspaceNavigator.IsClear(legs[index], legs[(index + 1) % legs.Length], altitude, altitude, weather.ActiveZones, 8))
                return false;
        return true;
    }

    private static bool RunwayBusy(Airport airport, Runway candidate, IReadOnlyList<Aircraft> traffic, DateTime time, Aircraft except, bool arriving = false)
    {
        foreach (var other in traffic)
        {
            if (other == except) continue;
            var ops = other.Operations;
            bool inbound = ops.Phase is FlightPhase.Approach or FlightPhase.Final or FlightPhase.MissedApproach or FlightPhase.Landed;
            if ((inbound ? other.ArrivalAirportIcao : other.DepartureAirportIcao) != airport.IcaoCode) continue;
            bool onApproach = ops.Phase is FlightPhase.Approach or FlightPhase.Final or FlightPhase.MissedApproach;
            if (!onApproach && ops.Phase != FlightPhase.Departing && !(ops.RunwayClearAt > time)) continue;
            var occupied = airport.Runways.Find(runway => runway.Designator == ops.RunwayDesignator);
            // The simulated parallels do not provide lateral separation on simultaneous finals.
            if (occupied == null || RunwayLayout.SharesProtection(candidate, occupied) || (arriving && onApproach) ||
                (ops.Phase == FlightPhase.Departing && other.PlannedDepartureSpeed.HasValue))
                return true;
        }
        return false;
    }

    private static Vector2D AssignedRunwayCenter(Airport airport, FlightOperationsState ops) =>
        airport.Runways.Find(runway => runway.Designator == ops.RunwayDesignator) is { } runway
            ? RunwayLayout.Center(airport, runway)
            : At(airport);

    private static bool CanVector(Aircraft aircraft) => aircraft.IsAirborne &&
        aircraft.Operations.Phase is FlightPhase.EnRoute or FlightPhase.ArrivalQueue or FlightPhase.Holding;

    private static IEnumerable<Runway> AvailableRunways(Airport airport, double heading) => airport.Runways
        .Where(runway => runway.IsAvailable)
        .OrderBy(runway => Math.Abs(KinematicsCalculator.NormalizeAngle(runway.Angle - heading)));

    private static Vector2D At(Airport airport) => new((double)airport.CoordX, (double)airport.CoordY);

    private static int WakeSeconds(Aircraft aircraft) => aircraft.Model is { Category: "HEAVY" } or { IconType: "IL76" } ? 120 : 60;

    private double DescentDistance(Aircraft aircraft, decimal speed) =>
        (double)(Math.Abs(aircraft.Altitude - InitialApproachAltitude) / DescentFeetPerSecond)
        * (double)(speed + weather.CurrentWindSpeed) / 3600;

    private double RestrictedExtent(Vector2D threshold, decimal altitude)
    {
        double extent = 0;
        foreach (var zone in weather.ActiveZones)
        {
            if (!zone.BlocksFlight || zone.Radius <= 0 || zone.MinAltitude > altitude) continue;
            double distance = threshold.DistanceTo(new Vector2D((double)zone.CenterX, (double)zone.CenterY));
            if (distance <= (double)zone.Radius + ApproachEntryDistance)
                extent = Math.Max(extent, distance + (double)zone.Radius);
        }
        return extent;
    }

    private static decimal TerminalSpeed(Aircraft aircraft, bool final) => aircraft.Model?.Category == "LIGHT"
        ? final ? 85m : 115m
        : final ? 150m : 210m;

    private static void ReduceSpeed(Aircraft aircraft, decimal target, double seconds) =>
        aircraft.GroundSpeed = Math.Min(aircraft.GroundSpeed, Math.Max(target, aircraft.GroundSpeed - (decimal)seconds * 2));

    private static decimal HoldingAltitude(Aircraft aircraft, IReadOnlyList<Aircraft> traffic)
    {
        decimal altitude = 5000;
        var occupied = traffic.Where(other => !ReferenceEquals(other, aircraft) &&
            other.ArrivalAirportIcao == aircraft.ArrivalAirportIcao && other.Operations.Phase == FlightPhase.Holding)
            .Select(other => other.Operations.HoldingAltitude).ToHashSet();
        while (occupied.Contains(altitude)) altitude += 1000;
        return Math.Min(altitude, aircraft.Model?.MaxAltitude is > 0 and { } ceiling ? ceiling : 40000);
    }

    private void Advisory(Aircraft aircraft, Airport? airport, string instruction, string readback)
    {
        if (_lastAdvisory.TryGetValue(aircraft, out var previous) && previous == instruction) return;
        _lastAdvisory[aircraft] = instruction;
        Say(aircraft, airport, instruction, readback);
    }

    private void Say(Aircraft aircraft, Airport? airport, string instruction, string readback)
    {
        var time = clock();
        radio.DispatchAtcInstruction(aircraft, instruction, airport == null ? "SECTOR CONTROL" : $"{airport.IcaoCode} TOWER", airport?.Id ?? 0, time);
        radio.GeneratePilotReadback(aircraft, readback, airport?.Id ?? 0, time);
    }
}
