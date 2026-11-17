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

    public bool CanDepart(Aircraft aircraft, IReadOnlyList<Aircraft> traffic) =>
        !traffic.Any(other => other != aircraft && other.IsAirborne && IsLossOfSeparation(aircraft, other, out _, out _));

    public List<CollisionAlert> CheckSeparation(IReadOnlyList<Aircraft> aircraftList)
    {
        var alerts = new List<CollisionAlert>();

        for (int i = 0; i < aircraftList.Count; i++)
        {
            var a1 = aircraftList[i];
            if (!a1.IsAirborne) continue;

            for (int j = i + 1; j < aircraftList.Count; j++)
            {
                var a2 = aircraftList[j];
                if (a2.IsAirborne && IsLossOfSeparation(a1, a2, out double hDist, out decimal vDist))
                {
                    var alert = new CollisionAlert(a1, a2, Math.Round(hDist, 2), vDist);
                    alerts.Add(alert);
                    SeparationAlertTriggered?.Invoke(alert);
                }
            }
        }

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
