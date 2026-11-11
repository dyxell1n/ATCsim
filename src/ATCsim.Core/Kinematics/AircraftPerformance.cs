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
}
