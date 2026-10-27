using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;

namespace ATCsim.Core.WorldGeneration;

public class WorldGenerator : IWorldGenerator
{
    public const int MapWidth = 1000;
    public const int MapHeight = 700;

    public WorldSector GenerateSector(long seed)
    {
        var random = new SeedRandom(seed);
        var sector = new WorldSector { Seed = seed };

        // Берегова лінія: суша розташована вище берега.
        var land = new List<MapPoint>
        {
            new(0, 0),
            new(MapWidth, 0)
        };

        for (int x = MapWidth; x >= 0; x -= 125)
        {
            land.Add(new MapPoint(x, random.Next(490, 651)));
        }

        sector.Terrain.Add(new TerrainPatch("LAND", land));

        // Генерація ділянок рельєфу.
        for (int i = 0; i < 5; i++)
        {
            double centerX = random.Next(160, 841);
            double centerY = random.Next(130, 381);
            double radiusX = random.Next(70, 151);
            double radiusY = random.Next(40, 91);

            var points = new List<MapPoint>();

            for (int vertex = 0; vertex < 12; vertex++)
            {
                double angle = vertex * Math.PI / 6;
                double scale = random.Next(75, 101) / 100.0;

                double x = centerX
                    + Math.Cos(angle) * radiusX * scale;

                double y = centerY
                    + Math.Sin(angle) * radiusY * scale;

                points.Add(new MapPoint(
                    Math.Round(x, 3),
                    Math.Round(y, 3)));
            }

            sector.Terrain.Add(
                new TerrainPatch("HIGHLAND", points));
        }

        // Назви зберігаємо, а розташування генеруємо.
        (string Code, string Name)[] airports =
        [
            ("UKLL", "Lviv Danylo Halytskyi"),
            ("UKBB", "Kyiv Boryspil"),
            ("UKKK", "Kyiv Zhuliany"),
            ("UKOO", "Odesa International"),
            ("UKHH", "Kharkiv International")
        ];

        // Кожен аеропорт займає окрему комірку.
        // Це гарантує відстань між ними щонайменше 110 одиниць.
        var cells = Enumerable.Range(0, 12).ToList();

        for (int i = 0; i < airports.Length; i++)
        {
            int index = random.Next(0, cells.Count);
            int cell = cells[index];

            cells.RemoveAt(index);

            int headingNumber = random.Next(1, 37);
            int reciprocal = (headingNumber + 17) % 36 + 1;

            var airport = new Airport
            {
                Id = i + 1,
                IcaoCode = airports[i].Code,
                Name = airports[i].Name,

                CoordX = 130 + cell % 4 * 240
                    + random.Next(-20, 21),

                CoordY = 100 + cell / 4 * 150
                    + random.Next(-20, 21),

                MaxCapacity = random.Next(10, 41),

                // Фіксоване значення забезпечує повторюваність.
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

        // Навігаційні точки.
        int waypointCount = random.Next(8, 15);

        for (int i = 0; i < waypointCount; i++)
        {
            sector.Waypoints.Add(new Waypoint(
                $"WP{i + 1:D2}",
                random.Next(60, 941),
                random.Next(60, 641)));
        }

        // Погодні та обмежені зони.
        string[] zoneTypes =
        [
            "STORM",
            "TURBULENCE",
            "RESTRICTED"
        ];

        for (int i = 0; i < zoneTypes.Length; i++)
        {
            sector.WeatherZones.Add(new WeatherZone
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
            });
        }

        AircraftModel[] models =
        [
            new()
            {
                Id = 1,
                ModelName = "Boeing 737-800",
                Category = "PASSENGER",
                CruiseSpeed = 450,
                MaxAltitude = 41000,
                FuelCapacity = 20894,
                IconType = "B738"
            },
            new()
            {
                Id = 2,
                ModelName = "Airbus A320",
                Category = "PASSENGER",
                CruiseSpeed = 450,
                MaxAltitude = 39000,
                FuelCapacity = 19000,
                IconType = "A320"
            },
            new()
            {
                Id = 3,
                ModelName = "Embraer E195",
                Category = "REGIONAL",
                CruiseSpeed = 430,
                MaxAltitude = 37000,
                FuelCapacity = 12971,
                IconType = "E195"
            },
            new()
            {
                Id = 4,
                ModelName = "Airbus A321",
                Category = "PASSENGER",
                CruiseSpeed = 460,
                MaxAltitude = 39800,
                FuelCapacity = 23700,
                IconType = "A321"
            }
        ];

        string[] callsigns =
        [
            "UKR102", "WZZ418", "LOT751", "THY461",
            "UKR205", "WZZ619", "LOT832", "THY704"
        ];

        // Літаки та початкові маршрути.
        for (int i = 0; i < callsigns.Length; i++)
        {
            int departureIndex = random.Next(0, airports.Length);

            // Аеропорт прибуття завжди відрізняється від відправлення.
            int arrivalIndex = (
                departureIndex + random.Next(1, airports.Length)
            ) % airports.Length;

            var departure = sector.Airports[departureIndex];
            var arrival = sector.Airports[arrivalIndex];

            decimal progress = random.Next(15, 76) / 100m;

            decimal x = departure.CoordX
                + (arrival.CoordX - departure.CoordX) * progress;

            decimal y = departure.CoordY
                + (arrival.CoordY - departure.CoordY) * progress;

            double heading = KinematicsCalculator.CalculateBearing(
                (double)x,
                (double)y,
                (double)arrival.CoordX,
                (double)arrival.CoordY);

            decimal altitude = 14000
                + i * 2000
                + random.Next(0, 2) * 1000;

            var model = models[i % models.Length];

            sector.Aircraft.Add(new Aircraft
            {
                Id = i + 1,
                Callsign = callsigns[i],
                ModelId = model.Id,
                Model = model,

                CoordX = x,
                CoordY = y,

                Altitude = altitude,
                TargetAltitude = altitude,

                Heading = heading,
                TargetHeading = heading,

                GroundSpeed = random.Next(340, 431),
                FuelRemaining = random.Next(3000, 7001),
                IsActive = true,

                DepartureAirportIcao = departure.IcaoCode,
                ArrivalAirportIcao = arrival.IcaoCode,

                OriginX = (double)departure.CoordX,
                OriginY = (double)departure.CoordY,

                DestinationX = (double)arrival.CoordX,
                DestinationY = (double)arrival.CoordY,

                TrailPoints =
                [
                    new((double)x, (double)y, altitude)
                ]
            });
        }

        return sector;
    }

    // Детермінований генератор SplitMix64.
    // Використовує всі 64 біти сіду.
    private sealed class SeedRandom(long seed)
    {
        private ulong _state = unchecked((ulong)seed);

        // min включно, max не включно.
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