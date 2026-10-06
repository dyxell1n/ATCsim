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

    public List<CollisionAlert> CheckSeparation(IReadOnlyList<Aircraft> aircraftList)
    {
        var alerts = new List<CollisionAlert>();

        for (int i = 0; i < aircraftList.Count; i++)
        {
            var a1 = aircraftList[i];
            if (!a1.IsActive) continue;

            for (int j = i + 1; j < aircraftList.Count; j++)
            {
                var a2 = aircraftList[j];
                if (!a2.IsActive) continue;

                var p1 = new Vector2D((double)a1.CoordX, (double)a1.CoordY);
                var p2 = new Vector2D((double)a2.CoordX, (double)a2.CoordY);

                double hDist = p1.DistanceTo(p2);
                decimal vDist = Math.Abs(a1.Altitude - a2.Altitude);

                if (hDist < HorizontalSeparationMinNm && vDist < VerticalSeparationMinFt)
                {
                    var alert = new CollisionAlert(a1, a2, Math.Round(hDist, 2), vDist);
                    alerts.Add(alert);
                    SeparationAlertTriggered?.Invoke(alert);
                }
            }
        }

        return alerts;
    }
}
