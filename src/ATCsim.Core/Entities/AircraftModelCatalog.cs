namespace ATCsim.Core.Entities;

/// <summary>
/// Shared aircraft choices for generated traffic, manual entry and saved sessions.
/// New entries retain stable database IDs; performance values are simulation defaults.
/// </summary>
public static class AircraftModelCatalog
{
    public static IReadOnlyList<AircraftModelDefinition> All { get; } =
        Array.AsReadOnly<AircraftModelDefinition>(
        [
            new(1, "Boeing 737-800", "PASSENGER", 450, 41000, 20894, "B738", 28000),
            new(2, "Airbus A320", "PASSENGER", 450, 39000, 19000, "A320", 28000),
            new(3, "Embraer E195", "REGIONAL", 430, 37000, 12971, "E195", 26000),
            new(4, "Airbus A321", "PASSENGER", 460, 39800, 23700, "A321", 30000),
            new(5, "Ilyushin Il-76TD", "CARGO", 405, 40000, 88000, "IL76", 26000),
            new(6, "Bombardier Global Express", "BUSINESS", 460, 51000, 20298, "GLEX", 34000),
            new(7, "Cirrus SR22", "LIGHT", 175, 17500, 246, "SR22", 6000)
        ]);

    public static AircraftModelDefinition? FindByIconType(string? iconType) =>
        All.FirstOrDefault(model => string.Equals(
            model.IconType, iconType?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static AircraftModel? CreateByIconType(string? iconType) =>
        FindByIconType(iconType)?.CreateModel();
}

public sealed record AircraftModelDefinition(
    int Id,
    string ModelName,
    string Category,
    decimal CruiseSpeed,
    decimal MaxAltitude,
    decimal FuelCapacity,
    string IconType,
    decimal DefaultAltitude)
{
    public string DisplayName => $"{ModelName} ({IconType} / {Category} / {CruiseSpeed:0} kts)";

    // Return a fresh entity so editing a flight cannot mutate the shared catalog.
    public AircraftModel CreateModel() => new()
    {
        Id = Id,
        ModelName = ModelName,
        Category = Category,
        CruiseSpeed = CruiseSpeed,
        MaxAltitude = MaxAltitude,
        FuelCapacity = FuelCapacity,
        IconType = IconType
    };
}
