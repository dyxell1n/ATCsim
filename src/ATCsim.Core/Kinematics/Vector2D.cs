namespace ATCsim.Core.Kinematics;

/// <summary>
/// Two-dimensional coordinate representation in the radar sector space.
/// </summary>
public readonly record struct Vector2D(double X, double Y)
{
    public static Vector2D Zero => new(0, 0);

    public double DistanceTo(Vector2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
