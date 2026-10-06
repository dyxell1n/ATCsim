using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Context;

/// <summary>
/// Initializes SQLite database schema according to the 9-entity relational model.
/// Automatically runs on application startup and ensures baseline data exists.
/// </summary>
public static class DatabaseInitializer
{
    private const string SchemaSql = @"
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS airports (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            icao_code TEXT UNIQUE NOT NULL,
            name TEXT NOT NULL,
            coord_x REAL NOT NULL,
            coord_y REAL NOT NULL,
            max_capacity INTEGER NOT NULL,
            created_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS runways (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            airport_id INTEGER NOT NULL REFERENCES airports(id) ON DELETE CASCADE,
            designator TEXT NOT NULL,
            angle INTEGER NOT NULL,
            length REAL NOT NULL,
            is_available INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS aircraft_models (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            model_name TEXT UNIQUE NOT NULL,
            category TEXT NOT NULL,
            cruise_speed REAL,
            max_altitude REAL,
            fuel_capacity REAL,
            icon_type TEXT
        );

        CREATE TABLE IF NOT EXISTS aircrafts (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            callsign TEXT UNIQUE NOT NULL,
            model_id INTEGER NOT NULL REFERENCES aircraft_models(id),
            is_active INTEGER NOT NULL DEFAULT 1,
            coord_x REAL NOT NULL,
            coord_y REAL NOT NULL,
            altitude REAL NOT NULL,
            fuel_remaining REAL NOT NULL
        );

        CREATE TABLE IF NOT EXISTS flights (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            flight_number TEXT NOT NULL,
            aircraft_id INTEGER NOT NULL REFERENCES aircrafts(id),
            departure_airport_id INTEGER NOT NULL REFERENCES airports(id),
            arrival_airport_id INTEGER NOT NULL REFERENCES airports(id),
            status TEXT NOT NULL DEFAULT 'SCHEDULED',
            departure_time TEXT,
            arrival_time TEXT,
            estimated_arrival_time TEXT,
            delay_minutes INTEGER DEFAULT 0,
            is_delayed INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS communication_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            airport_id INTEGER NOT NULL REFERENCES airports(id),
            aircraft_id INTEGER NOT NULL REFERENCES aircrafts(id),
            sender_type TEXT NOT NULL,
            message TEXT NOT NULL,
            timestamp TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS weather (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            coord_x REAL NOT NULL,
            coord_y REAL NOT NULL,
            wind_speed REAL NOT NULL,
            wind_direction INTEGER NOT NULL,
            recorded_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS weather_zones (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            zone_type TEXT NOT NULL,
            center_x REAL NOT NULL,
            center_y REAL NOT NULL,
            radius REAL NOT NULL,
            min_altitude REAL,
            max_altitude REAL,
            is_active INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS sim_logs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            world_seed INTEGER NOT NULL,
            event_type TEXT NOT NULL,
            flight_id INTEGER REFERENCES flights(id),
            aircraft_id INTEGER REFERENCES aircrafts(id),
            airport_id INTEGER REFERENCES airports(id),
            details TEXT NOT NULL,
            sim_time TEXT NOT NULL,
            created_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS sim_session (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            world_seed INTEGER NOT NULL,
            sim_time TEXT NOT NULL,
            time_scale REAL NOT NULL,
            saved_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS saved_aircraft (
            id INTEGER PRIMARY KEY,
            callsign TEXT UNIQUE NOT NULL,
            model_name TEXT NOT NULL,
            icon_type TEXT NOT NULL,
            coord_x REAL NOT NULL,
            coord_y REAL NOT NULL,
            altitude REAL NOT NULL,
            heading REAL NOT NULL,
            ground_speed REAL NOT NULL,
            target_heading REAL NOT NULL,
            target_altitude REAL NOT NULL,
            fuel_remaining REAL NOT NULL,
            departure_icao TEXT NOT NULL,
            arrival_icao TEXT NOT NULL,
            origin_x REAL NOT NULL,
            origin_y REAL NOT NULL,
            destination_x REAL NOT NULL,
            destination_y REAL NOT NULL,
            trail_points_json TEXT NOT NULL
        );
    ";

    public static void Initialize(DatabaseContext context)
    {
        using var connection = context.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();

        SeedInitialData(connection);
    }

    private static void SeedInitialData(SqliteConnection connection)
    {
        // Check if airports already seeded
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM airports;";
        long count = (long)(checkCmd.ExecuteScalar() ?? 0L);

        if (count > 0) return;

        using var trans = connection.BeginTransaction();
        using var seedCmd = connection.CreateCommand();
        seedCmd.Transaction = trans;

        seedCmd.CommandText = @"
            -- Airports
            INSERT INTO airports (id, icao_code, name, coord_x, coord_y, max_capacity, created_at) VALUES
            (1, 'UKLL', 'Lviv Danylo Halytskyi', 80.0, 280.0, 15, '2026-10-06T12:00:00Z'),
            (2, 'UKBB', 'Kyiv Boryspil', 440.0, 160.0, 35, '2026-10-06T12:00:00Z'),
            (3, 'UKKK', 'Kyiv Zhuliany', 380.0, 220.0, 20, '2026-10-06T12:00:00Z');

            -- Runways
            INSERT INTO runways (id, airport_id, designator, angle, length, is_available) VALUES
            (1, 1, '31', 310, 3305.0, 1),
            (2, 2, '36R', 360, 4000.0, 1),
            (3, 3, '08', 80, 2310.0, 1);

            -- Aircraft Models
            INSERT INTO aircraft_models (id, model_name, category, cruise_speed, max_altitude, fuel_capacity, icon_type) VALUES
            (1, 'Boeing 737-800', 'PASSENGER', 450.0, 41000.0, 20894.0, 'B738'),
            (2, 'Airbus A320', 'PASSENGER', 450.0, 39000.0, 19000.0, 'A320');

            -- Aircrafts
            INSERT INTO aircrafts (id, callsign, model_id, is_active, coord_x, coord_y, altitude, fuel_remaining) VALUES
            (1, 'UKR102', 1, 1, 260.0, 205.0, 28000.0, 4850.0),
            (2, 'WZZ418', 2, 1, 250.0, 280.0, 16000.0, 3200.0);

            -- Flights
            INSERT INTO flights (id, flight_number, aircraft_id, departure_airport_id, arrival_airport_id, status, departure_time, arrival_time, estimated_arrival_time, delay_minutes, is_delayed) VALUES
            (1, 'UK102', 1, 1, 2, 'EN_ROUTE', '2026-10-06T12:18:00Z', '2026-10-06T13:20:00Z', '2026-10-06T13:20:00Z', 0, 0);

            -- Weather
            INSERT INTO weather (id, coord_x, coord_y, wind_speed, wind_direction, recorded_at) VALUES
            (1, 260.0, 205.0, 14.0, 310, '2026-10-06T12:00:00Z');

            -- Weather Zones
            INSERT INTO weather_zones (id, name, zone_type, center_x, center_y, radius, min_altitude, max_altitude, is_active) VALUES
            (1, 'RESTRICTED ZONE #04', 'STORM', 350.0, 150.0, 50.0, 0.0, 22000.0, 1);

            -- Communication Logs
            INSERT INTO communication_logs (id, airport_id, aircraft_id, sender_type, message, timestamp) VALUES
            (1, 1, 1, 'ATC', 'UKR102, cleared for takeoff Runway 31. Climb FL180, heading 045.', '2026-10-06T12:18:04Z'),
            (2, 1, 1, 'AIRCRAFT', 'Cleared for takeoff RWY 31, climbing FL180, heading 045, UKR102.', '2026-10-06T12:18:12Z');

            -- Sim Logs
            INSERT INTO sim_logs (id, world_seed, event_type, flight_id, aircraft_id, airport_id, details, sim_time, created_at) VALUES
            (1, 84920417, 'SYSTEM', 1, 1, 1, 'Airspace sector initialized with default scenario.', '2026-10-06T12:00:00Z', '2026-10-06T12:00:00Z');
        ";

        seedCmd.ExecuteNonQuery();
        trans.Commit();
    }
}
