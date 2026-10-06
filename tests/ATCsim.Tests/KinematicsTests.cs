using ATCsim.Core.Kinematics;

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
        var weather = new ATCsim.Core.Weather.WeatherService();
        var aircraft = new ATCsim.Core.Entities.Aircraft
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
}
