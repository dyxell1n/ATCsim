using ATCsim.Core.CollisionDetection;
using ATCsim.Core.Entities;

namespace ATCsim.Tests;

public class CollisionTests
{
    [Fact]
    public void CheckSeparation_WhenWithin5NMAnd1000Ft_ShouldTriggerAlert()
    {
        // Arrange
        var detector = new CollisionDetector();
        var plane1 = new Aircraft
        {
            Id = 1,
            Callsign = "UKR101",
            CoordX = 100m,
            CoordY = 100m,
            Altitude = 25000m,
            IsActive = true
        };
        var plane2 = new Aircraft
        {
            Id = 2,
            Callsign = "UKR102",
            CoordX = 103m, // Distance = 4.24 NM (hypotenuse of 3 and 3)
            CoordY = 103m,
            Altitude = 25500m, // Vertical delta = 500 ft (< 1000 ft)
            IsActive = true
        };

        // Act
        var alerts = detector.CheckSeparation([plane1, plane2]);

        // Assert
        Assert.Single(alerts);
        Assert.Equal("UKR101", alerts[0].Aircraft1.Callsign);
        Assert.Equal("UKR102", alerts[0].Aircraft2.Callsign);
    }

    [Fact]
    public void CheckSeparation_WhenHorizontalSeparationGreater5NM_ShouldNotTriggerAlert()
    {
        // Arrange
        var detector = new CollisionDetector();
        var plane1 = new Aircraft
        {
            Id = 1,
            Callsign = "UKR101",
            CoordX = 100m,
            CoordY = 100m,
            Altitude = 25000m,
            IsActive = true
        };
        var plane2 = new Aircraft
        {
            Id = 2,
            Callsign = "UKR102",
            CoordX = 110m, // Distance = 10 NM
            CoordY = 100m,
            Altitude = 25200m,
            IsActive = true
        };

        // Act
        var alerts = detector.CheckSeparation([plane1, plane2]);

        // Assert
        Assert.Empty(alerts);
    }
}
