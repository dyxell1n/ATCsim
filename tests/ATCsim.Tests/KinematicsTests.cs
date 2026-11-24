using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Services;
using ATCsim.Core.Weather;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Tests;

public class KinematicsTests
{
    [Fact]
    public void CalculateNextPosition_PureNorthHeading_DecreasesY()
    {
        // Heading 0 degrees = North -> X unchanged, Y decreases (upwards on radar screen)
        double startX = 100.0;
        double startY = 100.0;
        double groundSpeed = 360.0; // 360 kts = 0.1 NM/sec
        double heading = 0.0;
        double dt = 10.0; // 10 seconds => -1.0 NM in Y

        (double newX, double newY) = KinematicsCalculator.CalculateNextPosition(
            startX, startY, groundSpeed, heading, windSpeedKnots: 0, windDirectionDegrees: 0, dtSeconds: dt);

        Assert.Equal(startX, newX, precision: 4);
        Assert.Equal(startY - 1.0, newY, precision: 4);
    }

    [Fact]
    public void CalculateBearing_PointsDueEast_Returns90Degrees()
    {
        double bearing = KinematicsCalculator.CalculateBearing(100, 100, 200, 100);
        Assert.Equal(90.0, bearing, precision: 1);
    }

    [Fact]
    public void UpdateHeading_StandardTurnRate_MovesTowardsTarget()
    {
        double currentHeading = 90.0;
        double targetHeading = 120.0;
        double dt = 5.0; // at 3 deg/sec, 5s should advance 15 degrees to 105

        double updated = KinematicsCalculator.UpdateHeading(currentHeading, targetHeading, dt, turnRateDegPerSec: 3.0);

        Assert.Equal(105.0, updated, precision: 1);
    }

    [Fact]
    public void UpdateAltitude_StandardClimb_ClimbsTowardsTarget()
    {
        decimal currentAlt = 10000m;
        decimal targetAlt = 20000m;
        double dt = 60.0; // 60 seconds at 1500 fpm => +1500 ft

        decimal updated = KinematicsCalculator.UpdateAltitude(currentAlt, targetAlt, dt, fpm: 1500m);

        Assert.Equal(11500m, updated);
    }

    [Fact]
    public void AircraftTracker_NavigatesTowardsDestination_AndRecordsTrail()
    {
        var tracker = new AircraftTracker();
        var weather = new WeatherService();
        var aircraft = new Aircraft
        {
            CoordX = 100m,
            CoordY = 100m,
            DestinationX = 200.0,
            DestinationY = 100.0, // Due East
            Heading = 0,
            GroundSpeed = 360m,
            Altitude = 28000m
        };

        // Advance 2 seconds
        tracker.UpdateAircraftPosition(aircraft, weather, 1.0);
        tracker.UpdateAircraftPosition(aircraft, weather, 1.0);

        // Heading should steer towards 90 deg (East)
        Assert.True(aircraft.Heading > 0, "Aircraft heading should steer towards East destination");
        Assert.NotEmpty(aircraft.TrailPoints);
    }

    [Fact]
    public void ScheduledAircraft_StartsAtAirportAndAppearsWhenDeparting()
    {
        var tracker = new AircraftTracker();
        var weather = new WeatherService { CurrentWindSpeed = 0 };
        var airport = new Airport { IcaoCode = "UKLL", CoordX = 250.125m, CoordY = 350.25m };
        var destination = new Airport { IcaoCode = "UKBB", CoordX = 700m, CoordY = 350.25m };
        var aircraft = new Aircraft();
        AircraftTracker.ScheduleDeparture(aircraft, airport, destination, targetAltitude: 24000m, departureSpeed: 360m);

        tracker.UpdateAircraftPosition(aircraft, weather, 0);
        Assert.False(aircraft.IsAirborne);
        Assert.Equal(airport.CoordX, aircraft.CoordX);
        Assert.Equal(airport.CoordY, aircraft.CoordY);
        Assert.Equal(0m, aircraft.Altitude);
        Assert.Equal(0m, aircraft.GroundSpeed);

        tracker.UpdateAircraftPosition(aircraft, weather, 10);
        Assert.True(aircraft.IsAirborne);
        Assert.Equal(airport.CoordX + 1m, aircraft.CoordX);
        Assert.Equal(airport.CoordY, aircraft.CoordY);
        Assert.Equal(250m, aircraft.Altitude);
        Assert.Equal(360m, aircraft.GroundSpeed);
        Assert.Null(aircraft.PlannedDepartureSpeed);
        Assert.Equal(new TrailPoint((double)airport.CoordX, (double)airport.CoordY, 0), aircraft.TrailPoints[0]);
    }

    [Fact]
    public void MovingAirport_KeepsScheduledAircraftAtDeparture()
    {
        using var engine = new SimulationEngine();
        var airport = new Airport { Id = 1, IcaoCode = "UKLL", CoordX = 250m, CoordY = 350m };
        var destination = new Airport { Id = 2, IcaoCode = "UKBB", CoordX = 700m, CoordY = 350m };
        var aircraft = new Aircraft();
        AircraftTracker.ScheduleDeparture(aircraft, airport, destination, targetAltitude: 24000m, departureSpeed: 360m);
        engine.LoadSector(new WorldSector { Airports = [airport, destination], Aircraft = [aircraft] });

        engine.UpdateAirport(new Airport { Id = airport.Id, IcaoCode = airport.IcaoCode, CoordX = 250m, CoordY = 500m });

        Assert.False(aircraft.IsAirborne);
        Assert.Equal(250m, aircraft.CoordX);
        Assert.Equal(500m, aircraft.CoordY);
        Assert.Equal(new TrailPoint(250, 500, 0), Assert.Single(aircraft.TrailPoints));
        Assert.Equal(360m, aircraft.PlannedDepartureSpeed);
    }
    [Theory]
    [InlineData(1, 100)]
    [InlineData(8, 800)]
    [InlineData(16, 1600)]
    public async Task LiveTickAdvancesByScaledTimerInterval(double scale, int expectedMilliseconds)
    {
        using var engine = new ATCsim.Core.Services.SimulationEngine();
        engine.LoadSector(new(), DateTime.UnixEpoch);
        engine.SetTimeScale(scale);
        var firstTick = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.StateUpdated += () =>
        {
            var elapsed = engine.SimTime - DateTime.UnixEpoch;
            if (elapsed > TimeSpan.Zero) firstTick.TrySetResult(elapsed);
        };
        engine.Start();
        var actual = await firstTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
        engine.Pause();
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), actual);
    }
}
