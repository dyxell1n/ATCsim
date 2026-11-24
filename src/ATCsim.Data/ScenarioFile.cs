using System.Text.Json;
using System.Text.Json.Serialization;
using ATCsim.Core.Entities;
using ATCsim.Core.Services;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data.Dto;

namespace ATCsim.Data;

public static class ScenarioFile
{
    private sealed record Document<T>(string Kind, int Version, T Data);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectRequiredConstructorParameters = true
    };

    public static void WriteSave(string path, SessionSnapshotDto save) => Write(path, "atcsim.save", save);
    public static void WritePreset(string path, AircraftPreset preset) => Write(path, "atcsim.preset", preset);
    public static AircraftPreset ReadPreset(string path) => Read<AircraftPreset>(path, "atcsim.preset");

    public static SessionSnapshotDto ReadSave(string path)
    {
        var save = Read<SessionSnapshotDto>(path, "atcsim.save");
        if (save.TerrainVersion is not (WorldSector.LegacyTerrainVersion or WorldSector.CurrentTerrainVersion)
            || !double.IsFinite(save.TimeScale) || save.TimeScale is < 1 or > SimulationEngine.MaxTimeScale
            || save.SimTime > DateTime.MaxValue.AddDays(-1) || save.Airports == null || save.WeatherZones == null)
            throw new InvalidDataException("Invalid map version, simulation clock or sector data.");
        var icaos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<int>();
        foreach (var a in save.Airports)
        {
            if (a == null || a.Id < 1 || !ids.Add(a.Id) || string.IsNullOrWhiteSpace(a.IcaoCode) ||
                !icaos.Add(a.IcaoCode) || string.IsNullOrWhiteSpace(a.Name) ||
                !Coordinate((double)a.CoordX) || !Coordinate((double)a.CoordY) || a.Runways == null ||
                a.Runways.Any(r => r == null || string.IsNullOrWhiteSpace(r.Designator) || r.Angle is < 0 or > 360))
                throw new InvalidDataException("Invalid or duplicate airport in the save.");
        }
        if (save.WeatherZones.Any(z => z == null || string.IsNullOrWhiteSpace(z.Name) || z.ZoneType == null ||
            !Coordinate((double)z.CenterX) || !Coordinate((double)z.CenterY) || z.Radius is <= 0 or > 1000000 ||
            z.MinAltitude is < 0 or > 1000000 || z.MaxAltitude is < 0 or > 1000000))
            throw new InvalidDataException("Invalid weather zone in the save.");
        ValidateTraffic(save.Aircraft);
        return save;
    }

    private static bool Coordinate(double n) => double.IsFinite(n) && Math.Abs(n) <= 1_000_000_000;
    private static bool Heading(double n) => double.IsFinite(n) && n is >= -360 and <= 360;

    internal static void ValidateTraffic(List<Aircraft> traffic)
    {
        if (traffic == null) throw new InvalidDataException("Missing aircraft list.");
        var ids = new HashSet<int>();
        var callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in traffic)
        {
            if (a == null || a.Id < 1 || !ids.Add(a.Id) || string.IsNullOrWhiteSpace(a.Callsign) || !callsigns.Add(a.Callsign))
                throw new InvalidDataException("Each aircraft needs a unique ID and callsign.");
            if (a.Model == null || string.IsNullOrWhiteSpace(a.Model.ModelName) || string.IsNullOrWhiteSpace(a.Model.IconType) ||
                a.Model.Category == null || a.Model.FuelCapacity is < 0 or > 10_000_000 || a.FuelRemaining is < 0 or > 10_000_000)
                throw new InvalidDataException($"Invalid model or fuel for {a.Callsign}.");
            if (a.GroundSpeed is < 0 or > 100_000 || a.PlannedDepartureSpeed is <= 0 or > 100_000 ||
                a.Altitude is < 0 or > 1_000_000 || a.TargetAltitude is < 0 or > 1_000_000 ||
                !Heading(a.Heading) || !Heading(a.TargetHeading) || !double.IsFinite(a.TrailTimer))
                throw new InvalidDataException($"Invalid flight parameters for {a.Callsign}.");
            if (!Coordinate((double)a.CoordX) || !Coordinate((double)a.CoordY) ||
                !Coordinate(a.OriginX) || !Coordinate(a.OriginY) || !Coordinate(a.DestinationX) || !Coordinate(a.DestinationY) ||
                a.DepartureAirportIcao == null || a.ArrivalAirportIcao == null ||
                a.TrailPoints == null || a.TrailPoints.Any(p => !Coordinate(p.X) || !Coordinate(p.Y)))
                throw new InvalidDataException($"Invalid route or trail for {a.Callsign}.");
            var ops = a.Operations;
            if (ops == null || !Enum.IsDefined(ops.Phase) || ops.RunwayHeading is < 0 or > 360 ||
                ops.ApproachEntryDistanceNm is { } entry && (!double.IsFinite(entry) || entry < 10) ||
                ops.DepartureFuelKg is < 0 or > 10_000_000 ||
                ops.HoldingAltitude is < 0 or > 1_000_000 || ops.HoldingPattern is { } pattern &&
                (ops.HoldingLeg < 0 || ops.HoldingLeg >= pattern.Length || pattern.Any(p => !Coordinate(p.X) || !Coordinate(p.Y))))
                throw new InvalidDataException($"Invalid flight phase or holding pattern for {a.Callsign}.");
        }
    }

    private static T Read<T>(string path, string kind) where T : class
    {
        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<Document<JsonElement>>(stream, Json);
        if (doc?.Kind != kind || doc.Version != 1 || doc.Data.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Expected a version 1 {kind} file.");
        return doc.Data.Deserialize<T>(Json) ?? throw new InvalidDataException("Missing scenario data.");
    }

    private static void Write<T>(string path, string kind, T data)
    {
        string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetRandomFileName());
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, new Document<T>(kind, 1, data), Json);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
