namespace ATCsim.Core.Kinematics;

/// <summary>
/// Mathematical algorithms for aircraft vector integration, wind drift, turns, and vertical profiles.
/// Uses tactical screen/radar coordinate system where North is up (-Y) and East is right (+X).
/// </summary>
public static class KinematicsCalculator
{
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>
    /// Calculates the next position coordinates after a time delta dt (in seconds).
    /// </summary>
    public static (double NewX, double NewY) CalculateNextPosition(
        double currentX,
        double currentY,
        double groundSpeedKnots,
        double headingDegrees,
        double windSpeedKnots,
        double windDirectionDegrees,
        double dtSeconds)
    {
        // Aircraft velocity vector (North = -Y, East = +X)
        double headingRad = headingDegrees * DegreesToRadians;
        double speedNmPerSec = groundSpeedKnots / 3600.0;
        double vx = speedNmPerSec * Math.Sin(headingRad);
        double vy = -speedNmPerSec * Math.Cos(headingRad);

        // Wind vector (wind direction indicates where the wind is coming FROM)
        if (windSpeedKnots > 0)
        {
            double windRad = (windDirectionDegrees + 180.0) % 360.0 * DegreesToRadians;
            double windNmPerSec = windSpeedKnots / 3600.0;
            vx += windNmPerSec * Math.Sin(windRad);
            vy += -windNmPerSec * Math.Cos(windRad);
        }

        double newX = currentX + vx * dtSeconds;
        double newY = currentY + vy * dtSeconds;

        return (newX, newY);
    }

    /// <summary>
    /// Calculates the compass heading (0-359°) from point A to point B.
    /// </summary>
    public static double CalculateBearing(double fromX, double fromY, double toX, double toY)
    {
        double dx = toX - fromX;
        double dy = fromY - toY; // Invert Y because screen Y is down
        double rad = Math.Atan2(dx, dy);
        double deg = rad * RadiansToDegrees;
        return NormalizeCompass(deg);
    }

    /// <summary>
    /// Updates aircraft heading towards target heading using standard rate turn (3 deg/sec).
    /// </summary>
    public static double UpdateHeading(double currentHeading, double targetHeading, double dtSeconds, double turnRateDegPerSec = 3.0)
    {
        double diff = NormalizeAngle(targetHeading - currentHeading);
        if (Math.Abs(diff) < 0.1)
        {
            return NormalizeAngle(targetHeading);
        }

        double maxStep = turnRateDegPerSec * dtSeconds;
        if (Math.Abs(diff) <= maxStep)
        {
            return NormalizeAngle(targetHeading);
        }

        double step = Math.Sign(diff) * maxStep;
        return NormalizeAngle(currentHeading + step);
    }

    /// <summary>
    /// Updates aircraft altitude towards target altitude (standard climb/descent rate: 1500 fpm = 25 fps).
    /// </summary>
    public static decimal UpdateAltitude(decimal currentAlt, decimal targetAlt, double dtSeconds, decimal fpm = 1500m)
    {
        decimal diff = targetAlt - currentAlt;
        if (Math.Abs(diff) < 1m)
        {
            return targetAlt;
        }

        decimal feetPerSecond = fpm / 60m;
        decimal maxStep = feetPerSecond * (decimal)dtSeconds;

        if (Math.Abs(diff) <= maxStep)
        {
            return targetAlt;
        }

        return currentAlt + Math.Sign(diff) * maxStep;
    }

    /// <summary>
    /// Normalizes angle between -180 and +180 degrees for heading difference calculations.
    /// </summary>
    public static double NormalizeAngle(double angle)
    {
        while (angle > 180.0) angle -= 360.0;
        while (angle <= -180.0) angle += 360.0;
        return angle;
    }

    /// <summary>
    /// Normalizes compass heading to 0 - 359.99 degrees.
    /// </summary>
    public static double NormalizeCompass(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }

    /// <summary>
    /// Calculates Euclidean 2D distance between two points.
    /// </summary>
    public static double CalculateDistance2D(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
