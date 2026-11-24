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

    [Fact]
    public void CanDepart_WaitsForAirborneTrafficButIgnoresParkedAircraft()
    {
        var detector = new CollisionDetector();
        var first = new Aircraft { CoordX = 100m, CoordY = 100m, PlannedDepartureSpeed = 300m };
        var second = new Aircraft { CoordX = 100m, CoordY = 100m, PlannedDepartureSpeed = 300m };
        Aircraft[] traffic = [first, second];

        Assert.True(detector.CanDepart(first, traffic));
        first.Altitude = 25m;
        Assert.Empty(detector.CheckSeparation(traffic));
        Assert.False(detector.CanDepart(second, traffic));

        first.Altitude = CollisionDetector.VerticalSeparationMinFt;
        Assert.True(detector.CanDepart(second, traffic));
    }
    [Theory]
    [InlineData(32)]
    [InlineData(512)]
    public void SweepMatchesAllPairsInOrderIncludingBoundaryCases(int count)
    {
        var random = new Random(8124);
        var traffic = Enumerable.Range(0, count).Select(i => new Aircraft
        {
            Id = i, CoordX = random.Next(-40, 41), CoordY = random.Next(-40, 41),
            Altitude = random.Next(1, 5) * 500, IsActive = i % 9 != 0
        }).ToList();
        traffic.AddRange([new() { Id = count, Altitude = 20000, CoordX = -5 },
            new() { Id = count + 1, Altitude = 20000 }, new() { Id = count + 2, Altitude = 20000, CoordX = 4.99m }]);

        var expected = (from a in traffic from b in traffic
            where a.Id < b.Id && a.IsAirborne && b.IsAirborne && Math.Abs(a.Altitude - b.Altitude) < 1000
            let dx = a.CoordX - b.CoordX let dy = a.CoordY - b.CoordY
            where dx * dx + dy * dy < 25 select (a.Id, b.Id)).ToList();
        var detector = new CollisionDetector();
        var emitted = new List<CollisionAlert>();
        detector.SeparationAlertTriggered += emitted.Add;
        var alerts = detector.CheckSeparation(traffic);
        Assert.Equal(expected, alerts.Select(a => (a.Aircraft1.Id, a.Aircraft2.Id)));
        Assert.Equal(alerts, emitted);
    }

    [Fact]
    public void ConflictIsReportedOnceUntilSeparationRecovers()
    {
        var a = new Aircraft { Id = 1, Altitude = 30000 };
        var b = new Aircraft { Id = 2, Altitude = 30000 };
        using var engine = new ATCsim.Core.Services.SimulationEngine();
        engine.ReplaceAircraft([a, b]);
        int warnings = 0;
        engine.ConflictDetected += _ => warnings++;
        engine.Advance(1);
        engine.Advance(1);
        Assert.Equal(1, warnings);
        Assert.True(engine.ConflictingAircraft.SetEquals([1, 2]));
        b.CoordX = 20;
        engine.Advance(1);
        Assert.Empty(engine.ConflictingAircraft);
        b.CoordX = 0;
        engine.Advance(1);
        Assert.Equal(2, warnings);
    }
}
