using ATCsim.Core.Entities;
using ATCsim.Core.Weather;

namespace ATCsim.Core.Kinematics;

/// <summary>
/// Tracks active aircraft spatial coordinates, telemetry updates, and path integration.
/// </summary>
public class AircraftTracker
{
    public event Action<Aircraft>? TelemetryUpdated;

    public void UpdateAircraftPosition(Aircraft aircraft, WeatherService weather, double dtSeconds)
    {
        if (!aircraft.IsActive)
            return;

        // Auto-navigate towards destination airport/waypoint if set and no manual override
        if (!aircraft.HasManualHeadingOverride && (aircraft.DestinationX != 0 || aircraft.DestinationY != 0))
        {
            double distToDest = KinematicsCalculator.CalculateDistance2D(
                (double)aircraft.CoordX,
                (double)aircraft.CoordY,
                aircraft.DestinationX,
                aircraft.DestinationY);

            if (distToDest > 8.0)
            {
                aircraft.TargetHeading = KinematicsCalculator.CalculateBearing(
                    (double)aircraft.CoordX,
                    (double)aircraft.CoordY,
                    aircraft.DestinationX,
                    aircraft.DestinationY);
            }
        }

        // Update heading towards target if assigned
        aircraft.Heading = KinematicsCalculator.UpdateHeading(aircraft.Heading, aircraft.TargetHeading, dtSeconds);

        // Update altitude towards target if assigned
        if (aircraft.TargetAltitude > 0)
        {
            aircraft.Altitude = KinematicsCalculator.UpdateAltitude(aircraft.Altitude, aircraft.TargetAltitude, dtSeconds);
        }

        // Kinematics step
        (double newX, double newY) = KinematicsCalculator.CalculateNextPosition(
            (double)aircraft.CoordX,
            (double)aircraft.CoordY,
            (double)aircraft.GroundSpeed,
            aircraft.Heading,
            (double)weather.CurrentWindSpeed,
            weather.CurrentWindDirection,
            dtSeconds);

        aircraft.CoordX = (decimal)Math.Round(newX, 4);
        aircraft.CoordY = (decimal)Math.Round(newY, 4);

        // Record breadcrumb trail point (recorded per simulation elapsed interval)
        aircraft.TrailTimer += dtSeconds;
        if (aircraft.TrailTimer >= 1.0)
        {
            aircraft.TrailTimer = 0;
            // Record if position shifted or list is empty
            if (aircraft.TrailPoints.Count == 0 ||
                Math.Abs(aircraft.TrailPoints[^1].X - (double)aircraft.CoordX) > 1.5 ||
                Math.Abs(aircraft.TrailPoints[^1].Y - (double)aircraft.CoordY) > 1.5)
            {
                aircraft.TrailPoints.Add(new TrailPoint((double)aircraft.CoordX, (double)aircraft.CoordY, aircraft.Altitude));
                if (aircraft.TrailPoints.Count > 400)
                {
                    aircraft.TrailPoints.RemoveAt(0);
                }
            }
        }

        // Fuel burn calculation (~0.8 kg/sec at cruise)
        if (aircraft.FuelRemaining > 0)
        {
            decimal fuelBurn = (decimal)(0.75 * dtSeconds);
            aircraft.FuelRemaining = Math.Max(0m, aircraft.FuelRemaining - fuelBurn);
        }

        TelemetryUpdated?.Invoke(aircraft);
    }
}
