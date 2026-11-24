using ATCsim.Core.Entities;
using ATCsim.Core.Weather;

namespace ATCsim.Core.Kinematics;

/// <summary>
/// Tracks active aircraft spatial coordinates, telemetry updates, and path integration.
/// </summary>
public class AircraftTracker
{
    public static void ScheduleDeparture(Aircraft aircraft, Airport departure, Airport arrival,
        decimal targetAltitude, decimal departureSpeed)
    {
        aircraft.DepartureAirportIcao = departure.IcaoCode;
        aircraft.ArrivalAirportIcao = arrival.IcaoCode;
        aircraft.CoordX = departure.CoordX;
        aircraft.CoordY = departure.CoordY;
        aircraft.OriginX = (double)departure.CoordX;
        aircraft.OriginY = (double)departure.CoordY;
        aircraft.DestinationX = (double)arrival.CoordX;
        aircraft.DestinationY = (double)arrival.CoordY;

        aircraft.Heading = aircraft.TargetHeading = KinematicsCalculator.CalculateBearing(
            aircraft.OriginX, aircraft.OriginY, aircraft.DestinationX, aircraft.DestinationY);
        aircraft.Altitude = 0;
        aircraft.GroundSpeed = 0;
        aircraft.TargetAltitude = targetAltitude;
        aircraft.PlannedDepartureSpeed = departureSpeed;
        aircraft.IsActive = true;
        aircraft.HasManualHeadingOverride = false;
        aircraft.Operations = new FlightOperationsState();
        aircraft.TrailTimer = 0;
        aircraft.TrailPoints = [new(aircraft.OriginX, aircraft.OriginY, 0)];
    }

    public void UpdateAircraftPosition(Aircraft aircraft, WeatherService weather, double dtSeconds,
        bool followDestination = true, decimal? verticalRate = null)
    {
        if (!aircraft.IsActive || dtSeconds <= 0)
            return;

        if (aircraft.PlannedDepartureSpeed is > 0 and { } departureSpeed)
        {
            aircraft.GroundSpeed = departureSpeed;
            aircraft.PlannedDepartureSpeed = null;
        }

        // Auto-navigate towards destination airport/waypoint if set and no manual override
        if (followDestination && !aircraft.HasManualHeadingOverride && (aircraft.DestinationX != 0 || aircraft.DestinationY != 0))
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

        if (aircraft.TargetAltitude > 0 || verticalRate.HasValue)
        {
            aircraft.TargetAltitude = AvailableAltitude(aircraft, aircraft.TargetAltitude);
            aircraft.Altitude = NextAltitude(aircraft, aircraft.TargetAltitude, dtSeconds, verticalRate);
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
        aircraft.TrailTimer = Math.Round(aircraft.TrailTimer + dtSeconds, 9);
        if (aircraft.TrailTimer >= 1.0)
        {
            aircraft.TrailTimer %= 1;
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

        // Shared simulation estimate scales the baseline burn to the aircraft model.
        if (aircraft.FuelRemaining > 0)
        {
            decimal fuelBurn = AircraftPerformance.FuelBurnPerHour(aircraft.Model) * (decimal)dtSeconds / 3600m;
            aircraft.FuelRemaining = Math.Max(0m, aircraft.FuelRemaining - fuelBurn);
        }
    }

    internal static decimal AvailableAltitude(Aircraft aircraft, decimal target) =>
        aircraft.Model is { MaxAltitude: > 0 } model ? Math.Min(target, model.MaxAltitude) : target;

    internal static decimal NextAltitude(Aircraft aircraft, decimal target, double seconds, decimal? verticalRate = null) =>
        target > 0 || verticalRate.HasValue
            ? KinematicsCalculator.UpdateAltitude(aircraft.Altitude, AvailableAltitude(aircraft, target), seconds, verticalRate ?? 1500m)
            : aircraft.Altitude;
}
