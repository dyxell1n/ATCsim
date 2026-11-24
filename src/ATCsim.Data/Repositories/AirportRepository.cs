using ATCsim.Core.Entities;
using ATCsim.Data.Context;
using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Repositories;

public class AirportRepository(DatabaseContext context)
{
    public List<Airport> GetAll()
    {
        var list = new List<Airport>();
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, icao_code, name, coord_x, coord_y, max_capacity, created_at FROM airports ORDER BY id;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Airport
            {
                Id = reader.GetInt32(0),
                IcaoCode = reader.GetString(1),
                Name = reader.GetString(2),
                CoordX = (decimal)reader.GetDouble(3),
                CoordY = (decimal)reader.GetDouble(4),
                MaxCapacity = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }

        // Attach runways
        foreach (var airport in list)
        {
            airport.Runways = GetRunwaysForAirport(conn, airport.Id);
        }

        return list;
    }

    private static List<Runway> GetRunwaysForAirport(SqliteConnection conn, int airportId)
    {
        var runways = new List<Runway>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, airport_id, designator, angle, length, is_available FROM runways WHERE airport_id = @aid;";
        cmd.Parameters.AddWithValue("@aid", airportId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            runways.Add(new Runway
            {
                Id = reader.GetInt32(0),
                AirportId = reader.GetInt32(1),
                Designator = reader.GetString(2),
                Angle = reader.GetInt32(3),
                Length = (decimal)reader.GetDouble(4),
                IsAvailable = reader.GetInt32(5) == 1
            });
        }
        return runways;
    }

    public void AddAirport(Airport airport)
    {
        using var conn = context.CreateConnection();
        using var trans = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO airports (id, icao_code, name, coord_x, coord_y, max_capacity, created_at)
            VALUES (@id, @icao, @name, @x, @y, @cap, @created)
            ON CONFLICT(id) DO UPDATE SET
                icao_code = excluded.icao_code,
                name = excluded.name,
                coord_x = excluded.coord_x,
                coord_y = excluded.coord_y,
                max_capacity = excluded.max_capacity;
        ";
        cmd.Parameters.AddWithValue("@id", airport.Id);
        cmd.Parameters.AddWithValue("@icao", airport.IcaoCode);
        cmd.Parameters.AddWithValue("@name", airport.Name);
        cmd.Parameters.AddWithValue("@x", (double)airport.CoordX);
        cmd.Parameters.AddWithValue("@y", (double)airport.CoordY);
        cmd.Parameters.AddWithValue("@cap", airport.MaxCapacity);
        cmd.Parameters.AddWithValue("@created", airport.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();

        foreach (var rwy in airport.Runways)
        {
            using var rcmd = conn.CreateCommand();
            rcmd.Transaction = trans;
            rcmd.CommandText = @"
                INSERT INTO runways (id, airport_id, designator, angle, length, is_available)
                VALUES (@id, @aid, @des, @ang, @len, @avail)
                ON CONFLICT(id) DO UPDATE SET
                    designator = excluded.designator,
                    angle = excluded.angle,
                    length = excluded.length,
                    is_available = excluded.is_available;
            ";
            rcmd.Parameters.AddWithValue("@id", rwy.Id);
            rcmd.Parameters.AddWithValue("@aid", airport.Id);
            rcmd.Parameters.AddWithValue("@des", rwy.Designator);
            rcmd.Parameters.AddWithValue("@ang", rwy.Angle);
            rcmd.Parameters.AddWithValue("@len", (double)rwy.Length);
            rcmd.Parameters.AddWithValue("@avail", rwy.IsAvailable ? 1 : 0);
            rcmd.ExecuteNonQuery();
        }

        trans.Commit();
    }

    public void UpdateAirport(Airport airport)
    {
        using var conn = context.CreateConnection();
        using var trans = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            UPDATE airports
            SET name = @name, coord_x = @x, coord_y = @y, max_capacity = @cap
            WHERE icao_code = @icao OR id = @id;
        ";
        cmd.Parameters.AddWithValue("@name", airport.Name);
        cmd.Parameters.AddWithValue("@x", (double)airport.CoordX);
        cmd.Parameters.AddWithValue("@y", (double)airport.CoordY);
        cmd.Parameters.AddWithValue("@cap", airport.MaxCapacity);
        cmd.Parameters.AddWithValue("@icao", airport.IcaoCode);
        cmd.Parameters.AddWithValue("@id", airport.Id);
        cmd.ExecuteNonQuery();

        foreach (var rwy in airport.Runways)
        {
            using var rcmd = conn.CreateCommand();
            rcmd.Transaction = trans;
            rcmd.CommandText = @"
                INSERT INTO runways (id, airport_id, designator, angle, length, is_available)
                VALUES (NULLIF(@id, 0), @aid, @des, @ang, @len, @avail)
                ON CONFLICT(id) DO UPDATE SET
                    designator = excluded.designator, angle = excluded.angle,
                    length = excluded.length, is_available = excluded.is_available
                RETURNING id;
            ";
            rcmd.Parameters.AddWithValue("@des", rwy.Designator);
            rcmd.Parameters.AddWithValue("@ang", rwy.Angle);
            rcmd.Parameters.AddWithValue("@len", (double)rwy.Length);
            rcmd.Parameters.AddWithValue("@avail", rwy.IsAvailable ? 1 : 0);
            rcmd.Parameters.AddWithValue("@id", rwy.Id);
            rcmd.Parameters.AddWithValue("@aid", airport.Id);
            rwy.Id = Convert.ToInt32(rcmd.ExecuteScalar());
        }

        using var remove = conn.CreateCommand();
        remove.Transaction = trans;
        remove.CommandText = """
            DELETE FROM runways WHERE airport_id = @airport AND id NOT IN (SELECT value FROM json_each(@retained));
            """;
        remove.Parameters.AddWithValue("@airport", airport.Id);
        remove.Parameters.AddWithValue("@retained", System.Text.Json.JsonSerializer.Serialize(airport.Runways.Select(r => r.Id)));
        remove.ExecuteNonQuery();

        trans.Commit();
    }
}
