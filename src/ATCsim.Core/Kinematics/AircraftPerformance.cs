using ATCsim.Core.Entities;

namespace ATCsim.Core.Kinematics;

/// <summary>Shared, simplified performance estimates for the simulation.</summary>
public static class AircraftPerformance
{
    /// <summary>
    /// Estimates cruise fuel use in kg per hour, scaled by tank capacity from the
    /// existing B738 simulation baseline (0.75 kg/s). This is a gameplay estimate,
    /// not an exact aircraft specification. Missing capacity keeps the baseline.
    /// </summary>
    public static decimal FuelBurnPerHour(AircraftModel? model) =>
        model is { FuelCapacity: > 0 }
            ? 2700m * model.FuelCapacity / 20894m
            : 2700m;

    public static decimal FuelCapacity(AircraftModel? model) =>
        model is { FuelCapacity: > 0 } ? model.FuelCapacity : 20894m;

    public static decimal DepartureFuel(AircraftModel? model, double distanceNm, decimal plannedSpeed,
        decimal windSpeed, double windDirection, double course, int otherInboundFlights = 0)
    {
        // Route time with a headwind allowance, 5% contingency, 9 minutes for taxi
        // and terminal operations, a 30-minute reserve, and a load-based arrival allowance.
        decimal headwind = Math.Max(0, windSpeed * (decimal)Math.Cos((windDirection - course) * Math.PI / 180));
        decimal speed = Math.Max(60, plannedSpeed - headwind);
        decimal routeHours = (decimal)distanceNm / speed;
        decimal expectedDelayHours = Math.Clamp(otherInboundFlights, 0, 14) * 0.15m;
        return Math.Ceiling(FuelBurnPerHour(model) * (routeHours * 1.05m + 0.65m + expectedDelayHours));
    }
}
