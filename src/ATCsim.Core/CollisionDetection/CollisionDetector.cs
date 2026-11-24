using ATCsim.Core.Entities;
using ATCsim.Core.Kinematics;

namespace ATCsim.Core.CollisionDetection;

public record CollisionAlert(Aircraft Aircraft1, Aircraft Aircraft2, double HorizontalDistanceNm, decimal VerticalDistanceFt);

/// <summary>
/// Monitors airspace separation minimums (horizontal &lt; 5 NM, vertical &lt; 1000 ft)
/// and triggers alerts when loss of separation is detected.
/// </summary>
public class CollisionDetector
{
    public const double HorizontalSeparationMinNm = 5.0;
    public const decimal VerticalSeparationMinFt = 1000m;

    public event Action<CollisionAlert>? SeparationAlertTriggered;

    public bool CanDepart(Aircraft aircraft, IReadOnlyList<Aircraft> traffic, Vector2D? runwayStart = null)
    {
        var start = runwayStart ?? AirspaceNavigator.Position(aircraft);
        return !traffic.Any(other => other != aircraft && other.IsAirborne &&
            Math.Abs(aircraft.Altitude - other.Altitude) < VerticalSeparationMinFt &&
            start.DistanceTo(AirspaceNavigator.Position(other)) < HorizontalSeparationMinNm);
    }

    public List<CollisionAlert> CheckSeparation(IReadOnlyList<Aircraft> aircraftList)
    {
        var sorted = aircraftList.Select((aircraft, index) => (aircraft, index))
            .Where(entry => entry.aircraft.IsAirborne)
            .OrderBy(entry => entry.aircraft.CoordX).ToArray();
        var pairs = new List<(int First, int Second, CollisionAlert Alert)>?[sorted.Length];

        void Scan(int i)
        {
            var (left, leftIndex) = sorted[i];
            for (int j = i + 1; j < sorted.Length; j++)
            {
                var (right, rightIndex) = sorted[j];
                if ((double)right.CoordX - (double)left.CoordX >= HorizontalSeparationMinNm) break;
                if (!IsLossOfSeparation(left, right, out double distance, out decimal vertical)) continue;
                int first = Math.Min(leftIndex, rightIndex);
                int second = Math.Max(leftIndex, rightIndex);
                (pairs[i] ??= []).Add((first, second,
                    new(aircraftList[first], aircraftList[second], Math.Round(distance, 2), vertical)));
            }
        }

        if (sorted.Length >= 256)
            Parallel.For(0, sorted.Length, Scan);
        else
            for (int i = 0; i < sorted.Length; i++) Scan(i);

        // Preserve input pair order and deliver events on the simulation thread.
        var alerts = pairs.Where(row => row != null).SelectMany(row => row!)
            .OrderBy(pair => pair.First).ThenBy(pair => pair.Second).Select(pair => pair.Alert).ToList();
        foreach (var alert in alerts) SeparationAlertTriggered?.Invoke(alert);
        return alerts;
    }

    private static bool IsLossOfSeparation(Aircraft first, Aircraft second, out double horizontal, out decimal vertical)
    {
        var firstPosition = new Vector2D((double)first.CoordX, (double)first.CoordY);
        var secondPosition = new Vector2D((double)second.CoordX, (double)second.CoordY);
        horizontal = firstPosition.DistanceTo(secondPosition);
        vertical = Math.Abs(first.Altitude - second.Altitude);
        return horizontal < HorizontalSeparationMinNm && vertical < VerticalSeparationMinFt;
    }
}
