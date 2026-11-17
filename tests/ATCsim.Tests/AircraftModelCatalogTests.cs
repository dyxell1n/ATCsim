using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Tests;

public class AircraftModelCatalogTests
{
    [Fact]
    public void Catalog_KeepsStableDatabaseIdsAndCreatesIndependentModels()
    {
        string[] expectedIcons = ["B738", "A320", "E195", "A321", "IL76", "GLEX", "SR22"];
        Assert.Equal(expectedIcons, AircraftModelCatalog.All.Select(model => model.IconType));
        Assert.Equal(Enumerable.Range(1, expectedIcons.Length), AircraftModelCatalog.All.Select(model => model.Id));

        foreach (var definition in AircraftModelCatalog.All)
        {
            var model = AircraftModelCatalog.CreateByIconType(definition.IconType.ToLowerInvariant());
            Assert.NotNull(model);
            Assert.Equal(definition.Id, model.Id);
            Assert.Equal(definition.Category, model.Category);
            Assert.InRange(definition.DefaultAltitude, 1m, model.MaxAltitude);
            Assert.True(model.CruiseSpeed > 0);
            Assert.True(model.FuelCapacity > 0);
            model.ModelName = "Changed flight model";
            Assert.Equal(definition.ModelName, AircraftModelCatalog.CreateByIconType(definition.IconType)!.ModelName);
        }

        Assert.Null(AircraftModelCatalog.CreateByIconType("UNKNOWN"));
        Assert.Null(AircraftModelCatalog.CreateByIconType(null));
    }

    [Fact]
    public void GeneratedTraffic_ContainsRequestedTypesWithAppropriateLightAircraftDefaults()
    {
        var sector = new WorldGenerator().GenerateSector(84920417);
        Assert.Equal(new[] { "B738", "A320", "E195", "A321", "IL76", "GLEX", "SR22", "B738" },
            sector.Aircraft.Select(aircraft => aircraft.Model!.IconType));
        var light = Assert.Single(sector.Aircraft, aircraft => aircraft.Model!.Category == "LIGHT");
        Assert.InRange(light.TargetAltitude, 6000m, 7000m);
        Assert.InRange(Assert.IsType<decimal>(light.PlannedDepartureSpeed), 130m, 175m);
        Assert.InRange(light.FuelRemaining, 1m, light.Model!.FuelCapacity);
        Assert.Contains(sector.Aircraft, aircraft => aircraft.Model!.Category == "CARGO");
        Assert.Contains(sector.Aircraft, aircraft => aircraft.Model!.Category == "BUSINESS");
    }

}
