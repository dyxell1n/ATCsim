using System.Text.Json;
using ATCsim.Core.Entities;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Context;
using ATCsim.Data.Diagnostics;
using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Repositories;

/// <summary>
/// Unified repository manager coordinating data access, session state, and history.
/// Directly synchronizes live simulation events to SQLite tables.
/// </summary>
public class RepositoryManager
{
    private sealed record AircraftOperationsSnapshot(FlightOperationsState Operations, bool IsActive, bool HasManualHeadingOverride,
        AircraftModel? Model = null, double TrailTimer = 0);

    public DatabaseContext Context { get; }
    public AirportRepository Airports { get; }
    public AircraftRepository Aircraft { get; }
    public FlightRepository Flights { get; }
    public LogRepository Logs { get; }

    public RepositoryManager(DatabaseContext? context = null)
    {
        Context = context ?? new DatabaseContext();
        DatabaseInitializer.Initialize(Context);

        Airports = new AirportRepository(Context);
        Aircraft = new AircraftRepository(Context);
        Flights = new FlightRepository(Context);
        Logs = new LogRepository(Context);
    }

    /// <summary>
    /// Synchronizes a generated or active sector into SQLite tables.
    /// </summary>
    public void SyncSectorToDatabase(WorldSector sector)
    {
        using var conn = Context.CreateConnection();
        using var trans = conn.BeginTransaction();

        SyncSector(sector, conn, trans);
        trans.Commit();
    }

    // Portable files come from other databases. Match stable names before assigning local foreign keys.
    public void ImportSector(WorldSector sector)
    {
        using var conn = Context.CreateConnection();
        using var trans = conn.BeginTransaction();
        var airports = new LocalIds(conn, trans, "SELECT id, icao_code FROM airports");
        var runways = new LocalIds(conn, trans, "SELECT id, airport_id || '/' || designator FROM runways");
        var models = new LocalIds(conn, trans, "SELECT id, model_name FROM aircraft_models");
        var aircraft = new LocalIds(conn, trans, "SELECT id, callsign FROM aircrafts");
        foreach (var airport in sector.Airports)
        {
            airport.Id = airports.Resolve(airport.IcaoCode, airport.Id);
            foreach (var runway in airport.Runways)
            {
                runway.AirportId = airport.Id;
                runway.Id = runways.Resolve($"{airport.Id}/{runway.Designator}", runway.Id);
            }
        }
        foreach (var plane in sector.Aircraft)
        {
            if (plane.Model is { } model)
                plane.ModelId = model.Id = models.Resolve(model.ModelName, model.Id);
            plane.Id = aircraft.Resolve(plane.Callsign, plane.Id);
        }
        SyncSector(sector, conn, trans);
        trans.Commit();
    }

    private sealed class LocalIds
    {
        private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
        private readonly HashSet<int> _used = [];
        private int _next = 1;

        public LocalIds(SqliteConnection connection, SqliteTransaction transaction, string query)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = query;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                _byName[reader.GetString(1)] = id;
                _used.Add(id);
            }
        }

        public int Resolve(string name, int preferred)
        {
            if (_byName.TryGetValue(name, out int id)) return id;
            while (_used.Contains(_next)) _next++;
            id = preferred > 0 && !_used.Contains(preferred) ? preferred : _next;
            _byName.Add(name, id);
            _used.Add(id);
            return id;
        }
    }

    private static void SyncSector(WorldSector sector, SqliteConnection conn, SqliteTransaction trans)
    {
        // Sync airports
        foreach (var apt in sector.Airports)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT INTO airports (id, icao_code, name, coord_x, coord_y, max_capacity, created_at)
                VALUES (@id, @icao, @name, @x, @y, @cap, @created)
                ON CONFLICT(id) DO UPDATE SET
                    coord_x = excluded.coord_x,
                    coord_y = excluded.coord_y,
                    name = excluded.name, max_capacity = excluded.max_capacity;
            ";
            cmd.Parameters.AddWithValue("@id", apt.Id);
            cmd.Parameters.AddWithValue("@icao", apt.IcaoCode);
            cmd.Parameters.AddWithValue("@name", apt.Name);
            cmd.Parameters.AddWithValue("@x", (double)apt.CoordX);
            cmd.Parameters.AddWithValue("@y", (double)apt.CoordY);
            cmd.Parameters.AddWithValue("@cap", apt.MaxCapacity);
            cmd.Parameters.AddWithValue("@created", apt.CreatedAt.ToString("o"));
            cmd.ExecuteNonQuery();

            // Runways
            foreach (var rwy in apt.Runways)
            {
                using var rwyCmd = conn.CreateCommand();
                rwyCmd.Transaction = trans;
                rwyCmd.CommandText = @"
                    INSERT INTO runways (id, airport_id, designator, angle, length, is_available)
                    VALUES (@id, @aid, @des, @ang, @len, @avail)
                    ON CONFLICT(id) DO UPDATE SET
                        airport_id = excluded.airport_id, designator = excluded.designator, length = excluded.length, angle = excluded.angle,
                        is_available = excluded.is_available;
                ";
                rwyCmd.Parameters.AddWithValue("@id", rwy.Id);
                rwyCmd.Parameters.AddWithValue("@aid", apt.Id);
                rwyCmd.Parameters.AddWithValue("@des", rwy.Designator);
                rwyCmd.Parameters.AddWithValue("@ang", rwy.Angle);
                rwyCmd.Parameters.AddWithValue("@len", (double)rwy.Length);
                rwyCmd.Parameters.AddWithValue("@avail", rwy.IsAvailable ? 1 : 0);
                rwyCmd.ExecuteNonQuery();
            }
        }

        var storedModels = new HashSet<int>();
        foreach (var ac in sector.Aircraft)
        {
            if (ac.Model != null && storedModels.Add(ac.Model.Id))
            {
                using var modelCmd = conn.CreateCommand();
                modelCmd.Transaction = trans;
                modelCmd.CommandText = @"
                    INSERT INTO aircraft_models (id, model_name, category, cruise_speed, max_altitude, fuel_capacity, icon_type)
                    VALUES (@id, @name, @cat, @spd, @alt, @fuel, @icon)
                    ON CONFLICT(id) DO UPDATE SET
                        model_name = excluded.model_name, category = excluded.category, cruise_speed = excluded.cruise_speed, max_altitude = excluded.max_altitude, fuel_capacity = excluded.fuel_capacity, icon_type = excluded.icon_type;
                ";
                modelCmd.Parameters.AddWithValue("@id", ac.Model.Id);
                modelCmd.Parameters.AddWithValue("@name", ac.Model.ModelName);
                modelCmd.Parameters.AddWithValue("@cat", ac.Model.Category);
                modelCmd.Parameters.AddWithValue("@spd", (double)ac.Model.CruiseSpeed);
                modelCmd.Parameters.AddWithValue("@alt", (double)ac.Model.MaxAltitude);
                modelCmd.Parameters.AddWithValue("@fuel", (double)ac.Model.FuelCapacity);
                modelCmd.Parameters.AddWithValue("@icon", ac.Model.IconType);
                modelCmd.ExecuteNonQuery();
            }

            using var acCmd = conn.CreateCommand();
            acCmd.Transaction = trans;
            acCmd.CommandText = @"
                INSERT INTO aircrafts (id, callsign, model_id, is_active, coord_x, coord_y, altitude, fuel_remaining)
                VALUES (@id, @cs, @mid, @act, @x, @y, @alt, @fuel)
                ON CONFLICT(id) DO UPDATE SET
                    callsign = excluded.callsign,
                    model_id = excluded.model_id,
                    coord_x = excluded.coord_x,
                    coord_y = excluded.coord_y,
                    altitude = excluded.altitude,
                    fuel_remaining = excluded.fuel_remaining,
                    is_active = excluded.is_active;
            ";
            acCmd.Parameters.AddWithValue("@id", ac.Id);
            acCmd.Parameters.AddWithValue("@cs", ac.Callsign);
            acCmd.Parameters.AddWithValue("@mid", ac.ModelId);
            acCmd.Parameters.AddWithValue("@act", ac.IsActive ? 1 : 0);
            acCmd.Parameters.AddWithValue("@x", (double)ac.CoordX);
            acCmd.Parameters.AddWithValue("@y", (double)ac.CoordY);
            acCmd.Parameters.AddWithValue("@alt", (double)ac.Altitude);
            acCmd.Parameters.AddWithValue("@fuel", (double)ac.FuelRemaining);
            acCmd.ExecuteNonQuery();
        }

        // Sync weather zones
        foreach (var zone in sector.WeatherZones)
        {
            using var zoneCmd = conn.CreateCommand();
            zoneCmd.Transaction = trans;
            zoneCmd.CommandText = @"
                INSERT INTO weather_zones (id, name, zone_type, center_x, center_y, radius, min_altitude, max_altitude, is_active)
                VALUES (@id, @name, @zt, @cx, @cy, @rad, @minalt, @maxalt, @act)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name, zone_type = excluded.zone_type, center_x = excluded.center_x, center_y = excluded.center_y, radius = excluded.radius, min_altitude = excluded.min_altitude, max_altitude = excluded.max_altitude, is_active = excluded.is_active;
            ";
            zoneCmd.Parameters.AddWithValue("@id", zone.Id);
            zoneCmd.Parameters.AddWithValue("@name", zone.Name);
            zoneCmd.Parameters.AddWithValue("@zt", zone.ZoneType);
            zoneCmd.Parameters.AddWithValue("@cx", (double)zone.CenterX);
            zoneCmd.Parameters.AddWithValue("@cy", (double)zone.CenterY);
            zoneCmd.Parameters.AddWithValue("@rad", (double)zone.Radius);
            zoneCmd.Parameters.AddWithValue("@minalt", zone.MinAltitude.HasValue ? (object)(double)zone.MinAltitude.Value : DBNull.Value);
            zoneCmd.Parameters.AddWithValue("@maxalt", zone.MaxAltitude.HasValue ? (object)(double)zone.MaxAltitude.Value : DBNull.Value);
            zoneCmd.Parameters.AddWithValue("@act", zone.IsActive ? 1 : 0);
            zoneCmd.ExecuteNonQuery();
        }
    }

    public bool HasSavedSession()
    {
        using var conn = Context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sim_session;";
        long count = (long)(cmd.ExecuteScalar() ?? 0L);
        return count > 0;
    }

    public void SaveSessionProgress(long seed, DateTime simTime, double timeScale, IReadOnlyList<Airport> airports,
        IReadOnlyList<Aircraft> aircraftList, IReadOnlyList<WeatherZone> weatherZones,
        int terrainVersion = WorldSector.CurrentTerrainVersion)
    {
        using var conn = Context.CreateConnection();
        using var trans = conn.BeginTransaction();

        // 1. Save session metadata
        using var sessCmd = conn.CreateCommand();
        sessCmd.Transaction = trans;
        sessCmd.CommandText = @"
            INSERT INTO sim_session (id, world_seed, sim_time, time_scale, terrain_version, saved_at, zones_json, airports_json)
            VALUES (1, @seed, @st, @ts, @terrainVersion, @now, @zones, @airports)
            ON CONFLICT(id) DO UPDATE SET
                world_seed = excluded.world_seed,
                sim_time = excluded.sim_time,
                time_scale = excluded.time_scale,
                terrain_version = excluded.terrain_version,
                zones_json = excluded.zones_json,
                airports_json = excluded.airports_json,
                saved_at = excluded.saved_at;
        ";
        sessCmd.Parameters.AddWithValue("@seed", seed);
        sessCmd.Parameters.AddWithValue("@st", simTime.ToString("o"));
        sessCmd.Parameters.AddWithValue("@ts", timeScale);
        sessCmd.Parameters.AddWithValue("@terrainVersion", terrainVersion);
        sessCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        sessCmd.Parameters.AddWithValue("@airports", JsonSerializer.Serialize(airports));
        sessCmd.Parameters.AddWithValue("@zones", JsonSerializer.Serialize(weatherZones));
        sessCmd.ExecuteNonQuery();

        // 2. Save active aircraft
        using var delAcCmd = conn.CreateCommand();
        delAcCmd.Transaction = trans;
        delAcCmd.CommandText = "DELETE FROM saved_aircraft;";
        delAcCmd.ExecuteNonQuery();

        foreach (var ac in aircraftList)
        {
            string trailJson = System.Text.Json.JsonSerializer.Serialize(ac.TrailPoints);

            using var acCmd = conn.CreateCommand();
            acCmd.Transaction = trans;
            acCmd.CommandText = @"
                INSERT INTO saved_aircraft (
                    id, callsign, model_name, icon_type, coord_x, coord_y, altitude,
                    heading, ground_speed, target_heading, target_altitude, fuel_remaining,
                    departure_icao, arrival_icao, origin_x, origin_y, destination_x, destination_y,
                    trail_points_json, departure_speed, operations_json
                ) VALUES (
                    @id, @cs, @mn, @icon, @cx, @cy, @alt,
                    @hdg, @spd, @thdg, @talt, @fuel,
                    @dep, @arr, @ox, @oy, @dx, @dy,
                    @trail, @departureSpeed, @operations
                );
            ";
            acCmd.Parameters.AddWithValue("@id", ac.Id);
            acCmd.Parameters.AddWithValue("@cs", ac.Callsign);
            acCmd.Parameters.AddWithValue("@mn", ac.Model?.ModelName ?? "Boeing 737-800");
            acCmd.Parameters.AddWithValue("@icon", ac.Model?.IconType ?? "B738");
            acCmd.Parameters.AddWithValue("@cx", (double)ac.CoordX);
            acCmd.Parameters.AddWithValue("@cy", (double)ac.CoordY);
            acCmd.Parameters.AddWithValue("@alt", (double)ac.Altitude);
            acCmd.Parameters.AddWithValue("@hdg", ac.Heading);
            acCmd.Parameters.AddWithValue("@spd", (double)ac.GroundSpeed);
            acCmd.Parameters.AddWithValue("@thdg", ac.TargetHeading);
            acCmd.Parameters.AddWithValue("@talt", (double)ac.TargetAltitude);
            acCmd.Parameters.AddWithValue("@fuel", (double)ac.FuelRemaining);
            acCmd.Parameters.AddWithValue("@dep", ac.DepartureAirportIcao);
            acCmd.Parameters.AddWithValue("@arr", ac.ArrivalAirportIcao);
            acCmd.Parameters.AddWithValue("@ox", ac.OriginX);
            acCmd.Parameters.AddWithValue("@oy", ac.OriginY);
            acCmd.Parameters.AddWithValue("@dx", ac.DestinationX);
            acCmd.Parameters.AddWithValue("@dy", ac.DestinationY);
            acCmd.Parameters.AddWithValue("@trail", trailJson);
            acCmd.Parameters.AddWithValue("@departureSpeed", ac.PlannedDepartureSpeed is { } speed ? (double)speed : DBNull.Value);
            acCmd.Parameters.AddWithValue("@operations", JsonSerializer.Serialize(
                new AircraftOperationsSnapshot(ac.Operations, ac.IsActive, ac.HasManualHeadingOverride, ac.Model, ac.TrailTimer)));
            acCmd.ExecuteNonQuery();
        }

        trans.Commit();
    }

    public (long Seed, DateTime SimTime, double TimeScale, List<Aircraft> Aircraft, int TerrainVersion,
        List<WeatherZone>? WeatherZones, List<Airport>? Airports)? LoadSavedSession()
    {
        using var conn = Context.CreateConnection();
        long seed = 84920417;
        DateTime simTime = DateTime.UtcNow;
        double timeScale = 1.0;
        int terrainVersion;
        List<WeatherZone>? weatherZones;
        List<Airport>? airports;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT world_seed, sim_time, time_scale, terrain_version, zones_json, airports_json FROM sim_session WHERE id = 1;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                seed = reader.GetInt64(0);
                simTime = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                timeScale = reader.GetDouble(2);
                terrainVersion = reader.GetInt32(3);
                airports = reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<List<Airport>>(reader.GetString(5));
                weatherZones = reader.IsDBNull(4) ? null : JsonSerializer.Deserialize<List<WeatherZone>>(reader.GetString(4));
            }
            else return null;
        }

        var list = new List<Aircraft>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, callsign, model_name, icon_type, coord_x, coord_y, altitude,
                       heading, ground_speed, target_heading, target_altitude, fuel_remaining,
                       departure_icao, arrival_icao, origin_x, origin_y, destination_x, destination_y,
                       trail_points_json, departure_speed, operations_json
                FROM saved_aircraft ORDER BY id;
            ";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string modelName = reader.GetString(2);
                string iconType = reader.GetString(3);
                // Older snapshots store only the model name and icon code. Recover
                // catalog capabilities without changing the existing SQLite schema.
                var model = AircraftModelCatalog.CreateByIconType(iconType) ?? new AircraftModel
                {
                    ModelName = modelName,
                    IconType = iconType,
                    Category = "PASSENGER"
                };
                model.ModelName = modelName;

                var ac = new Aircraft
                {
                    Id = reader.GetInt32(0),
                    Callsign = reader.GetString(1),
                    ModelId = model.Id,
                    Model = model,
                    CoordX = (decimal)reader.GetDouble(4),
                    CoordY = (decimal)reader.GetDouble(5),
                    Altitude = (decimal)reader.GetDouble(6),
                    Heading = reader.GetDouble(7),
                    GroundSpeed = (decimal)reader.GetDouble(8),
                    TargetHeading = reader.GetDouble(9),
                    TargetAltitude = (decimal)reader.GetDouble(10),
                    FuelRemaining = (decimal)reader.GetDouble(11),
                    DepartureAirportIcao = reader.GetString(12),
                    ArrivalAirportIcao = reader.GetString(13),
                    OriginX = reader.GetDouble(14),
                    OriginY = reader.GetDouble(15),
                    DestinationX = reader.GetDouble(16),
                    DestinationY = reader.GetDouble(17),
                    PlannedDepartureSpeed = reader.IsDBNull(19) ? null : (decimal)reader.GetDouble(19)
                };

                if (!reader.IsDBNull(20) && JsonSerializer.Deserialize<AircraftOperationsSnapshot>(reader.GetString(20)) is { } snapshot)
                {
                    ac.Model = snapshot.Model ?? ac.Model;
                    ac.ModelId = ac.Model!.Id;
                    ac.TrailTimer = snapshot.TrailTimer;
                    ac.Operations = snapshot.Operations;
                    ac.IsActive = snapshot.IsActive;
                    ac.HasManualHeadingOverride = snapshot.HasManualHeadingOverride;
                }

                string trailJson = reader.GetString(18);
                if (!string.IsNullOrWhiteSpace(trailJson))
                {
                    try
                    {
                        var points = System.Text.Json.JsonSerializer.Deserialize<List<TrailPoint>>(trailJson);
                        if (points != null)
                        {
                            // Filter out any invalid / default (0, 0) top-left coordinates
                            ac.TrailPoints = points.Where(p => p.X > 5.0 && p.Y > 5.0).ToList();
                        }
                    }
                    catch (JsonException exception)
                    {
                        AppDiagnostics.Warning(exception, "Stored aircraft track could not be restored for {AircraftId}", ac.Id);
                    }
                }

                if (ac.TrailPoints.Count == 0 && ac.Operations.Phase != FlightPhase.Landed)
                {
                    double startX = ac.OriginX > 5.0 ? ac.OriginX : (double)ac.CoordX;
                    double startY = ac.OriginY > 5.0 ? ac.OriginY : (double)ac.CoordY;
                    ac.TrailPoints.Add(new TrailPoint(startX, startY, ac.Altitude));
                    ac.TrailPoints.Add(new TrailPoint((double)ac.CoordX, (double)ac.CoordY, ac.Altitude));
                }

                list.Add(ac);
            }
        }

        return (seed, simTime, timeScale, list, terrainVersion, weatherZones, airports);
    }
}


