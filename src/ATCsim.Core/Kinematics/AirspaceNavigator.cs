using ATCsim.Core.Entities;

namespace ATCsim.Core.Kinematics;

internal static class AirspaceNavigator
{
    public static Vector2D Position(Aircraft aircraft) => new((double)aircraft.CoordX, (double)aircraft.CoordY);

    public static Vector2D Along(Vector2D point, double heading, double distance)
    {
        double radians = heading * Math.PI / 180;
        return new(point.X + distance * Math.Sin(radians), point.Y - distance * Math.Cos(radians));
    }

    public static double Bearing(Vector2D from, Vector2D to) =>
        KinematicsCalculator.CalculateBearing(from.X, from.Y, to.X, to.Y);

    public static Vector2D HeadingGoal(Vector2D position, double heading, IReadOnlyList<WeatherZone> zones)
    {
        double distance = 20;
        foreach (var zone in zones)
        {
            if (!zone.BlocksFlight || zone.Radius <= 0) continue;
            // Put the end of the vector beyond any obstacle so the route can go around it.
            var center = new Vector2D((double)zone.CenterX, (double)zone.CenterY);
            distance = Math.Max(distance, position.DistanceTo(center) + (double)zone.Radius + 20);
        }
        return Along(position, heading, distance);
    }

    public static double EscapeHeading(Vector2D position, decimal altitude, double heading,
        IReadOnlyList<WeatherZone> zones)
    {
        double nearestEdge = double.PositiveInfinity;
        var center = position;
        foreach (var zone in zones)
        {
            if (!zone.BlocksFlight || zone.Radius <= 0 || altitude < zone.MinAltitude || altitude > zone.MaxAltitude)
                continue;
            var candidate = new Vector2D((double)zone.CenterX, (double)zone.CenterY);
            double distance = candidate.DistanceTo(position);
            if (distance > (double)zone.Radius + 1) continue;
            double edge = Math.Abs(distance - (double)zone.Radius);
            if (edge >= nearestEdge) continue;
            nearestEdge = edge;
            center = candidate;
        }
        return center == position ? heading : Bearing(center, position);
    }

    public static double WindCorrectedHeading(Vector2D from, Vector2D to, decimal speed, decimal wind, int direction)
    {
        double course = Bearing(from, to);
        double crosswind = (double)wind * Math.Sin((direction - course) * Math.PI / 180);
        double correction = Math.Asin(Math.Clamp(crosswind / Math.Max((double)speed, 1), -1, 1));
        return KinematicsCalculator.NormalizeCompass(course + correction * 180 / Math.PI);
    }

    public static bool IsClear(Vector2D from, Vector2D to, decimal altitude, decimal targetAltitude,
        IReadOnlyList<WeatherZone> zones, double margin = 0, bool allowExit = false)
    {
        foreach (var zone in zones)
        {
            if (!zone.BlocksFlight || zone.Radius <= 0) continue;
            decimal floor = zone.MinAltitude ?? 0;
            decimal ceiling = zone.MaxAltitude ?? decimal.MaxValue;
            if (ceiling < floor || Math.Max(altitude, targetAltitude) < floor || Math.Min(altitude, targetAltitude) > ceiling) continue;

            double start = 0;
            double end = 1;
            if (altitude != targetAltitude)
            {
                double floorFraction = (double)(floor - altitude) / (double)(targetAltitude - altitude);
                double ceilingFraction = (double)(ceiling - altitude) / (double)(targetAltitude - altitude);
                start = Math.Max(0, Math.Min(floorFraction, ceilingFraction));
                end = Math.Min(1, Math.Max(floorFraction, ceilingFraction));
            }

            if (!ClearOfZone(from, to, zone, margin, allowExit, start, end)) return false;
        }
        return true;
    }

    private static bool ClearOfZone(Vector2D from, Vector2D to, WeatherZone zone, double margin,
        bool allowExit, double start = 0, double end = 1)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double offsetX = from.X - (double)zone.CenterX;
        double offsetY = from.Y - (double)zone.CenterY;
        double lengthSquared = dx * dx + dy * dy;
        double dot = offsetX * dx + offsetY * dy;
        double radius = (double)zone.Radius + margin;
        double radiusSquared = radius * radius;
        double initialDistanceSquared = offsetX * offsetX + offsetY * offsetY;
        if (allowExit && start == 0 && initialDistanceSquared < radiusSquared &&
            ((dot >= 0 && lengthSquared > 0) || end < 1))
            return true;

        double nearest = lengthSquared > 0 ? Math.Clamp(-dot / lengthSquared, start, end) : start;
        double nearestX = offsetX + dx * nearest;
        double nearestY = offsetY + dy * nearest;
        return nearestX * nearestX + nearestY * nearestY > radiusSquared;
    }

    public static Vector2D? NextWaypoint(Vector2D from, Vector2D goal, decimal altitude, decimal targetAltitude,
        IReadOnlyList<WeatherZone> zones, double margin)
    {
        decimal low = Math.Min(altitude, targetAltitude);
        decimal high = Math.Max(altitude, targetAltitude);
        bool Applies(WeatherZone zone) => zone.BlocksFlight && zone.Radius > 0 &&
            (zone.MinAltitude ?? 0) <= high && (zone.MaxAltitude ?? decimal.MaxValue) >= low;
        bool direct = true;
        foreach (var zone in zones)
        {
            if (!Applies(zone) || ClearOfZone(from, goal, zone, margin, allowExit: true)) continue;
            direct = false;
            break;
        }
        if (direct) return goal;
        var barriers = zones.Where(Applies).ToArray();

        const int sides = 16;
        var nodes = new List<Vector2D> { from, goal };
        foreach (var zone in barriers)
        {
            var center = new Vector2D((double)zone.CenterX, (double)zone.CenterY);
            double radius = ((double)zone.Radius + margin + 1) / Math.Cos(Math.PI / sides);
            for (int side = 0; side < sides; side++)
            {
                Vector2D candidate = Along(center, side * 360.0 / sides, radius);
                if (Clear(candidate, candidate)) nodes.Add(candidate);
            }
        }

        var costs = Enumerable.Repeat(double.PositiveInfinity, nodes.Count).ToArray();
        var firstSteps = new int[nodes.Count];
        var visited = new bool[nodes.Count];
        var open = new PriorityQueue<int, double>();
        open.Enqueue(0, 0);
        costs[0] = 0;
        while (open.TryDequeue(out int current, out _))
        {
            if (visited[current]) continue;
            if (current == 1) return nodes[firstSteps[current]];
            visited[current] = true;
            for (int next = 1; next < nodes.Count; next++)
            {
                if (visited[next]) continue;
                double cost = costs[current] + nodes[current].DistanceTo(nodes[next]);
                if (cost >= costs[next] || !Clear(nodes[current], nodes[next], current == 0)) continue;
                firstSteps[next] = current == 0 ? next : firstSteps[current];
                costs[next] = cost;
                open.Enqueue(next, cost);
            }
        }
        return null;

        bool Clear(Vector2D start, Vector2D end, bool allowExit = false)
        {
            foreach (var zone in barriers)
                if (!ClearOfZone(start, end, zone, margin, allowExit)) return false;
            return true;
        }
    }
}
