using ATCsim.Core.Entities;
using ATCsim.Data.Context;

namespace ATCsim.Data.Repositories;

public class FlightRepository(DatabaseContext context)
{
    public List<Flight> GetAll()
    {
        var list = new List<Flight>();
        using var conn = context.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, flight_number, aircraft_id, departure_airport_id, arrival_airport_id, 
                   status, departure_time, arrival_time, estimated_arrival_time, delay_minutes, is_delayed
            FROM flights
            ORDER BY id;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Flight
            {
                Id = reader.GetInt32(0),
                FlightNumber = reader.GetString(1),
                AircraftId = reader.GetInt32(2),
                DepartureAirportId = reader.GetInt32(3),
                ArrivalAirportId = reader.GetInt32(4),
                Status = reader.GetString(5),
                DepartureTime = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                ArrivalTime = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                EstimatedArrivalTime = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                DelayMinutes = reader.GetInt32(9),
                IsDelayed = reader.GetInt32(10) == 1
            });
        }

        return list;
    }
}
