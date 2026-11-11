using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;
using ATCsim.Core.Weather;

namespace ATCsim.Tests;

public class AircraftPerformanceTests
{
    [Fact]
    public void PropellerAircraft_ThreeMinuteFlight_PreservesFuelAtModelRate()
    {
        var model = AircraftModelCatalog.CreateByIconType("SR22")!;
        decimal initialFuel = model.FuelCapacity * 0.45m;
        var aircraft = new Aircraft { Model = model, FuelRemaining = initialFuel };
        var tracker = new AircraftTracker();
        var weather = new WeatherService();

        for (int second = 0; second < 180; second++)
            tracker.UpdateAircraftPosition(aircraft, weather, 1);

        decimal expectedBurn = 2700m * model.FuelCapacity / 20894m * 180m / 3600m;
        Assert.True(aircraft.FuelRemaining > 0);
        Assert.Equal(initialFuel - expectedBurn, aircraft.FuelRemaining, precision: 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceJetOrMissingModel_PreservesLegacyFuelBurn(bool hasModel)
    {
        var aircraft = new Aircraft
        {
            Model = hasModel ? AircraftModelCatalog.CreateByIconType("B738") : null,
            FuelRemaining = 1000m
        };

        new AircraftTracker().UpdateAircraftPosition(aircraft, new WeatherService(), 180);

        Assert.Equal(865m, aircraft.FuelRemaining);
        Assert.Equal(2700m, AircraftPerformance.FuelBurnPerHour(aircraft.Model));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MissingCapacity_UsesLegacyFuelBurn(int fuelCapacity)
    {
        var model = new AircraftModel { FuelCapacity = fuelCapacity };

        Assert.Equal(2700m, AircraftPerformance.FuelBurnPerHour(model));
    }

    [Fact]
    public void PropellerAircraft_ClimbCommand_CapsTargetAtReachableCeiling()
    {
        var aircraft = new Aircraft
        {
            Model = AircraftModelCatalog.CreateByIconType("SR22"),
            Altitude = 6000m,
            TargetAltitude = 32000m
        };

        new AircraftTracker().UpdateAircraftPosition(aircraft, new WeatherService(), 600);

        Assert.Equal(17500m, aircraft.TargetAltitude);
        Assert.Equal(17500m, aircraft.Altitude);
    }

    [Theory]
    [InlineData("SR22")]
    [InlineData("B738")]
    public void FuelBurn_ExceedsRemainingFuel_StopsAtZero(string iconType)
    {
        var aircraft = new Aircraft
        {
            Model = AircraftModelCatalog.CreateByIconType(iconType),
            FuelRemaining = 1m
        };
        var tracker = new AircraftTracker();
        var weather = new WeatherService();

        tracker.UpdateAircraftPosition(aircraft, weather, 3600);
        tracker.UpdateAircraftPosition(aircraft, weather, 3600);

        Assert.Equal(0m, aircraft.FuelRemaining);
    }
}
