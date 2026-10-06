using ATCsim.Core.Entities;
using ATCsim.Data.Context;
using Microsoft.Data.Sqlite;

namespace ATCsim.Data.Repositories;

public class AircraftRepository(DatabaseContext context)
{
    public List<Aircraft> GetAll()
    {
        var list = new List<Aircraft>();
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.id, a.callsign, a.model_id, a.is_active, a.coord_x, a.coord_y, a.altitude, a.fuel_remaining,
                   m.model_name, m.category, m.cruise_speed, m.max_altitude, m.fuel_capacity, m.icon_type
            FROM aircrafts a
            JOIN aircraft_models m ON a.model_id = m.id
            ORDER BY a.id;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var aircraft = new Aircraft
            {
                Id = reader.GetInt32(0),
                Callsign = reader.GetString(1),
                ModelId = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) == 1,
                CoordX = (decimal)reader.GetDouble(4),
                CoordY = (decimal)reader.GetDouble(5),
                Altitude = (decimal)reader.GetDouble(6),
                FuelRemaining = (decimal)reader.GetDouble(7),
                Heading = 72,
                TargetHeading = 72,
                GroundSpeed = 420m,
                Model = new AircraftModel
                {
                    Id = reader.GetInt32(2),
                    ModelName = reader.GetString(8),
                    Category = reader.GetString(9),
                    CruiseSpeed = reader.IsDBNull(10) ? 450m : (decimal)reader.GetDouble(10),
                    MaxAltitude = reader.IsDBNull(11) ? 40000m : (decimal)reader.GetDouble(11),
                    FuelCapacity = reader.IsDBNull(12) ? 20000m : (decimal)reader.GetDouble(12),
                    IconType = reader.IsDBNull(13) ? "B738" : reader.GetString(13)
                }
            };
            list.Add(aircraft);
        }

        return list;
    }

    public void UpdateTelemetry(int aircraftId, decimal coordX, decimal coordY, decimal altitude, decimal fuelRemaining)
    {
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE aircrafts 
            SET coord_x = @x, coord_y = @y, altitude = @alt, fuel_remaining = @fuel 
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@x", (double)coordX);
        cmd.Parameters.AddWithValue("@y", (double)coordY);
        cmd.Parameters.AddWithValue("@alt", (double)altitude);
        cmd.Parameters.AddWithValue("@fuel", (double)fuelRemaining);
        cmd.Parameters.AddWithValue("@id", aircraftId);
        cmd.ExecuteNonQuery();
    }
}
