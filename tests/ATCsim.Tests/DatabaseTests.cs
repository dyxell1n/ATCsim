using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Context;
using ATCsim.Data.Repositories;

namespace ATCsim.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _tempDbFile;
    private readonly RepositoryManager _repoManager;

    public DatabaseTests()
    {
        _tempDbFile = Path.Combine(Path.GetTempPath(), $"atcsim_test_{Guid.NewGuid()}.db");
        var context = new DatabaseContext(_tempDbFile);
        _repoManager = new RepositoryManager(context);
    }

    [Fact]
    public void DatabaseInitialization_ShouldSeedAirportsAndAircraft()
    {
        // Act
        var airports = _repoManager.Airports.GetAll();
        var aircraft = _repoManager.Aircraft.GetAll();
        var flights = _repoManager.Flights.GetAll();

        // Assert
        Assert.NotEmpty(airports);
        Assert.Contains(airports, a => a.IcaoCode == "UKLL");
        Assert.Contains(airports, a => a.IcaoCode == "UKBB");

        Assert.NotEmpty(aircraft);
        Assert.Contains(aircraft, a => a.Callsign == "UKR102");

        Assert.NotEmpty(flights);
        Assert.Contains(flights, f => f.FlightNumber == "UK102");
    }

    [Fact]
    public void CommunicationLogs_CanAddAndRetrieve()
    {
        // Arrange
        var log = new CommunicationLog
        {
            AirportId = 1,
            AircraftId = 1,
            SenderType = "ATC",
            Message = "UKR102, turn right heading 090.",
            Timestamp = DateTime.UtcNow
        };

        // Act
        _repoManager.Logs.AddCommunicationLog(log);
        var retrieved = _repoManager.Logs.GetCommunicationLogs(10);

        // Assert
        Assert.Contains(retrieved, l => l.Message.Contains("turn right heading 090"));
    }

    [Fact]
    public void SessionPersistence_CanSaveAndRestoreSession()
    {
        // Arrange
        var testPlanes = new List<Aircraft>
        {
            new()
            {
                Id = 1,
                Callsign = "TST999",
                CoordX = 350m,
                CoordY = 250m,
                Altitude = 31000m,
                Heading = 85,
                GroundSpeed = 440m,
                DepartureAirportIcao = "UKLL",
                ArrivalAirportIcao = "UKBB",
                TrailPoints = [new(100.0, 100.0, 31000m), new(350.0, 250.0, 31000m)]
            }
        };

        // Act
        _repoManager.SaveSessionProgress(99999, DateTime.UtcNow, 2.0, [], testPlanes, []);

        // Assert
        Assert.True(_repoManager.HasSavedSession());
        var restored = _repoManager.LoadSavedSession();
        Assert.NotNull(restored);
        Assert.Equal(99999, restored.Value.Seed);
        Assert.Equal(2.0, restored.Value.TimeScale);
        Assert.Equal(WorldSector.CurrentTerrainVersion, restored.Value.TerrainVersion);
        Assert.Single(restored.Value.Aircraft);
        Assert.Equal("TST999", restored.Value.Aircraft[0].Callsign);
        Assert.Equal(2, restored.Value.Aircraft[0].TrailPoints.Count);
    }

    [Fact]
    public void SessionPersistence_RestoresHoldingPatternRunwayTimerAndAirspace()
    {
        var time = DateTime.UnixEpoch.AddMinutes(5);
        var holding = new Aircraft
        {
            Id = 1, Callsign = "HOLD1", Altitude = 5000, GroundSpeed = 210,
            Operations = new FlightOperationsState
            {
                Phase = FlightPhase.Holding, ManualHold = true, HoldingLeg = 2, HoldingAltitude = 5000,
                HoldingPattern = [new(10, 20), new(15, 20), new(15, 30), new(10, 30)], ArrivalQueuedAt = time
            }
        };
        var landed = new Aircraft
        {
            Id = 2, Callsign = "LAND2", IsActive = false,
            Operations = new FlightOperationsState { Phase = FlightPhase.Landed, RunwayClearAt = time.AddSeconds(105) }
        };
        var restricted = new WeatherZone { Id = 4, Name = "Custom restricted area", ZoneType = "RESTRICTED", Radius = 35 };
        _repoManager.SaveSessionProgress(84920417, time, 4, [], [holding, landed], [restricted]);

        var restored = _repoManager.LoadSavedSession();
        Assert.NotNull(restored);
        Assert.Equal(time, restored.Value.SimTime);
        Assert.Equal(DateTimeKind.Utc, restored.Value.SimTime.Kind);
        var planes = restored.Value.Aircraft;
        Assert.True(planes[0].Operations.ManualHold);
        Assert.Equal(2, planes[0].Operations.HoldingLeg);
        Assert.Equal(holding.Operations.HoldingPattern, planes[0].Operations.HoldingPattern);
        Assert.Equal(FlightPhase.Landed, planes[1].Operations.Phase);
        Assert.Equal(landed.Operations.RunwayClearAt, planes[1].Operations.RunwayClearAt);
        Assert.False(planes[1].IsActive);
        Assert.True(Assert.Single(restored.Value.WeatherZones!).BlocksFlight);
    }

    [Fact]
    public void SessionPersistence_MigratesOlderSaveWithLegacyTerrainVersion()
    {
        _repoManager.SaveSessionProgress(84920417, DateTime.UnixEpoch, 1.0, [], [], []);
        using (var connection = _repoManager.Context.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "ALTER TABLE sim_session DROP COLUMN terrain_version;";
            command.ExecuteNonQuery();
        }

        var reopened = new RepositoryManager(_repoManager.Context);
        var restored = reopened.LoadSavedSession();
        Assert.NotNull(restored);
        Assert.Equal(84920417, restored.Value.Seed);
        Assert.Equal(WorldSector.LegacyTerrainVersion, restored.Value.TerrainVersion);
    }

    [Fact]
    public void Airports_CanAddAndUpdateAirport()
    {
        var apt = new Airport
        {
            Id = 99,
            IcaoCode = "UK99",
            Name = "Test Aerodrome",
            CoordX = 500m,
            CoordY = 500m,
            MaxCapacity = 10,
            CreatedAt = DateTime.UtcNow,
            Runways = [new Runway { Id = 99, AirportId = 99, Designator = "12", Angle = 120, Length = 2200m }]
        };

        _repoManager.Airports.AddAirport(apt);
        apt.Name = "Renamed Aerodrome";
        _repoManager.Airports.UpdateAirport(apt);

        var all = _repoManager.Airports.GetAll();
        var found = all.FirstOrDefault(a => a.IcaoCode == "UK99");
        Assert.NotNull(found);
        Assert.Equal("Renamed Aerodrome", found.Name);
    }

    [Fact]
    public void RegeneratingSector_UpdatesAirportRunwayAndWeatherGeometry()
    {
        var generator = new ATCsim.Core.WorldGeneration.WorldGenerator();
        _repoManager.SyncSectorToDatabase(generator.GenerateSector(12345));
        var expected = generator.GenerateSector(98765);
        _repoManager.SyncSectorToDatabase(expected);
        var actualAirports = _repoManager.Airports.GetAll();
        foreach (var airport in expected.Airports)
        {
            var actual = Assert.Single(actualAirports, a => a.Id == airport.Id);
            Assert.Equal(airport.CoordX, actual.CoordX);
            Assert.Equal(airport.CoordY, actual.CoordY);
            Assert.Equal(airport.MaxCapacity, actual.MaxCapacity);
            Assert.Equal(airport.Runways.Count, actual.Runways.Count);
            foreach (var runway in airport.Runways)
            {
                var stored = Assert.Single(actual.Runways, r => r.Id == runway.Id);
                Assert.Equal(runway.Angle, stored.Angle);
                Assert.Equal(runway.Designator, stored.Designator);
                Assert.Equal(runway.Length, stored.Length);
            }
        }
        using var connection = _repoManager.Context.CreateConnection();
        foreach (var zone in expected.WeatherZones)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT center_x, center_y, radius, max_altitude, zone_type FROM weather_zones WHERE id = @id";
            command.Parameters.AddWithValue("@id", zone.Id);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal((double)zone.CenterX, reader.GetDouble(0));
            Assert.Equal((double)zone.CenterY, reader.GetDouble(1));
            Assert.Equal((double)zone.Radius, reader.GetDouble(2));
            Assert.Equal((double)zone.MaxAltitude!.Value, reader.GetDouble(3));
            Assert.Equal(zone.ZoneType, reader.GetString(4));
        }
    }
    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbFile))
            {
                File.Delete(_tempDbFile);
            }
        }
        catch
        {
            // Ignore cleanup errors on temp file
        }
    }
}

