using ATCsim.Core.WorldGeneration;

namespace ATCsim.Tests;

public class WorldGenTests
{
    [Fact]
    public void GenerateSector_WithSameSeed_ShouldProduceDeterministicResults()
    {
        // Arrange
        var generator = new WorldGenerator();
        long testSeed = 84920417;

        // Act
        var sector1 = generator.GenerateSector(testSeed);
        var sector2 = generator.GenerateSector(testSeed);

        // Assert
        Assert.Equal(sector1.Airports.Count, sector2.Airports.Count);
        Assert.Equal(sector1.Aircraft.Count, sector2.Aircraft.Count);
        for (int i = 0; i < sector1.Airports.Count; i++)
        {
            Assert.Equal(sector1.Airports[i].IcaoCode, sector2.Airports[i].IcaoCode);
            Assert.Equal(sector1.Airports[i].CoordX, sector2.Airports[i].CoordX);
            Assert.Equal(sector1.Airports[i].CoordY, sector2.Airports[i].CoordY);
        }
    }
}
