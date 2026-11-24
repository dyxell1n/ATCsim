using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;

namespace ATCsim.Core.WorldGeneration;

public partial class WorldGenerator : IWorldGenerator
{
    public const int MapWidth = 1000;
    public const int MapHeight = 700;

    public WorldSector GenerateSector(long seed)
    {
        var random = new SeedRandom(seed);
        var sector = new WorldSector { Seed = seed };

        var coastline = new Coastline(random);

        // ����� ��������, � ������������ ��������.
        (string Code, string Name)[] airports =
        [
            ("UKLL", "Lviv Danylo Halytskyi"),
            ("UKBB", "Kyiv Boryspil"),
            ("UKKK", "Kyiv Zhuliany"),
            ("UKOO", "Odesa International"),
            ("UKHH", "Kharkiv International")
        ];

        var airportSites = FindInlandAirportSites(coastline, random);
        for (int i = 0; i < airports.Length; i++)
        {
            int index = random.Next(0, airportSites.Count);
            var site = airportSites[index];
            airportSites.RemoveAt(index);

            int headingNumber = random.Next(1, 37);
            int reciprocal = (headingNumber + 17) % 36 + 1;

            var airport = new Airport
            {
                Id = i + 1,
                IcaoCode = airports[i].Code,
                Name = airports[i].Name,

                CoordX = (decimal)site.X,
                CoordY = (decimal)site.Y,

                MaxCapacity = random.Next(10, 41),

                // Գ������� �������� ��������� ��������������.
                CreatedAt = DateTime.UnixEpoch
            };

            int runwayLength = random.Next(22, 41) * 100;

            airport.Runways =
            [
                new Runway
                {
                    Id = i * 2 + 1,
                    AirportId = airport.Id,
                    Designator = headingNumber.ToString("D2"),
                    Angle = headingNumber * 10 % 360,
                    Length = runwayLength,
                    IsAvailable = true
                },
                new Runway
                {
                    Id = i * 2 + 2,
                    AirportId = airport.Id,
                    Designator = reciprocal.ToString("D2"),
                    Angle = reciprocal * 10 % 360,
                    Length = runwayLength,
                    IsAvailable = true
                }
            ];

            sector.Airports.Add(airport);
        }

        GenerateTerrain(sector, coastline, random);
        GenerateWaypoints(sector, random);

        // ������� �� �������� ����.
        string[] zoneTypes =
        [
            "STORM",
            "TURBULENCE",
            "RESTRICTED"
        ];

        for (int i = 0; i < zoneTypes.Length; i++)
        {
            var zone = new WeatherZone
            {
                Id = i + 1,
                Name = $"{zoneTypes[i]} {i + 1:D2}",
                ZoneType = zoneTypes[i],

                CenterX = random.Next(100, 901),
                CenterY = random.Next(100, 601),
                Radius = random.Next(30, 66),

                MinAltitude = 0,
                MaxAltitude = random.Next(18, 37) * 1000,
                IsActive = true
            };
            if (zone.BlocksFlight && !ClearOfAirportApproaches(zone.CenterX, zone.CenterY))
            {
                // Keep the main seed sequence independent of how many possible centers are rejected.
                var zoneRandom = new SeedRandom(seed ^ ((long)(i + 1) << 32));
                bool found = false;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    zone.CenterX = zoneRandom.Next(100, 901);
                    zone.CenterY = zoneRandom.Next(100, 601);
                    if (!(found = ClearOfAirportApproaches(zone.CenterX, zone.CenterY))) continue;
                    break;
                }
                if (!found) continue;
            }
            sector.WeatherZones.Add(zone);

            bool ClearOfAirportApproaches(decimal x, decimal y) => sector.Airports.All(airport =>
                KinematicsCalculator.CalculateDistance2D((double)x, (double)y, (double)airport.CoordX,
                    (double)airport.CoordY) > (double)zone.Radius + 20);
        }

        var models = AircraftModelCatalog.All.Where(model => model.IconType != "A225").ToArray();

        string[] callsigns =
        [
            "UKR102", "WZZ418", "LOT751", "THY461",
            "UKR205", "WZZ619", "LOT832", "THY704"
        ];

        // ˳���� �� �������� ��������.
        for (int i = 0; i < callsigns.Length; i++)
        {
            int departureIndex = random.Next(0, airports.Length);

            // �������� �������� ������ ����������� �� �����������.
            int arrivalIndex = (
                departureIndex + random.Next(1, airports.Length)
            ) % airports.Length;

            var departure = sector.Airports[departureIndex];
            var arrival = sector.Airports[arrivalIndex];

            // Keep the draw reserved for the former initial route progress so
            // aircraft routes and performance draws keep the same seed order.
            _ = random.Next(15, 76);

            decimal altitude = 14000
                + i * 2000
                + random.Next(0, 2) * 1000;

            var definition = models[i % models.Length];
            var model = definition.CreateModel();

            // Keep exactly the same random draws, preserving map geometry and routes.
            decimal speedSample = random.Next(340, 431);
            decimal fuelSample = random.Next(3000, 7001);
            if (model.Category == "LIGHT")
                altitude = definition.DefaultAltitude + altitude % 2000;

            var aircraft = new Aircraft
            {
                Id = i + 1,
                Callsign = callsigns[i],
                ModelId = model.Id,
                Model = model,
                FuelRemaining = model.Id <= 4 ? fuelSample : model.FuelCapacity * (0.4m + (fuelSample - 3000m) / 10000m)
            };
            decimal departureSpeed = model.Id <= 4 ? speedSample : Math.Round(model.CruiseSpeed * speedSample / 430m);
            AircraftTracker.ScheduleDeparture(aircraft, departure, arrival, altitude, departureSpeed);
            sector.Aircraft.Add(aircraft);
        }

        return sector;
    }

    private static void GenerateWaypoints(WorldSector sector, SeedRandom random)
    {
        int waypointCount = random.Next(8, 15);
        for (int i = 0; i < waypointCount; i++)
        {
            sector.Waypoints.Add(new Waypoint(
                $"WP{i + 1:D2}",
                random.Next(60, 941),
                random.Next(60, 641)));
        }
    }

    // ������������� ��������� SplitMix64.
    // ����������� �� 64 ��� ���.
    private sealed class SeedRandom(long seed)
    {
        private ulong _state = unchecked((ulong)seed);

        // min �������, max �� �������.
        public int Next(int min, int max)
        {
            unchecked
            {
                ulong value = (
                    _state += 0x9E3779B97F4A7C15UL
                );

                value = (value ^ (value >> 30))
                    * 0xBF58476D1CE4E5B9UL;

                value = (value ^ (value >> 27))
                    * 0x94D049BB133111EBUL;

                value ^= value >> 31;

                return min + (int)(
                    value % (ulong)(max - min)
                );
            }
        }
    }
}
