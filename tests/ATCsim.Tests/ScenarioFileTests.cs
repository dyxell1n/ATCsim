using System.Text.Json;
using ATCsim.Core.Entities;
using ATCsim.Core.Services;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data;
using ATCsim.Data.Context;
using ATCsim.Data.Dto;
using ATCsim.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ATCsim.Tests;

public sealed class ScenarioFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("atcsim-transfer-").FullName;
    private string SavePath => Path.Combine(_directory, "sector.atcsave");
    private string PresetPath => Path.Combine(_directory, "traffic.atcpreset");

    [Theory]
    [InlineData(WorldSector.CurrentTerrainVersion)]
    [InlineData(WorldSector.LegacyTerrainVersion)]
    public void SaveRestoresMapAndSimulation(int version)
    {
        var generator = new WorldGenerator();
        var sector = generator.GenerateSector(-165);
        if (version == WorldSector.LegacyTerrainVersion) generator.RestoreLegacyTerrain(sector);
        sector.Airports[0].Name = "Edited airport";
        sector.Airports[0].Runways[0].IsAvailable = false;
        sector.Aircraft[0].Operations.HoldingPattern = [new(55, 70), new(70, 50)];
        sector.Aircraft[0].TrailPoints.Add(new(85.5, 112.5, 3500));
        sector.WeatherZones.Clear();
        var snapshot = new SessionSnapshotDto(sector.Seed, version, DateTime.UnixEpoch, SimulationEngine.MaxTimeScale,
            sector.Airports, sector.WeatherZones, sector.Aircraft);
        ScenarioFile.WriteSave(SavePath, snapshot);
        var saved = ScenarioFile.ReadSave(SavePath);
        Assert.Equal(snapshot.SimTime, saved.SimTime);
        Assert.Equal(SimulationEngine.MaxTimeScale, saved.TimeScale);
        Assert.Equal(JsonSerializer.Serialize(sector), JsonSerializer.Serialize(saved.RestoreMap(generator)));
    }

    [Fact]
    public void PresetReusesAirportLocationsAndStartsWithFreshTrails()
    {
        var generator = new WorldGenerator();
        var source = generator.GenerateSector(11);
        var first = source.Aircraft[0];
        first.TrailPoints.Add(new(50, 70, 5000));
        ScenarioFile.WritePreset(PresetPath, new(source.Aircraft.Select(PresetAircraft.FromAircraft).ToList()));
        var target = generator.GenerateSector(22);
        using var engine = new SimulationEngine();
        engine.LoadSector(target, DateTime.UnixEpoch);
        engine.SetTimeScale(8);
        var airports = engine.Airports;
        var zones = engine.WeatherZones;
        engine.ReplaceAircraft(ScenarioFile.ReadPreset(PresetPath).CreateTraffic(airports));
        Assert.Same(airports, engine.Airports);
        Assert.Same(zones, engine.WeatherZones);
        Assert.Equal(22, engine.CurrentSeed);
        Assert.Equal(DateTime.UnixEpoch, engine.SimTime);
        Assert.Equal(8, engine.TimeScale);
        var restored = engine.ActiveAircraft[0];
        var departure = airports.Single(a => a.IcaoCode == first.DepartureAirportIcao);
        Assert.Equal(departure.CoordX, restored.CoordX);
        Assert.Equal(departure.CoordY, restored.CoordY);
        Assert.Equal(first.PlannedDepartureSpeed, restored.PlannedDepartureSpeed);
        Assert.Single(restored.TrailPoints);
        Assert.DoesNotContain("worldSeed", File.ReadAllText(PresetPath));
    }

    [Fact]
    public void DensePresetRunsAndCanBeImportedTwiceIntoExistingDatabase()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Dense-256.atcpreset");
        var map = new WorldGenerator().GenerateSector(84920417);
        var repo = new RepositoryManager(new DatabaseContext(Path.Combine(_directory, "test.db")));
        repo.ImportSector(map);
        var preset = ScenarioFile.ReadPreset(file);
        var traffic = preset.CreateTraffic(map.Airports);
        repo.ImportSector(new WorldSector { Aircraft = traffic });
        var ids = traffic.Select(a => a.Id).ToArray();
        map.Aircraft = preset.CreateTraffic(map.Airports);
        repo.ImportSector(map);
        Assert.Equal(ids, map.Aircraft.Select(a => a.Id));
        Assert.Equal(256, ids.Distinct().Count());
        Assert.Single(traffic, plane => plane.Model?.IconType == "A225");
        Assert.All(traffic, plane => Assert.Contains(plane.Model?.IconType, new[] { "B738", "A320", "A321", "A225" }));
        using var engine = new SimulationEngine();
        engine.LoadSector(map);
        var start = engine.ActiveAircraft.Select(a => a.CoordX).ToArray();
        engine.Advance(8);
        Assert.Equal(256, engine.ActiveAircraft.Count(a => a.IsAirborne));
        Assert.NotEqual(start, engine.ActiveAircraft.Select(a => a.CoordX).ToArray());
        var radio = engine.Radio.Logs.Where(l => l.AirportId > 0).ToArray();
        repo.Logs.AddBatch(radio, []);
        repo.SaveSessionProgress(map.Seed, engine.SimTime, 8, map.Airports, map.Aircraft, map.WeatherZones);
        var saved = repo.LoadSavedSession();
        Assert.Equal(256, saved!.Value.Aircraft.Count);
        Assert.Equal(map.Airports.Count, saved.Value.Airports!.Count);
    }

    [Fact]
    public void BundledSaveStartsWithEightFullAirportsAndDepartsFromEach()
    {
        var snapshot = ScenarioFile.ReadSave(Path.Combine(AppContext.BaseDirectory, "Eight-Airports.atcsave"));
        var map = snapshot.RestoreMap(new WorldGenerator());
        Assert.Equal(8, map.Airports.Count);
        Assert.Equal(96, map.Aircraft.Count);
        Assert.All(map.Airports, airport => Assert.Equal(airport.MaxCapacity,
            map.Aircraft.Count(aircraft => aircraft.DepartureAirportIcao == airport.IcaoCode)));
        Assert.All(map.Aircraft, aircraft => Assert.False(aircraft.IsAirborne));
        Assert.Single(map.Aircraft, aircraft => aircraft.Model?.IconType == "A225");
        Assert.Contains(map.Airports, airport => airport.Runways.Count == 6);

        using var engine = new SimulationEngine();
        engine.LoadSector(map, snapshot.SimTime);
        engine.Advance(1);
        Assert.All(map.Aircraft, aircraft => Assert.Equal(FlightPhase.Refueling, aircraft.Operations.Phase));
        engine.Advance(120);
        Assert.Equal(8, map.Aircraft.Where(aircraft => aircraft.IsAirborne)
            .Select(aircraft => aircraft.DepartureAirportIcao).Distinct().Count());
    }

    [Fact]
    public void MissingRoutesAndWrongFormatsAreRejected()
    {
        var preset = ScenarioFile.ReadPreset(Path.Combine(AppContext.BaseDirectory, "Dense-256.atcpreset"));
        Assert.Throws<InvalidDataException>(() => preset.CreateTraffic([]));
        File.WriteAllText(SavePath, """{"kind":"atcsim.preset","version":1,"data":{"aircraft":[]}}""");
        Assert.Throws<InvalidDataException>(() => ScenarioFile.ReadSave(SavePath));
        File.WriteAllText(PresetPath, """{"kind":"atcsim.preset","version":2,"data":{"aircraft":[]}}""");
        Assert.Throws<InvalidDataException>(() => ScenarioFile.ReadPreset(PresetPath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
