using ATCsim.Core.Entities;

namespace ATCsim.Core.Weather;

/// <summary>
/// Manages active meteorological conditions, ambient wind vectors, and hazard zones.
/// </summary>
public class WeatherService
{
    public decimal CurrentWindSpeed { get; set; } = 14m; // knots
    public int CurrentWindDirection { get; set; } = 310; // degrees (310 @ 14 kts)
    public List<WeatherZone> ActiveZones { get; set; } = [];

    public bool IsInHazardZone(decimal coordX, decimal coordY, decimal altitude, out WeatherZone? hazardZone)
    {
        foreach (var zone in ActiveZones.Where(z => z.IsActive))
        {
            double dx = (double)(coordX - zone.CenterX);
            double dy = (double)(coordY - zone.CenterY);
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist <= (double)zone.Radius)
            {
                bool inAltRange = true;
                if (zone.MinAltitude.HasValue && altitude < zone.MinAltitude.Value)
                    inAltRange = false;
                if (zone.MaxAltitude.HasValue && altitude > zone.MaxAltitude.Value)
                    inAltRange = false;

                if (inAltRange)
                {
                    hazardZone = zone;
                    return true;
                }
            }
        }

        hazardZone = null;
        return false;
    }
}
