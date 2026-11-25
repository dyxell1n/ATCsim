using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;

namespace ATCsim.Data.Dto;

// Omit X/Y for a scheduled departure. Airborne aircraft start at the supplied position.
public sealed record PresetAircraft(string Callsign, string Model, string Departure, string Arrival,
    decimal Altitude, decimal Speed, decimal Fuel, decimal? X = null, decimal? Y = null,
    double? Heading = null, decimal? TargetAltitude = null, double? AssignedHeading = null,
    bool Active = true, AircraftModel? CustomModel = null)
{
    public static PresetAircraft FromAircraft(Aircraft aircraft)
    {
        var model = aircraft.Model;
        var definition = AircraftModelCatalog.FindByIconType(model?.IconType);
        bool standardModel = definition != null && model != null && definition.ModelName == model.ModelName &&
            definition.Category == model.Category && definition.CruiseSpeed == model.CruiseSpeed &&
            definition.MaxAltitude == model.MaxAltitude && definition.FuelCapacity == model.FuelCapacity;
        bool parked = aircraft.PlannedDepartureSpeed.HasValue;
        return new(aircraft.Callsign, model?.IconType ?? "B738", aircraft.DepartureAirportIcao,
            aircraft.ArrivalAirportIcao, parked ? aircraft.TargetAltitude : aircraft.Altitude,
            aircraft.PlannedDepartureSpeed ?? aircraft.GroundSpeed, aircraft.FuelRemaining,
            parked ? null : aircraft.CoordX, parked ? null : aircraft.CoordY, aircraft.Heading,
            aircraft.TargetAltitude, aircraft.HasManualHeadingOverride ? aircraft.TargetHeading : null,
            aircraft.IsActive, standardModel ? null : model);
    }
}

public sealed record AircraftPreset(List<PresetAircraft> Aircraft)
{
    public List<Aircraft> CreateTraffic(IReadOnlyList<Airport> airports)
    {
        if (Aircraft == null) throw new InvalidDataException("The preset is missing its aircraft list.");
        var byIcao = airports.ToDictionary(a => a.IcaoCode, StringComparer.OrdinalIgnoreCase);
        var callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var traffic = new List<Aircraft>(Aircraft.Count);
        foreach (var entry in Aircraft)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Callsign) || !callsigns.Add(entry.Callsign))
                throw new InvalidDataException("Each aircraft needs a unique callsign.");
            if (entry.Departure == null || !byIcao.TryGetValue(entry.Departure, out var departure) ||
                entry.Arrival == null || !byIcao.TryGetValue(entry.Arrival, out var arrival))
                throw new InvalidDataException($"{entry.Callsign} needs airports {entry.Departure} and {entry.Arrival} on this map.");

            var model = entry.CustomModel ?? AircraftModelCatalog.CreateByIconType(entry.Model)
                ?? throw new InvalidDataException($"Unknown aircraft model '{entry.Model}'.");
            if (entry.X.HasValue != entry.Y.HasValue ||
                entry.X is { } startX && !ScenarioFile.Coordinate((double)startX) ||
                entry.Y is { } startY && !ScenarioFile.Coordinate((double)startY) ||
                entry.Speed is < 0 or > 100_000 || (!entry.X.HasValue && entry.Speed == 0))
                throw new InvalidDataException($"Invalid starting position or speed for {entry.Callsign}.");
            if (!ScenarioFile.Altitude(entry.Altitude) ||
                entry.TargetAltitude is { } targetAltitude && !ScenarioFile.Altitude(targetAltitude) ||
                entry.Heading is { } heading && !ScenarioFile.Heading(heading) ||
                entry.AssignedHeading is { } assignedHeading && !ScenarioFile.Heading(assignedHeading))
                throw new InvalidDataException($"Invalid altitude or heading for {entry.Callsign}.");
            var aircraft = new Aircraft { Id = traffic.Count + 1, Callsign = entry.Callsign,
                Model = model, ModelId = model.Id, FuelRemaining = entry.Fuel };
            AircraftTracker.ScheduleDeparture(aircraft, departure, arrival, entry.TargetAltitude ?? entry.Altitude, entry.Speed);
            if (entry.X is { } x && entry.Y is { } y)
            {
                aircraft.PlannedDepartureSpeed = null;
                aircraft.CoordX = x;
                aircraft.CoordY = y;
                aircraft.Altitude = entry.Altitude;
                aircraft.GroundSpeed = entry.Speed;
                aircraft.Heading = entry.Heading ?? KinematicsCalculator.CalculateBearing((double)x, (double)y,
                    aircraft.DestinationX, aircraft.DestinationY);
                aircraft.TargetHeading = entry.AssignedHeading ?? aircraft.Heading;
                aircraft.HasManualHeadingOverride = entry.AssignedHeading.HasValue;
                aircraft.TrailPoints = [new((double)x, (double)y, entry.Altitude)];
            }
            aircraft.IsActive = entry.Active;
            if (!entry.Active) aircraft.Operations.Phase = FlightPhase.Landed;
            traffic.Add(aircraft);
        }
        ScenarioFile.ValidateTraffic(traffic);
        return traffic;
    }
}
