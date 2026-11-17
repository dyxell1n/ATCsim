namespace ATCsim.Core.WorldGeneration;

public partial class WorldGenerator
{
    private static List<MapPoint> FindInlandAirportSites(Coastline coast, SeedRandom random)
    {
        var sites = new List<MapPoint>();
        const int spacing = 130;
        const int jitter = 10;

        // Adjacent grid cells remain at least 110 units apart after jitter.
        for (int y = 95; y < MapHeight - 50; y += spacing)
        {
            for (int x = 95; x < MapWidth - 50; x += spacing)
            {
                var point = new MapPoint(x + random.Next(-jitter, jitter + 1),
                    y + random.Next(-jitter, jitter + 1));
                if (coast.IsInland(coast.Project(point), 42))
                    sites.Add(point);
            }
        }

        return sites;
    }

    private static void GenerateTerrain(WorldSector sector, Coastline coast, SeedRandom random)
    {
        // Terrain is built in coordinates parallel and perpendicular to the sea edge.
        var mainland = new List<MapPoint> { new(0, 0), new(coast.Length, 0) };
        mainland.AddRange(coast.Shore.Reverse());
        AddPatch(sector, coast, "LAND", mainland);

        GenerateIslands(sector, coast, random);
        GenerateBeaches(sector, coast, random);

        var airports = sector.Airports
            .Select(airport => coast.Project(new MapPoint((double)airport.CoordX, (double)airport.CoordY)))
            .ToArray();

        var peaks = GenerateMountains(sector, coast, random, airports);
        GenerateRivers(sector, coast, random, airports, peaks);
    }

    private static void GenerateIslands(WorldSector sector, Coastline coast, SeedRandom random)
    {
        if (random.Next(0, 100) >= 65)
            return;

        int count = random.Next(1, 4);
        var islands = new List<(MapPoint Center, int RadiusX, int RadiusY)>();
        for (int attempt = 0; attempt < 25 && islands.Count < count; attempt++)
        {
            int radiusX = random.Next(24, 66);
            int radiusY = random.Next(14, 35);
            int x = random.Next(radiusX + 22, coast.Length - radiusX - 22);
            double nearbyShore = Math.Max(coast.ShoreAt(x - radiusX), coast.ShoreAt(x + radiusX));
            foreach (var shorePoint in coast.Shore)
            {
                if (Math.Abs(shorePoint.X - x) <= radiusX)
                    nearbyShore = Math.Max(nearbyShore, shorePoint.Y);
            }

            int minDepth = (int)Math.Ceiling(nearbyShore) + radiusY + 18;
            int maxDepth = coast.Depth - radiusY - 15;
            if (minDepth >= maxDepth)
                continue;

            var center = new MapPoint(x, random.Next(minDepth, maxDepth));
            if (islands.Any(island => Math.Abs(island.Center.X - center.X) < island.RadiusX + radiusX + 16
                && Math.Abs(island.Center.Y - center.Y) < island.RadiusY + radiusY + 16))
                continue;

            islands.Add((center, radiusX, radiusY));
            AddPatch(sector, coast, "LAND", CreateHillOutline(center, radiusX, radiusY, 0, random));
        }
    }

    private static void GenerateBeaches(WorldSector sector, Coastline coast, SeedRandom random)
    {
        const int sections = 4;
        int sectionLength = Coastline.ShoreIntervals / sections;
        for (int section = 0; section < sections; section++)
        {
            if (section % 2 != 0 && random.Next(0, 2) == 0)
                continue;

            int length = random.Next(8, 16);
            int start = section * sectionLength + random.Next(1, sectionLength - length);
            int width = random.Next(18, 36);
            var beach = new List<MapPoint>(length * 2);
            for (int step = 0; step <= length; step++)
                beach.Add(coast.Shore[start + step]);

            for (int step = length - 1; step > 0; step--)
            {
                MapPoint shore = coast.Shore[start + step];
                double inset = Math.Sin(Math.PI * step / length) * width * random.Next(75, 101) / 100;
                beach.Add(new MapPoint(shore.X, shore.Y - inset));
            }

            AddPatch(sector, coast, "SAND", beach);
        }
    }

    private static List<MapPoint> GenerateMountains(
        WorldSector sector, Coastline coast, SeedRandom random, IReadOnlyList<MapPoint> airports)
    {
        var peaks = new List<MapPoint>();
        int count = random.Next(3, 6);
        for (int attempt = 0; attempt < 60 && peaks.Count < count; attempt++)
        {
            var center = new MapPoint(
                random.Next(85, coast.Length - 85),
                random.Next(85, coast.Depth / 2));

            if (peaks.Any(other => DistanceSquared(center, other) < 130 * 130))
                continue;

            double angle = random.Next(0, 180) * Math.PI / 180;
            var range = CreateHillOutline(center, random.Next(80, 141), random.Next(35, 64), angle, random);
            if (range.Any(point => !coast.IsInland(point, 44)) || !IsClearOfAirports(range, airports))
                continue;

            var ridge = range.Select(point => new MapPoint(
                center.X + (point.X - center.X) * 0.61,
                center.Y + (point.Y - center.Y) * 0.51));
            peaks.Add(center);
            AddPatch(sector, coast, "MOUNTAIN", range);
            AddPatch(sector, coast, "MOUNTAIN_RIDGE", ridge);
        }

        return peaks;
    }

    private static void GenerateRivers(
        WorldSector sector, Coastline coast, SeedRandom random, IReadOnlyList<MapPoint> airports,
        List<MapPoint> peaks)
    {
        int count = random.Next(1, 3);
        int basinLength = coast.Length / count;
        for (int basin = 0; basin < count; basin++)
        {
            // Separate basins keep different rivers from crossing each other.
            int basinStart = basin * basinLength;
            int basinEnd = basinStart + basinLength;
            var sources = peaks.Where(point => point.X > basinStart + 65 && point.X < basinEnd - 65).ToArray();
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var source = attempt < sources.Length
                    ? sources[attempt]
                    : new MapPoint(random.Next(basinStart + 65, basinEnd - 65), random.Next(70, 190));
                int swing = random.Next(-58, 59);
                double bend = random.Next(18, 54) * (random.Next(0, 2) == 0 ? -1 : 1);
                double endX = source.X + swing;
                // Use the shore on both sides of the mouth so the ribbon merges into open water.
                double endDepth = Math.Max(coast.ShoreAt(endX - 10), coast.ShoreAt(endX + 10)) + 12;
                var river = CreateRiverOutline(source, new MapPoint(endX, endDepth), bend, basinStart, basinEnd);
                if (!IsClearOfAirports(river, airports))
                    continue;

                AddPatch(sector, coast, "RIVER", river);
                break;
            }
        }
    }

    private static List<MapPoint> CreateRiverOutline(
        MapPoint source, MapPoint mouth, double bend, double basinStart, double basinEnd)
    {
        const int steps = 44;
        var nearBank = new List<MapPoint>(steps + 1);
        var farBank = new List<MapPoint>(steps + 1);
        for (int step = 0; step <= steps; step++)
        {
            double progress = step / (double)steps;
            double x = source.X + (mouth.X - source.X) * progress
                + Math.Sin(progress * Math.Tau) * bend
                + Math.Sin(progress * Math.Tau * 3) * 10;
            x = Math.Clamp(x, basinStart + 12, basinEnd - 12);
            double y = source.Y + (mouth.Y - source.Y) * progress;
            double width = 1.5 + 7 * progress;
            nearBank.Add(new MapPoint(x - width, y));
            farBank.Add(new MapPoint(x + width, y));
        }
        farBank.Reverse();
        nearBank.AddRange(farBank);
        return nearBank;
    }

    private static List<MapPoint> CreateHillOutline(
        MapPoint center, double radiusX, double radiusY, double angle, SeedRandom random)
    {
        const int vertices = 20;
        var outline = new List<MapPoint>(vertices);
        double cosine = Math.Cos(angle);
        double sine = Math.Sin(angle);
        for (int vertex = 0; vertex < vertices; vertex++)
        {
            double direction = vertex * Math.Tau / vertices;
            double scale = random.Next(68, 101) / 100.0;
            double x = Math.Cos(direction) * radiusX * scale;
            double y = Math.Sin(direction) * radiusY * scale;
            outline.Add(new MapPoint(center.X + x * cosine - y * sine, center.Y + x * sine + y * cosine));
        }
        return outline;
    }

    private static bool IsClearOfAirports(IReadOnlyList<MapPoint> outline, IReadOnlyList<MapPoint> airports)
    {
        const int clearance = 18;
        foreach (var airport in airports)
        {
            bool inside = false;
            for (int i = 0; i < outline.Count; i++)
            {
                MapPoint a = outline[i];
                MapPoint b = outline[(i + 1) % outline.Count];
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double projection = Math.Clamp(
                    ((airport.X - a.X) * dx + (airport.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                if (DistanceSquared(airport, new MapPoint(a.X + projection * dx, a.Y + projection * dy))
                    < clearance * clearance)
                    return false;

                if ((a.Y > airport.Y) != (b.Y > airport.Y)
                    && airport.X < a.X + (airport.Y - a.Y) * dx / dy)
                    inside = !inside;
            }
            if (inside)
                return false;
        }
        return true;
    }

    private static double DistanceSquared(MapPoint a, MapPoint b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static void AddPatch(WorldSector sector, Coastline coast, string kind, IEnumerable<MapPoint> outline) =>
        sector.Terrain.Add(new TerrainPatch(kind, outline.Select(coast.ToMap).ToList()));

    private enum SeaEdge { South, West, North, East }

    private sealed class Coastline
    {
        public const int ShoreIntervals = 80;
        private readonly SeaEdge _edge;

        public int Length { get; }
        public int Depth { get; }
        public MapPoint[] Shore { get; }

        public Coastline(SeedRandom random)
        {
            _edge = (SeaEdge)random.Next(0, 4);
            bool horizontal = _edge is SeaEdge.North or SeaEdge.South;
            Length = horizontal ? MapWidth : MapHeight;
            Depth = horizontal ? MapHeight : MapWidth;

            double landFraction = random.Next(63, 74) / 100.0;
            double phase = random.Next(0, 360) * Math.PI / 180;
            double detailPhase = random.Next(0, 360) * Math.PI / 180;
            Shore = new MapPoint[ShoreIntervals + 1];
            for (int i = 0; i <= ShoreIntervals; i++)
            {
                double fraction = i / (double)ShoreIntervals;
                double angle = fraction * Math.Tau;
                double depth = landFraction
                    + 0.10 * Math.Sin(angle + phase)
                    + 0.05 * Math.Sin(2.5 * angle + detailPhase)
                    + 0.015 * Math.Sin(7 * angle - phase);
                Shore[i] = new MapPoint(fraction * Length, depth * Depth);
            }
        }

        public double ShoreAt(double x)
        {
            double index = x / Length * ShoreIntervals;
            int start = Math.Min((int)index, ShoreIntervals - 1);
            return Shore[start].Y + (Shore[start + 1].Y - Shore[start].Y) * (index - start);
        }

        public bool IsInland(MapPoint point, double margin) =>
            point.X >= margin && point.X <= Length - margin
            && point.Y >= margin && point.Y <= ShoreAt(point.X) - margin;

        public MapPoint ToMap(MapPoint point) => _edge switch
        {
            SeaEdge.West => new MapPoint(MapWidth - point.Y, point.X),
            SeaEdge.North => new MapPoint(point.X, MapHeight - point.Y),
            SeaEdge.East => new MapPoint(point.Y, point.X),
            _ => point
        };

        public MapPoint Project(MapPoint point) => _edge switch
        {
            SeaEdge.West => new MapPoint(point.Y, MapWidth - point.X),
            SeaEdge.North => new MapPoint(point.X, MapHeight - point.Y),
            SeaEdge.East => new MapPoint(point.Y, point.X),
            _ => point
        };
    }
}
