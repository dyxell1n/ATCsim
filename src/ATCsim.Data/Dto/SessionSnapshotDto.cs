using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;

namespace ATCsim.Data.Dto;

public sealed record SessionSnapshotDto(long WorldSeed, int TerrainVersion, DateTime SimTime,
    double TimeScale, List<Airport> Airports, List<WeatherZone> WeatherZones, List<Aircraft> Aircraft)
{
    public WorldSector RestoreMap(IWorldGenerator generator)
    {
        var sector = generator.GenerateSector(WorldSeed);
        if (TerrainVersion == WorldSector.LegacyTerrainVersion) generator.RestoreLegacyTerrain(sector);
        sector.Airports = Airports;
        sector.WeatherZones = WeatherZones;
        sector.Aircraft = Aircraft;
        return sector;
    }
}
