namespace ATCsim.Core.WorldGeneration;

public partial class WorldGenerator
{
    public void RestoreLegacyTerrain(WorldSector sector)
    {
        var random = new SeedRandom(sector.Seed);
        sector.Terrain.Clear();
        sector.Waypoints.Clear();

        var land = new List<MapPoint> { new(0, 0), new(MapWidth, 0) };
        for (int x = MapWidth; x >= 0; x -= 125)
            land.Add(new MapPoint(x, random.Next(490, 651)));
        sector.Terrain.Add(new TerrainPatch("LAND", land));

        for (int i = 0; i < 5; i++)
        {
            double centerX = random.Next(160, 841);
            double centerY = random.Next(130, 381);
            double radiusX = random.Next(70, 151);
            double radiusY = random.Next(40, 91);
            var outline = new List<MapPoint>();
            for (int vertex = 0; vertex < 12; vertex++)
            {
                double angle = vertex * Math.PI / 6;
                double scale = random.Next(75, 101) / 100.0;
                outline.Add(new MapPoint(
                    Math.Round(centerX + Math.Cos(angle) * radiusX * scale, 3),
                    Math.Round(centerY + Math.Sin(angle) * radiusY * scale, 3)));
            }
            sector.Terrain.Add(new TerrainPatch("HIGHLAND", outline));
        }

        // The snapshot supplies its own airports. Advance past the six draws for each of the five original airports.
        for (int draw = 0; draw < 5 * 6; draw++)
            random.Next(0, 1);
        GenerateWaypoints(sector, random);
    }
}
