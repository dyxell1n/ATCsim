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
    private const decimal GlideSlope = 318m;
    private const decimal InitialApproachAltitude = FinalDistance * GlideSlope;
    private readonly Dictionary<Aircraft, string> _lastAdvisory = [];

    public void Reset() => _lastAdvisory.Clear();

    public void Update(Aircraft aircraft, IReadOnlyList<Aircraft> traffic, IReadOnlyList<Airport> airports,
        DateTime time, double seconds, AircraftTracker tracker)
    {
        if (!aircraft.IsActive) return;

        var ops = aircraft.Operations;
        Airport? departure = airports.FirstOrDefault(airport => airport.IcaoCode == aircraft.DepartureAirportIcao);
        Airport? arrival = airports.FirstOrDefault(airport => airport.IcaoCode == aircraft.ArrivalAirportIcao);
        Vector2D position = AirspaceNavigator.Position(aircraft);
        Vector2D destination = arrival == null ? new(aircraft.DestinationX, aircraft.DestinationY) : At(arrival);

        if (aircraft.PlannedDepartureSpeed is { } speed)
        {
            if (departure == null || !ReleaseDeparture(aircraft, departure, destination, speed, traffic, time)) return;
        }
        else if (!aircraft.IsAirborne) return;

        if (ops.Phase == FlightPhase.Departing && departure != null)
        {
            Vector2D runwayExit = AirspaceNavigator.Along(At(departure), ops.RunwayHeading, 3);
            if (position.DistanceTo(At(departure)) < 2.9)
            {
                Fly(aircraft, runwayExit, departure, seconds, tracker, straightIn: true);
                return;
            }
            ops.Phase = FlightPhase.EnRoute;
            Say(aircraft, departure, "contact departure", "Contacting departure");
        }

        if (ops.Phase == FlightPhase.MissedApproach && arrival != null)
        {
            var threshold = At(arrival);
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

        if (arrival != null && (ops.Phase is FlightPhase.ArrivalQueue or FlightPhase.Holding ||
            !aircraft.HasManualHeadingOverride && position.DistanceTo(destination) <= ArrivalDistance(aircraft, destination)))
        {
            SequenceArrival(aircraft, arrival, traffic, position, time, seconds, tracker);
            return;
        }

        Vector2D goal = aircraft.HasManualHeadingOverride
            ? AirspaceNavigator.HeadingGoal(position, aircraft.TargetHeading, weather.ActiveZones)
            : destination;
        Fly(aircraft, goal, arrival ?? departure, seconds, tracker);
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

    private bool ReleaseDeparture(Aircraft aircraft, Airport airport, Vector2D destination, decimal speed,
        IReadOnlyList<Aircraft> traffic, DateTime time)
    {
        var threshold = At(airport);
        var runway = AvailableRunways(airport, AirspaceNavigator.Bearing(threshold, destination))
            .FirstOrDefault(candidate => AirspaceNavigator.IsClear(threshold,
                AirspaceNavigator.Along(threshold, candidate.Angle, 3),
                0, (decimal)(3 * 3600 / Math.Max(60, (double)speed) * 25), weather.ActiveZones, 1));
        bool busy = RunwayBusy(airport, traffic, time, aircraft);
        var arrivalReady = !busy && traffic.Any(other => other.IsAirborne && other.ArrivalAirportIcao == airport.IcaoCode &&
            other.Operations.ArrivalQueuedAt.HasValue && !other.Operations.ManualHold &&
            FindApproachRunway(other, airport) != null);
        if (runway == null || arrivalReady || busy || !separation.CanDepart(aircraft, traffic))
        {
            string message = runway == null ? "hold position, departure unavailable" : $"hold short of runway {runway.Designator}";
            Advisory(aircraft, airport, message, runway == null ? "Holding position" : $"Holding short of runway {runway.Designator}");
            return false;
        }

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
        bool patternReady = ops.HoldingPattern == null ||
            (ops.HoldingLeg == 0 && position.DistanceTo(ops.HoldingPattern[0]) < 0.35);
        bool runwayBusy = RunwayBusy(airport, traffic, time, aircraft);
        bool earlierArrival = !runwayBusy && !ops.ManualHold && patternReady && traffic.Any(other => !ReferenceEquals(other, aircraft) && other.IsAirborne &&
            other.ArrivalAirportIcao == airport.IcaoCode && !other.Operations.ManualHold &&
            other.Operations.ArrivalQueuedAt is { } otherTime &&
            (otherTime < ops.ArrivalQueuedAt || otherTime == ops.ArrivalQueuedAt && other.Id < aircraft.Id) &&
            FindApproachRunway(other, airport) != null);

        if (!ops.ManualHold && patternReady && !earlierArrival && !runwayBusy &&
            FindApproachRunway(aircraft, airport) is { } runway)
        {
            ops.Phase = FlightPhase.Approach;
            ops.RunwayDesignator = runway.Designator;
            ops.RunwayHeading = runway.Angle;
            ops.ApproachAligned = false;
            ops.HoldingPattern = null;
            aircraft.TargetAltitude = InitialApproachAltitude;
            _lastAdvisory.Remove(aircraft);
            Say(aircraft, airport, $"cleared approach runway {runway.Designator}, maintain {InitialApproachAltitude:0} feet until established",
                $"Cleared approach runway {runway.Designator}, {InitialApproachAltitude:0} feet until established");
            AdvanceApproach(aircraft, airport, position, time, seconds, tracker);
            return;
        }

        if (ops.HoldingPattern == null)
        {
            ops.Phase = FlightPhase.Holding;
            ops.HoldingAltitude = HoldingAltitude(aircraft, traffic);
            ops.HoldingPattern = BuildHoldingPattern(aircraft, airport, position, ops.HoldingAltitude);
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

    private void AdvanceApproach(Aircraft aircraft, Airport airport, Vector2D position,
        DateTime time, double seconds, AircraftTracker tracker)
    {
        var ops = aircraft.Operations;
        Vector2D threshold = At(airport);
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
                Vector2D entry = AirspaceNavigator.Along(threshold, ops.RunwayHeading + 180, ApproachEntryDistance);
                if (position.DistanceTo(entry) > 0.35)
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
            aircraft.CoordX = airport.CoordX;
            aircraft.CoordY = airport.CoordY;
            aircraft.GroundSpeed = aircraft.Altitude = aircraft.TargetAltitude = 0;
            aircraft.IsActive = false;
            ops.Phase = FlightPhase.Landed;
            ops.RunwayClearAt = time.AddSeconds(45 + WakeSeconds(aircraft));
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
        AircraftTracker tracker, bool straightIn = false, bool final = false)
    {
        Vector2D position = AirspaceNavigator.Position(aircraft);
        decimal speed = aircraft.PlannedDepartureSpeed ?? aircraft.GroundSpeed;
        decimal nextAltitude = KinematicsCalculator.UpdateAltitude(aircraft.Altitude, aircraft.TargetAltitude, seconds);
        double margin = Math.Max(5, (double)(speed + weather.CurrentWindSpeed) / 3600 * 24 + 3);
        var waypoint = straightIn ? goal : AirspaceNavigator.NextWaypoint(position, goal, aircraft.Altitude,
            aircraft.TargetAltitude, weather.ActiveZones, margin);
        if (waypoint == null || waypoint != goal)
            Advisory(aircraft, airport, "deviate to remain clear of restricted airspace or weather", "Deviating to remain clear");
        else if (_lastAdvisory.TryGetValue(aircraft, out var last) && last.StartsWith("deviate", StringComparison.Ordinal))
        {
            _lastAdvisory.Remove(aircraft);
            Say(aircraft, airport, "resume own navigation", "Resuming own navigation");
        }

        double requestedHeading = waypoint.HasValue
            ? AirspaceNavigator.WindCorrectedHeading(position, waypoint.Value, speed, weather.CurrentWindSpeed, weather.CurrentWindDirection)
            : aircraft.Heading;
        if (aircraft.HasManualHeadingOverride && waypoint == goal) requestedHeading = aircraft.TargetHeading;

        decimal? verticalRate = final ? 1500m : null;
        if (verticalRate.HasValue)
            nextAltitude = KinematicsCalculator.UpdateAltitude(aircraft.Altitude, aircraft.TargetAltitude, seconds, verticalRate.Value);
        double? chosenHeading = ChooseSafeHeading(aircraft, position, requestedHeading, speed, nextAltitude, seconds);
        decimal targetAltitude = aircraft.TargetAltitude;
        if (!chosenHeading.HasValue && nextAltitude != aircraft.Altitude)
        {
            chosenHeading = ChooseSafeHeading(aircraft, position, requestedHeading, speed, aircraft.Altitude, seconds);
            if (chosenHeading.HasValue) aircraft.TargetAltitude = aircraft.Altitude;
        }
        if (!chosenHeading.HasValue) return;

        double manualHeading = aircraft.TargetHeading;
        aircraft.TargetHeading = chosenHeading.Value;
        tracker.UpdateAircraftPosition(aircraft, weather, seconds, followDestination: false,
            verticalRate: verticalRate);
        if (aircraft.Operations.Phase != FlightPhase.Landed) aircraft.TargetAltitude = targetAltitude;
        if (aircraft.HasManualHeadingOverride) aircraft.TargetHeading = manualHeading;
    }

    private double? ChooseSafeHeading(Aircraft aircraft, Vector2D position, double heading, decimal speed,
        decimal nextAltitude, double seconds)
    {
        if (Clear(heading, nextAltitude)) return heading;
        for (int offset = 15; offset <= 180; offset += 15)
        {
            double right = KinematicsCalculator.NormalizeCompass(heading + offset);
            if (Clear(right, nextAltitude)) return right;
            double left = KinematicsCalculator.NormalizeCompass(heading - offset);
            if (Clear(left, nextAltitude)) return left;
        }
        return null;

        bool Clear(double course, decimal altitude)
        {
            double actualHeading = KinematicsCalculator.UpdateHeading(aircraft.Heading, course, seconds);
            var (x, y) = KinematicsCalculator.CalculateNextPosition(position.X, position.Y, (double)speed,
                actualHeading, (double)weather.CurrentWindSpeed, weather.CurrentWindDirection, seconds);
            return AirspaceNavigator.IsClear(position, new(Math.Round(x, 4), Math.Round(y, 4)), aircraft.Altitude,
                altitude, weather.ActiveZones, allowExit: true);
        }
    }

    private Runway? FindApproachRunway(Aircraft aircraft, Airport airport)
    {
        var threshold = At(airport);
        var position = AirspaceNavigator.Position(aircraft);
        foreach (var candidate in AvailableRunways(airport, AirspaceNavigator.Bearing(position, threshold)))
        {
            var candidateFix = AirspaceNavigator.Along(threshold, candidate.Angle + 180, FinalDistance);
            var entry = AirspaceNavigator.Along(threshold, candidate.Angle + 180, ApproachEntryDistance);
            if (!AirspaceNavigator.IsClear(candidateFix, threshold, InitialApproachAltitude, 0, weather.ActiveZones, 0.25)) continue;
            if (!AirspaceNavigator.IsClear(entry, candidateFix, InitialApproachAltitude, InitialApproachAltitude, weather.ActiveZones, 2)) continue;
            double distance = position.DistanceTo(entry) + ApproachEntryDistance - FinalDistance;
            double descentTime = (double)Math.Max(0, Math.Abs(aircraft.Altitude - InitialApproachAltitude) - 150) / 25;
            if (distance + 0.5 < descentTime * (double)TerminalSpeed(aircraft, final: true) / 3600) continue;
            if (AirspaceNavigator.NextWaypoint(position, entry, aircraft.Altitude, InitialApproachAltitude, weather.ActiveZones, 5) != null)
                return candidate;
        }
        return null;
    }

    private Vector2D[]? BuildHoldingPattern(Aircraft aircraft, Airport airport, Vector2D position, decimal altitude)
    {
        var threshold = At(airport);
        double bearing = AirspaceNavigator.Bearing(threshold, position);
        double outsideAirspace = RestrictedExtent(threshold, Math.Max(aircraft.Altitude, altitude)) + 20;
        foreach (double radius in new[] { 18, 25, 35, 50, Math.Max(55, outsideAirspace) })
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
                if (IsHoldingPatternClear(legs, altitude) &&
                    AirspaceNavigator.NextWaypoint(position, fix, aircraft.Altitude, altitude, weather.ActiveZones, 5) != null)
                    return legs;
            }
        }
        return null;
    }

    private bool IsHoldingPatternClear(Vector2D[] legs, decimal altitude)
    {
        for (int index = 0; index < legs.Length; index++)
            if (!AirspaceNavigator.IsClear(legs[index], legs[(index + 1) % legs.Length], altitude, altitude, weather.ActiveZones, 8))
                return false;
        return true;
    }

    private static bool RunwayBusy(Airport airport, IReadOnlyList<Aircraft> traffic, DateTime time, Aircraft except) =>
        traffic.Any(other => !ReferenceEquals(other, except) &&
            (other.DepartureAirportIcao == airport.IcaoCode && other.Operations.Phase == FlightPhase.Departing ||
             other.ArrivalAirportIcao == airport.IcaoCode && other.Operations.Phase is FlightPhase.Approach or FlightPhase.Final or FlightPhase.MissedApproach ||
             other.Operations.RunwayClearAt > time && (other.Operations.Phase == FlightPhase.Landed
                 ? other.ArrivalAirportIcao == airport.IcaoCode : other.DepartureAirportIcao == airport.IcaoCode)));

    private static bool CanVector(Aircraft aircraft) => aircraft.IsAirborne &&
        aircraft.Operations.Phase is FlightPhase.EnRoute or FlightPhase.ArrivalQueue or FlightPhase.Holding;

    private static IEnumerable<Runway> AvailableRunways(Airport airport, double heading) => airport.Runways
        .Where(runway => runway.IsAvailable)
        .OrderBy(runway => Math.Abs(KinematicsCalculator.NormalizeAngle(runway.Angle - heading)));

    private static Vector2D At(Airport airport) => new((double)airport.CoordX, (double)airport.CoordY);

    private static int WakeSeconds(Aircraft aircraft) => aircraft.Model is { Category: "HEAVY" } or { IconType: "IL76" } ? 120 : 60;

    private double ArrivalDistance(Aircraft aircraft, Vector2D threshold)
    {
        double descentDistance = (double)Math.Max(0, aircraft.Altitude - InitialApproachAltitude) / 25
            * (double)TerminalSpeed(aircraft, final: true) / 3600;
        return Math.Max(Math.Max(32, descentDistance + 16), RestrictedExtent(threshold, aircraft.Altitude) + 25);
    }

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
