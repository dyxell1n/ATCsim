using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Context;
using ATCsim.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace ATCsim.Tests;

public sealed class AircraftModelPersistenceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"atcsim_models_{Guid.NewGuid():N}.db");
    private readonly RepositoryManager _repository;

    public AircraftModelPersistenceTests()
    {
        _repository = new RepositoryManager(new DatabaseContext(_databasePath));
    }

    [Theory]
    [InlineData("IL76", "CARGO")]
    [InlineData("GLEX", "BUSINESS")]
    [InlineData("SR22", "LIGHT")]
    public void SavedSession_RestoresAircraftModelIdentityAndCapabilities(string iconType, string category)
    {
        var model = AircraftModelCatalog.CreateByIconType(iconType)!;
        var aircraft = CreateAircraft(41, "MODEL41", model);
        aircraft.GroundSpeed = 163m;
        aircraft.Altitude = 12500m;
        aircraft.FuelRemaining = 123m;

        _repository.SaveSessionProgress(12345, DateTime.UnixEpoch, 1, [], [aircraft], []);
        var saved = _repository.LoadSavedSession();

        Assert.True(saved.HasValue);
        var restored = Assert.Single(saved.Value.Aircraft);
        Assert.NotNull(restored.Model);
        Assert.Equal(model.Id, restored.ModelId);
        Assert.Equal(model.Id, restored.Model.Id);
        Assert.NotEqual(restored.Id, restored.Model.Id);
        Assert.Equal(iconType, restored.Model.IconType);
        Assert.Equal(model.ModelName, restored.Model.ModelName);
        Assert.Equal(category, restored.Model.Category);
        Assert.Equal(model.CruiseSpeed, restored.Model.CruiseSpeed);
        Assert.Equal(model.MaxAltitude, restored.Model.MaxAltitude);
        Assert.Equal(model.FuelCapacity, restored.Model.FuelCapacity);
        Assert.Equal(aircraft.GroundSpeed, restored.GroundSpeed);
        Assert.Equal(aircraft.Altitude, restored.Altitude);
        Assert.Equal(aircraft.FuelRemaining, restored.FuelRemaining);
    }

    [Fact]
    public void SavedSession_PreservesUnrecognizedLegacyModelNameAndIcon()
    {
        var model = new AircraftModel { Id = 99, ModelName = "Legacy aircraft", IconType = "CUSTOM" };
        var aircraft = CreateAircraft(41, "LEGACY41", model);

        _repository.SaveSessionProgress(12345, DateTime.UnixEpoch, 1, [], [aircraft], []);
        var saved = _repository.LoadSavedSession();

        Assert.True(saved.HasValue);
        var restored = Assert.Single(saved.Value.Aircraft);
        Assert.NotNull(restored.Model);
        Assert.Equal(model.ModelName, restored.Model.ModelName);
        Assert.Equal(model.IconType, restored.Model.IconType);
    }

    [Fact]
    public void SectorSynchronization_UpdatesModelAndCallsignForExistingAircraft()
    {
        var passenger = AircraftModelCatalog.CreateByIconType("B738")!;
        var cargo = AircraftModelCatalog.CreateByIconType("IL76")!;
        _repository.SyncSectorToDatabase(new WorldSector
        {
            Aircraft = [CreateAircraft(1, "PREVIOUS1", passenger)]
        });

        _repository.SyncSectorToDatabase(new WorldSector
        {
            Aircraft = [CreateAircraft(1, "CARGO1", cargo)]
        });

        var restored = Assert.Single(_repository.Aircraft.GetAll(), aircraft => aircraft.Id == 1);
        Assert.Equal("CARGO1", restored.Callsign);
        Assert.Equal(cargo.Id, restored.ModelId);
        Assert.NotNull(restored.Model);
        Assert.Equal("IL76", restored.Model.IconType);
        Assert.Equal("CARGO", restored.Model.Category);
    }

    private static Aircraft CreateAircraft(int id, string callsign, AircraftModel model) => new()
    {
        Id = id,
        Callsign = callsign,
        ModelId = model.Id,
        Model = model,
        CoordX = 300,
        CoordY = 250,
        Altitude = 10000,
        TargetAltitude = 10000,
        GroundSpeed = model.CruiseSpeed,
        FuelRemaining = model.FuelCapacity / 2,
        TrailPoints = [new(300, 250, 10000)]
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
