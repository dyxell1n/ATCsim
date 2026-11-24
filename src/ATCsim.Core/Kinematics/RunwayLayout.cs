using ATCsim.Core.Entities;

namespace ATCsim.Core.Kinematics;

public static class RunwayLayout
{
    public static (int Axis, int Lane) Strip(Runway runway)
    {
        int heading = runway.Angle % 360;
        int lane = runway.Designator.EndsWith('L') ? -1 : runway.Designator.EndsWith('R') ? 1 : 0;
        return (heading % 180, heading < 180 ? lane : -lane);
    }

    // A reciprocal pair shares a strip. Crossing axes interlock at this scale.
    public static bool SharesProtection(Runway first, Runway second)
    {
        var a = Strip(first);
        var b = Strip(second);
        return a.Axis != b.Axis || a.Lane == b.Lane;
    }

    public static Vector2D Center(Airport airport, Runway runway)
    {
        var (axis, lane) = Strip(runway);
        return AirspaceNavigator.Along(new((double)airport.CoordX, (double)airport.CoordY), axis + 90, lane * 0.65);
    }
}
