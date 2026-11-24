using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Services;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Tests;

public class AirportTrafficControlTests
{
    [Fact]
    public void TenDeparturesUseSharedRunwayOneAtATimeWithReadbacks()
    {
        var origin = AirportAt(1, "UKLL", 20);
        var destination = AirportAt(2, "UKBB", 990);
        Aircraft[] aircraft = Enumerable.Range(1, 10).Select(id =>
        {
            var flight = new Aircraft { Id = id, Callsign = $"TST{id}", FuelRemaining = 20000 };
            AircraftTracker.ScheduleDeparture(flight, origin, destination, 12000, 360);
            return flight;
        }).ToArray();
        using var engine = StartSector([origin, destination], [.. aircraft]);

        engine.Advance(1);
        Assert.Single(aircraft, flight => flight.IsAirborne);
        Assert.All(aircraft.Skip(1), flight => Assert.Equal(origin.CoordX, flight.CoordX));
        Assert.Contains(engine.Radio.Logs, log => log.Message.Contains("hold short of runway"));

        engine.Advance(1200);
        var clearances = engine.Radio.Logs.Where(log => log.SenderType == "ATC" && log.Message.Contains("cleared for takeoff")).ToArray();
        Assert.Equal(aircraft.Select(flight => flight.Id), clearances.Select(log => log.AircraftId));
        Assert.All(clearances.Zip(clearances.Skip(1)), pair => Assert.True(pair.Second.Timestamp - pair.First.Timestamp >= TimeSpan.FromMinutes(2)));
        Assert.All(clearances, clearance => Assert.Contains(engine.Radio.Logs, log => log.SenderType == "AIRCRAFT" &&
            log.AircraftId == clearance.AircraftId && log.AirportId == origin.Id && log.Timestamp == clearance.Timestamp &&
            log.Message.Contains("Cleared for takeoff")));
    }

    [Fact]
    public void ArrivalsHoldAndLandInSequence()
    {
        var airport = AirportAt(1, "UKBB", 100);
        var first = Inbound(1, airport, 75, altitude: 3180);
        var second = Inbound(2, airport, 70, altitude: 5500);
        using var engine = StartSector([airport], [first, second]);

        engine.Advance(1);
        Assert.Equal(FlightPhase.Approach, first.Operations.Phase);
        Assert.Equal(FlightPhase.Holding, second.Operations.Phase);
        Assert.Equal(4, second.Operations.HoldingPattern!.Length);
        Assert.Contains(engine.Radio.Logs, log => log.AircraftId == second.Id && log.Message.Contains("right turns"));

        var visitedHoldingLegs = new HashSet<int>();
        for (int i = 0; i < 3000 && second.IsAirborne; i++)
        {
            engine.Advance(1);
            if (second.Operations.Phase == FlightPhase.Holding) visitedHoldingLegs.Add(second.Operations.HoldingLeg);
            if (second.Operations.Phase is FlightPhase.Approach or FlightPhase.Final)
                Assert.Equal(FlightPhase.Landed, first.Operations.Phase);
        }

        Assert.True(visitedHoldingLegs.Count > 1);
        Assert.Equal(FlightPhase.Landed, first.Operations.Phase);
        Assert.Equal(FlightPhase.Landed, second.Operations.Phase);
        Assert.False(first.IsAirborne);
        Assert.False(second.IsAirborne);
        var landingClearances = engine.Radio.Logs.Where(log => log.SenderType == "ATC" && log.Message.Contains("cleared to land"));
        Assert.Equal(new[] { first.Id, second.Id }, landingClearances.Select(log => log.AircraftId));
    }

    [Fact]
    public void ClosingRunwayOnFinalTriggersMissedApproach()
    {
        var airport = AirportAt(1, "UKBB", 100);
        var flight = Inbound(1, airport, 75, altitude: 3180);
        using var engine = StartSector([airport], [flight]);
        for (int i = 0; i < 600 && flight.Operations.Phase != FlightPhase.Final; i++) engine.Advance(1);
        Assert.Equal(FlightPhase.Final, flight.Operations.Phase);

        foreach (var runway in airport.Runways) runway.IsAvailable = false;
        engine.Advance(2);

        Assert.Equal(FlightPhase.MissedApproach, flight.Operations.Phase);
        Assert.True(flight.TargetAltitude > flight.Altitude);
        Assert.Contains(engine.Radio.Logs, log => log.SenderType == "ATC" && log.Message.Contains("go around"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutomaticAndManualCoursesAvoidRestrictedAirspace(bool controllerHeading)
    {
        var airport = AirportAt(1, "UKBB", 220);
        var flight = Inbound(1, airport, 10, altitude: 10000);
        var zone = new WeatherZone { CenterX = 75, CenterY = 100, Radius = 15, ZoneType = "RESTRICTED", MaxAltitude = 20000 };
        using var engine = StartSector([airport], [flight], [zone]);
        if (controllerHeading) engine.VectorAircraft(flight, 90);

        for (int i = 0; i < 1900 && flight.CoordX < 112; i++)
        {
            engine.Advance(1);
            var dx = (double)(flight.CoordX - zone.CenterX);
            var dy = (double)(flight.CoordY - zone.CenterY);
            Assert.True(dx * dx + dy * dy > (double)(zone.Radius * zone.Radius), $"Track entered zone at {flight.CoordX}/{flight.CoordY}");
        }

        Assert.True(flight.CoordX >= 112, $"Track failed to cross zone longitude: {flight.CoordX}/{flight.CoordY}");
        Assert.Contains(engine.Radio.Logs, log => log.SenderType == "ATC" && log.Message.Contains("deviate to remain clear"));
    }

    [Fact]
    public void FlightHoldsOutsideRestrictedAirportAndLandsWhenItReopens()
    {
        var airport = AirportAt(1, "UKBB", 120);
        var flight = Inbound(1, airport, 28, altitude: 8000);
        var zone = new WeatherZone { ZoneType = "MILITARY", CenterX = 120, CenterY = 100, Radius = 60 };
        using var engine = StartSector([airport], [flight], [zone]);

        engine.Advance(1700);
        Assert.Equal(FlightPhase.Holding, flight.Operations.Phase);
        Assert.NotNull(flight.Operations.HoldingPattern);
        var dx = flight.CoordX - zone.CenterX;
        var dy = flight.CoordY - zone.CenterY;
        Assert.True(dx * dx + dy * dy > zone.Radius * zone.Radius);

        zone.IsActive = false;
        engine.Advance(3600);
        Assert.Equal(FlightPhase.Landed, flight.Operations.Phase);
    }

    [Fact]
    public void OppositeCoursesMaintainSeparationWithControllerHeadings()
    {
        var airport = AirportAt(1, "UKBB", 800);
        var first = Inbound(1, airport, 40, altitude: 24000);
        var second = Inbound(2, airport, 63, altitude: 24000);
        first.GroundSpeed = second.GroundSpeed = 440;
        second.Heading = second.TargetHeading = 270;
        using var engine = StartSector([airport], [first, second]);
        engine.VectorAircraft(first, 90);
        engine.VectorAircraft(second, 270);
        int losses = 0;
        engine.ConflictDetected += _ => losses++;

        engine.Advance(220);

        Assert.True(first.CoordX > second.CoordX);
        Assert.Equal(0, losses);
        Assert.Equal(90, first.TargetHeading);
        Assert.Equal(270, second.TargetHeading);
        Assert.Contains(engine.Radio.Logs, log => log.Message.Contains("maintain separation"));
    }

    [Fact]
    public void NewlyActivatedZoneAllowsOutboundRecovery()
    {
        var airport = AirportAt(1, "UKBB", 220);
        var plane = Inbound(1, airport, 70, altitude: 10000);
        var zone = new WeatherZone { CenterX = 75, CenterY = 100, Radius = 15, ZoneType = "TURBULENCE", MaxAltitude = 20000 };
        using var engine = StartSector([airport], [plane], [zone]);

        engine.Advance(450);

        var dx = plane.CoordX - zone.CenterX;
        var dy = plane.CoordY - zone.CenterY;
        Assert.True(dx * dx + dy * dy > zone.Radius * zone.Radius);
    }

    [Fact]
    public void HoldingAtTheSameCeilingUsesDifferentCircuits()
    {
        var airport = AirportAt(1, "UKBB", 100);
        var first = Inbound(1, airport, 80, altitude: 5000);
        var second = Inbound(2, airport, 71, altitude: 5000);
        first.Model = second.Model = new AircraftModel { MaxAltitude = 5000 };
        var runwayOccupant = new Aircraft
        {
            Id = 3, IsActive = false, ArrivalAirportIcao = airport.IcaoCode,
            Operations = new() { Phase = FlightPhase.Landed, RunwayClearAt = DateTime.UnixEpoch.AddMinutes(3) }
        };
        using var engine = StartSector([airport], [first, second, runwayOccupant]);

        engine.Advance(1);

        Assert.Equal(5000, first.Operations.HoldingAltitude);
        Assert.Equal(5000, second.Operations.HoldingAltitude);
        var firstPattern = Assert.IsType<Vector2D[]>(first.Operations.HoldingPattern);
        var secondPattern = Assert.IsType<Vector2D[]>(second.Operations.HoldingPattern);
        Assert.All(firstPattern, a => Assert.All(secondPattern, b => Assert.True(a.DistanceTo(b) >= 6)));
    }

    [Fact]
    public void LandingRetainsTrailForFiveSimulationSeconds()
    {
        var airport = AirportAt(1, "UKBB", 100);
        var flight = Inbound(1, airport, 99.8m, altitude: 40);
        flight.Operations.Phase = FlightPhase.Final;
        flight.Operations.RunwayDesignator = "09";
        flight.Operations.RunwayHeading = 90;
        flight.TrailPoints.Add(new(94, 100, 1908));
        using var engine = StartSector([airport], [flight]);

        engine.Advance(1);
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(1), flight.Operations.LandedAt);
        Assert.Equal(new TrailPoint(100, 100, 0), flight.TrailPoints[^1]);

        engine.Advance(4);
        Assert.NotEmpty(flight.TrailPoints);
        engine.Advance(1);
        Assert.Empty(flight.TrailPoints);
        Assert.Equal(FlightPhase.Landed, flight.Operations.Phase);
        Assert.True(flight.Operations.RunwayClearAt > engine.SimTime);
    }

    [Fact]
    public void CruiseDescentStartsBeforeTheTerminalAreaAndJoinsFinalWithoutHolding()
    {
        var airport = AirportAt(1, "UKBB", 220);
        var flight = Inbound(1, airport, 100, altitude: 30000);
        flight.GroundSpeed = 450;
        using var engine = StartSector([airport], [flight]);

        engine.Advance(1);
        Assert.Equal(FlightPhase.EnRoute, flight.Operations.Phase);
        Assert.True(flight.Operations.ArrivalDescentStarted);
        Assert.InRange(flight.Altitude, 29000, 29999);

        engine.Advance(2400);
        Assert.Equal(FlightPhase.Landed, flight.Operations.Phase);
        Assert.DoesNotContain(engine.Radio.Logs, log =>
            log.Message.Contains("hold at fix") || log.Message.Contains("go around"));
    }

    [Fact]
    public void ParkedFlightRefuelsToCalculatedAmountBeforeTakeoff()
    {
        var departure = AirportAt(1, "UKLL", 40);
        var arrival = AirportAt(2, "UKBB", 320);
        var model = AircraftModelCatalog.CreateByIconType("A320")!;
        var aircraft = new Aircraft { Id = 1, Callsign = "FUEL01", Model = model };
        AircraftTracker.ScheduleDeparture(aircraft, departure, arrival, 22000, 400);
        using var engine = StartSector([departure, arrival], [aircraft]);
        decimal expected = AircraftPerformance.DepartureFuel(model, 280, 400, 0, 0, 90);

        engine.Advance(1);
        Assert.Equal(FlightPhase.Refueling, aircraft.Operations.Phase);
        Assert.InRange(aircraft.FuelRemaining, 1, expected - 1);
        Assert.False(aircraft.IsAirborne);
        Assert.Equal(expected, aircraft.Operations.DepartureFuelKg);

        engine.Advance(28);
        Assert.True(aircraft.IsAirborne);
        Assert.InRange(aircraft.FuelRemaining, expected - 25, expected);
        Assert.Single(engine.Radio.Logs, log => log.SenderType == "ATC" && log.Message.Contains("cleared for takeoff"));
    }

    [Fact]
    public void OccupiedStripBlocksItsReciprocalAndCrossingRunwayButLeavesParallelAvailable()
    {
        var departure = AirportAt(1, "UKLL", 40);
        departure.Runways =
        [
            new() { Designator = "09L", Angle = 90 }, new() { Designator = "27R", Angle = 270 },
            new() { Designator = "09R", Angle = 90 }, new() { Designator = "27L", Angle = 270 },
            new() { Designator = "18", Angle = 180 }
        ];
        var arrival = AirportAt(2, "UKBB", 320);
        var aircraft = new Aircraft { Id = 1, Callsign = "TST1", FuelRemaining = 10000 };
        AircraftTracker.ScheduleDeparture(aircraft, departure, arrival, 22000, 400);
        var occupant = new Aircraft
        {
            Id = 2, IsActive = false, ArrivalAirportIcao = departure.IcaoCode, Operations = new()
            {
                Phase = FlightPhase.Landed, RunwayDesignator = "27R", RunwayClearAt = DateTime.UnixEpoch.AddMinutes(2)
            }
        };
        using var engine = StartSector([departure, arrival], [aircraft, occupant]);

        Assert.True(RunwayLayout.SharesProtection(departure.Runways[0], departure.Runways[1]));
        Assert.True(RunwayLayout.SharesProtection(departure.Runways[0], departure.Runways[4]));
        Assert.False(RunwayLayout.SharesProtection(departure.Runways[0], departure.Runways[2]));

        engine.Advance(1);
        Assert.Equal("09R", aircraft.Operations.RunwayDesignator);
        Assert.Equal((decimal)RunwayLayout.Center(departure, departure.Runways[2]).Y, aircraft.CoordY);
    }

    private static SimulationEngine StartSector(List<Airport> airports, List<Aircraft> aircraft, List<WeatherZone>? zones = null)
    {
        var engine = new SimulationEngine();
        engine.Weather.CurrentWindSpeed = 0;
        engine.LoadSector(new WorldSector { Airports = airports, Aircraft = aircraft, WeatherZones = zones ?? [] }, DateTime.UnixEpoch);
        return engine;
    }

    private static Airport AirportAt(int id, string code, decimal x) => new()
    {
        Id = id, IcaoCode = code, CoordX = x, CoordY = 100,
        Runways = [new() { Angle = 90, Designator = "09" }, new() { Angle = 270, Designator = "27" }]
    };

    private static Aircraft Inbound(int id, Airport airport, decimal x, decimal altitude) => new()
    {
        Id = id, Callsign = $"TST{id}", CoordX = x, CoordY = airport.CoordY, GroundSpeed = 240,
        Heading = 90, TargetHeading = 90, Altitude = altitude, TargetAltitude = altitude, FuelRemaining = 10000,
        ArrivalAirportIcao = airport.IcaoCode, DestinationX = (double)airport.CoordX, DestinationY = (double)airport.CoordY
    };
}
