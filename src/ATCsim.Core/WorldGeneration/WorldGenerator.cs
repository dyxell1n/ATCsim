using ATCsim.Core.Entities;

namespace ATCsim.Core.WorldGeneration;

/// <summary>
/// Baseline world sector generator.
/// Generates realistic airspace topology (major airports, runways, waypoints, active flights, and weather hazards).
/// Full procedural algorithm will be extended by M. Borkov in Stage 6.
/// </summary>
public class WorldGenerator : IWorldGenerator
{
    public WorldSector GenerateSector(long seed)
    {
        int seedInt = (int)(seed ^ (seed >> 32));
        var rng = new Random(seedInt);

        // Small deterministic seed offsets for initial aircraft positions
        double ac1XOffset = (rng.NextDouble() - 0.5) * 40.0;
        double ac1YOffset = (rng.NextDouble() - 0.5) * 20.0;
        double ac2XOffset = (rng.NextDouble() - 0.5) * 40.0;
        double ac2YOffset = (rng.NextDouble() - 0.5) * 20.0;

        var sector = new WorldSector
        {
            Seed = seed,
            Airports =
            [
                new Airport
                {
                    Id = 1,
                    IcaoCode = "UKLL",
                    Name = "Lviv Danylo Halytskyi",
                    CoordX = 140m,
                    CoordY = 430m,
                    MaxCapacity = 15,
                    Runways =
                    [
                        new Runway { Id = 1, AirportId = 1, Designator = "31", Angle = 310, Length = 3305m, IsAvailable = true }
                    ]
                },
                new Airport
                {
                    Id = 2,
                    IcaoCode = "UKBB",
                    Name = "Kyiv Boryspil",
                    CoordX = 580m,
                    CoordY = 260m,
                    MaxCapacity = 35,
                    Runways =
                    [
                        new Runway { Id = 2, AirportId = 2, Designator = "36R", Angle = 360, Length = 4000m, IsAvailable = true },
                        new Runway { Id = 3, AirportId = 2, Designator = "18L", Angle = 180, Length = 3500m, IsAvailable = true }
                    ]
                },
                new Airport
                {
                    Id = 3,
                    IcaoCode = "UKKK",
                    Name = "Kyiv Zhuliany",
                    CoordX = 530m,
                    CoordY = 310m,
                    MaxCapacity = 20,
                    Runways =
                    [
                        new Runway { Id = 4, AirportId = 3, Designator = "08", Angle = 80, Length = 2310m, IsAvailable = true }
                    ]
                },
                new Airport
                {
                    Id = 4,
                    IcaoCode = "UKOO",
                    Name = "Odesa International",
                    CoordX = 520m,
                    CoordY = 560m,
                    MaxCapacity = 18,
                    Runways =
                    [
                        new Runway { Id = 5, AirportId = 4, Designator = "16", Angle = 160, Length = 2800m, IsAvailable = true }
                    ]
                },
                new Airport
                {
                    Id = 5,
                    IcaoCode = "UKHH",
                    Name = "Kharkiv International",
                    CoordX = 840m,
                    CoordY = 270m,
                    MaxCapacity = 22,
                    Runways =
                    [
                        new Runway { Id = 6, AirportId = 5, Designator = "07", Angle = 70, Length = 2500m, IsAvailable = true }
                    ]
                }
            ],
            Waypoints =
            [
                new Waypoint("TMR", 260m, 380m),
                new Waypoint("SLV", 380m, 330m),
                new Waypoint("KVR", 480m, 290m),
                new Waypoint("BODRU", 530m, 450m),
                new Waypoint("PEKIT", 710m, 265m)
            ],
            WeatherZones =
            [
                new WeatherZone
                {
                    Id = 1,
                    Name = "RESTRICTED ZONE #04",
                    ZoneType = "STORM",
                    CenterX = 460m,
                    CenterY = 200m,
                    Radius = 55m,
                    MinAltitude = 0m,
                    MaxAltitude = 22000m,
                    IsActive = true
                }
            ],
            Aircraft =
            [
                new Aircraft
                {
                    Id = 1,
                    Callsign = "UKR102",
                    ModelId = 1,
                    IsActive = true,
                    CoordX = (decimal)(310.0 + ac1XOffset),
                    CoordY = (decimal)(360.0 + ac1YOffset),
                    Altitude = 28000m,
                    Heading = 70,
                    TargetHeading = 70,
                    TargetAltitude = 28000m,
                    GroundSpeed = 420m,
                    FuelRemaining = 4850m,
                    DepartureAirportIcao = "UKLL",
                    ArrivalAirportIcao = "UKBB",
                    OriginX = 140.0,
                    OriginY = 430.0,
                    DestinationX = 580.0,
                    DestinationY = 260.0,
                    TrailPoints = [new(140.0, 430.0, 28000m), new(310.0 + ac1XOffset, 360.0 + ac1YOffset, 28000m)],
                    Model = new AircraftModel
                    {
                        Id = 1,
                        ModelName = "Boeing 737-800",
                        Category = "PASSENGER",
                        CruiseSpeed = 450m,
                        MaxAltitude = 41000m,
                        FuelCapacity = 20894m,
                        IconType = "B738"
                    }
                },
                new Aircraft
                {
                    Id = 2,
                    Callsign = "WZZ418",
                    ModelId = 2,
                    IsActive = true,
                    CoordX = (decimal)(460.0 + ac2XOffset),
                    CoordY = (decimal)(310.0 + ac2YOffset),
                    Altitude = 16000m,
                    Heading = 242,
                    TargetHeading = 242,
                    TargetAltitude = 16000m,
                    GroundSpeed = 340m,
                    FuelRemaining = 3200m,
                    DepartureAirportIcao = "UKBB",
                    ArrivalAirportIcao = "UKLL",
                    OriginX = 580.0,
                    OriginY = 260.0,
                    DestinationX = 140.0,
                    DestinationY = 430.0,
                    TrailPoints = [new(580.0, 260.0, 16000m), new(460.0 + ac2XOffset, 310.0 + ac2YOffset, 16000m)],
                    Model = new AircraftModel
                    {
                        Id = 2,
                        ModelName = "Airbus A320",
                        Category = "PASSENGER",
                        CruiseSpeed = 450m,
                        MaxAltitude = 39000m,
                        FuelCapacity = 19000m,
                        IconType = "A320"
                    }
                },
                new Aircraft
                {
                    Id = 3,
                    Callsign = "LOT751",
                    ModelId = 3,
                    IsActive = true,
                    CoordX = 400m,
                    CoordY = 280m,
                    Altitude = 31000m,
                    Heading = 80,
                    TargetHeading = 80,
                    TargetAltitude = 31000m,
                    GroundSpeed = 410m,
                    FuelRemaining = 3900m,
                    DepartureAirportIcao = "UKKK",
                    ArrivalAirportIcao = "UKHH",
                    OriginX = 530.0,
                    OriginY = 310.0,
                    DestinationX = 840.0,
                    DestinationY = 270.0,
                    TrailPoints = [new(530.0, 310.0, 31000m), new(400.0, 280.0, 31000m)],
                    Model = new AircraftModel
                    {
                        Id = 3,
                        ModelName = "Embraer E195",
                        Category = "REGIONAL",
                        CruiseSpeed = 430m,
                        MaxAltitude = 37000m,
                        FuelCapacity = 12971m,
                        IconType = "E195"
                    }
                },
                new Aircraft
                {
                    Id = 4,
                    Callsign = "THY461",
                    ModelId = 4,
                    IsActive = true,
                    CoordX = 530m,
                    CoordY = 480m,
                    Altitude = 22000m,
                    Heading = 350,
                    TargetHeading = 350,
                    TargetAltitude = 22000m,
                    GroundSpeed = 390m,
                    FuelRemaining = 5400m,
                    DepartureAirportIcao = "UKOO",
                    ArrivalAirportIcao = "UKBB",
                    OriginX = 520.0,
                    OriginY = 560.0,
                    DestinationX = 580.0,
                    DestinationY = 260.0,
                    TrailPoints = [new(520.0, 560.0, 22000m), new(530.0, 480.0, 22000m)],
                    Model = new AircraftModel
                    {
                        Id = 4,
                        ModelName = "Airbus A321",
                        Category = "PASSENGER",
                        CruiseSpeed = 460m,
                        MaxAltitude = 39800m,
                        FuelCapacity = 23700m,
                        IconType = "A321"
                    }
                }
            ]
        };

        return sector;
    }
}
