namespace ATCsim.Core.WorldGeneration;

/// <summary>
/// Interface for seed-driven sector generation.
/// Identical seeds reproduce the complete initial sector, including terrain.
/// </summary>
public interface IWorldGenerator
{
    WorldSector GenerateSector(long seed);
}

