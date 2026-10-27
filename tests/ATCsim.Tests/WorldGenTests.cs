using System.Text.Json;
using ATCsim.Core.CollisionDetection;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Tests;

public class WorldGenTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(84920417L)]
    [InlineData(-42L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void SameSeed_ReproducesCompleteInitialSector(long seed)
    {
        var generator = new WorldGenerator();
        var first = generator.GenerateSector(seed);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(generator.GenerateSector(seed)));
        generator.GenerateSector(123);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(generator.GenerateSector(seed)));
    }

    [Theory]
    [InlineData(12345L, 98765L)]
    [InlineData(0L, 4294967297L)] // These collided when folded into a 32-bit System.Random seed.
    [InlineData(-1L, 0L)]
    public void DifferentSeeds_ChangeTerrainAndAirportLayout(long firstSeed, long secondSeed)
    {
        var generator = new WorldGenerator();
        var first = generator.GenerateSector(firstSeed);
        var second = generator.GenerateSector(secondSeed);
        Assert.NotEqual(JsonSerializer.Serialize(first.Terrain), JsonSerializer.Serialize(second.Terrain));
        Assert.NotEqual(JsonSerializer.Serialize(first.Airports), JsonSerializer.Serialize(second.Airports));
    }

    [Fact]
    public void GeneratedSectors_HaveValidBoundsSeparationAndRoutes()
    {
        var generator = new WorldGenerator();
        for (long seed = -50; seed <= 50; seed++)
        {
            var sector = generator.GenerateSector(seed);
            Assert.Equal(5, sector.Airports.Count);
            Assert.Equal(sector.Airports.Count, sector.Airports.Select(a => a.IcaoCode).Distinct().Count());
            foreach (var airport in sector.Airports)
            {
                Assert.InRange(airport.CoordX, 0m, WorldGenerator.MapWidth);
                Assert.InRange(airport.CoordY, 0m, 420m); // Inland, north of minimum coastline Y=490.
                Assert.All(airport.Runways, runway =>
                {
                    Assert.Equal(airport.Id, runway.AirportId);
                    Assert.InRange(runway.Angle, 0, 359);
                    Assert.InRange(runway.Length, 2200m, 4000m);
                });
                foreach (var other in sector.Airports.Where(a => a.Id != airport.Id))
                {
                    double dx = (double)(airport.CoordX - other.CoordX);
                    double dy = (double)(airport.CoordY - other.CoordY);
                    Assert.True(Math.Sqrt(dx * dx + dy * dy) >= 110);
                }
            }
            foreach (var patch in sector.Terrain)
                Assert.All(patch.Points, p =>
                {
                    Assert.InRange(p.X, 0, WorldGenerator.MapWidth);
                    Assert.InRange(p.Y, 0, WorldGenerator.MapHeight);
                });
            foreach (var point in sector.Waypoints)
            {
                Assert.InRange(point.CoordX, 0m, WorldGenerator.MapWidth);
                Assert.InRange(point.CoordY, 0m, WorldGenerator.MapHeight);
            }
            foreach (var zone in sector.WeatherZones)
            {
                Assert.InRange(zone.CenterX - zone.Radius, 0m, WorldGenerator.MapWidth);
                Assert.InRange(zone.CenterX + zone.Radius, 0m, WorldGenerator.MapWidth);
                Assert.InRange(zone.CenterY - zone.Radius, 0m, WorldGenerator.MapHeight);
                Assert.InRange(zone.CenterY + zone.Radius, 0m, WorldGenerator.MapHeight);
            }
            foreach (var aircraft in sector.Aircraft)
            {
                var departure = Assert.Single(sector.Airports, a => a.IcaoCode == aircraft.DepartureAirportIcao);
                var arrival = Assert.Single(sector.Airports, a => a.IcaoCode == aircraft.ArrivalAirportIcao);
                Assert.NotEqual(departure.Id, arrival.Id);
                Assert.Equal((double)departure.CoordX, aircraft.OriginX);
                Assert.Equal((double)departure.CoordY, aircraft.OriginY);
                Assert.Equal((double)arrival.CoordX, aircraft.DestinationX);
                Assert.Equal((double)arrival.CoordY, aircraft.DestinationY);
                Assert.InRange(aircraft.CoordX, 0m, WorldGenerator.MapWidth);
                Assert.InRange(aircraft.CoordY, 0m, WorldGenerator.MapHeight);
                Assert.InRange(aircraft.Altitude, 0m, aircraft.Model!.MaxAltitude);
                Assert.InRange(aircraft.Heading, 0, 359.999999);
                Assert.InRange(aircraft.FuelRemaining, 0m, aircraft.Model.FuelCapacity);
            }
            Assert.Empty(new CollisionDetector().CheckSeparation(sector.Aircraft));
        }
    }
}
