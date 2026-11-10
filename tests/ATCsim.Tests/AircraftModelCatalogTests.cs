using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        Assert.InRange(light.Altitude, 6000m, 7000m);
        Assert.InRange(light.GroundSpeed, 130m, 175m);
        Assert.InRange(light.FuelRemaining, 1m, light.Model!.FuelCapacity);
        Assert.Contains(sector.Aircraft, aircraft => aircraft.Model!.Category == "CARGO");
        Assert.Contains(sector.Aircraft, aircraft => aircraft.Model!.Category == "BUSINESS");
    }

    [Theory]
    [InlineData(0L, "4A5640245608CFC0FE59C968AAC1839CB9E45DB318F8C72985602DA0811B2B71")]
    [InlineData(84920417L, "F5A3DD62B76EDB09354A6FF3D2FFB8D495D527F46FB6E1C5BD180B3AA6727A9E")]
    [InlineData(-42L, "D021C60EE2B8B3BA686B5BDA3EBCD6D35AE5B9F83745C0588B3599EE70365606")]
    public void ExpandedAircraftCatalog_PreservesPreviouslyGeneratedMapAndRoutes(long seed, string expectedHash)
    {
        var sector = new WorldGenerator().GenerateSector(seed);
        // Captured from the generator before introducing additional aircraft models.
        // Excluding model-dependent performance makes this guard focus on PRNG draw order.
        var geometry = new
        {
            sector.Terrain, sector.Airports, sector.Waypoints, sector.WeatherZones,
            Routes = sector.Aircraft.Select(aircraft => new
            {
                aircraft.Id, aircraft.Callsign, aircraft.CoordX, aircraft.CoordY, aircraft.Heading,
                aircraft.DepartureAirportIcao, aircraft.ArrivalAirportIcao,
                aircraft.OriginX, aircraft.OriginY, aircraft.DestinationX, aircraft.DestinationY
            })
        };
        string actualHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(geometry))));
        Assert.Equal(expectedHash, actualHash);
    }
}
