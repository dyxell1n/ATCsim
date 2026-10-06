using ATCsim.Core.Entities;
using ATCsim.Data.Context;

namespace ATCsim.Data.Repositories;

public class LogRepository(DatabaseContext context)
{
    public void AddCommunicationLog(CommunicationLog log)
    {
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO communication_logs (airport_id, aircraft_id, sender_type, message, timestamp)
            VALUES (@aid, @acid, @sender, @msg, @ts);
        ";
        cmd.Parameters.AddWithValue("@aid", log.AirportId > 0 ? log.AirportId : 1);
        cmd.Parameters.AddWithValue("@acid", log.AircraftId > 0 ? log.AircraftId : 1);
        cmd.Parameters.AddWithValue("@sender", log.SenderType);
        cmd.Parameters.AddWithValue("@msg", log.Message);
        cmd.Parameters.AddWithValue("@ts", log.Timestamp.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<CommunicationLog> GetCommunicationLogs(int limit = 50)
    {
        var list = new List<CommunicationLog>();
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.airport_id, c.aircraft_id, c.sender_type, c.message, c.timestamp, a.callsign
            FROM communication_logs c
            LEFT JOIN aircrafts a ON c.aircraft_id = a.id
            ORDER BY c.id DESC
            LIMIT @lim;
        ";
        cmd.Parameters.AddWithValue("@lim", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CommunicationLog
            {
                Id = reader.GetInt32(0),
                AirportId = reader.GetInt32(1),
                AircraftId = reader.GetInt32(2),
                SenderType = reader.GetString(3),
                Message = reader.GetString(4),
                Timestamp = DateTime.Parse(reader.GetString(5)),
                Callsign = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        list.Reverse();
        return list;
    }

    public void AddSimLog(SimLog log)
    {
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO sim_logs (world_seed, event_type, flight_id, aircraft_id, airport_id, details, sim_time, created_at)
            VALUES (@seed, @ev, @fid, @acid, @aid, @details, @simTime, @created);
        ";
        cmd.Parameters.AddWithValue("@seed", log.WorldSeed);
        cmd.Parameters.AddWithValue("@ev", log.EventType);
        cmd.Parameters.AddWithValue("@fid", (object?)log.FlightId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@acid", (object?)log.AircraftId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@aid", (object?)log.AirportId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@details", log.Details);
        cmd.Parameters.AddWithValue("@simTime", log.SimTime.ToString("o"));
        cmd.Parameters.AddWithValue("@created", log.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }
}
