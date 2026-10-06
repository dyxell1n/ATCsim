namespace ATCsim.Core.WorldGeneration;

/// <summary>
/// Interface for seed-driven sector generation.
/// Implemented as a blank/baseline stub for MVP, to be completed in Stage 6 by M. Borkov.
/// </summary>
public interface IWorldGenerator
{
    WorldSector GenerateSector(long seed);
}
