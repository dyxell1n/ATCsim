using System.Windows;
using System.Windows.Media;

namespace ATCsim.UI.Resources;

/// <summary>Shared, immutable silhouettes. All aircraft face north before heading rotation.</summary>
public static class AviationIcons
{
    private static readonly ResourceDictionary Geometries = new()
    {
        Source = new Uri("/ATCsim.UI;component/Resources/AviationIcons.xaml", UriKind.Relative)
    };

    public static Geometry ForAircraft(string? iconType)
    {
        string key = iconType?.Trim().ToUpperInvariant() switch
        {
            "E195" or "E145" or "E135" => "RegionalAircraftIcon",
            "IL76" => "CargoAircraftIcon",
            "GLEX" => "BusinessAircraftIcon",
            "SR20" or "SR22" or "S22T" => "PropellerAircraftIcon",
            _ => "PassengerAircraftIcon"
        };

        return Get(key);
    }

    private static Geometry Get(string key)
    {
        var geometry = (Geometry)Geometries[key];
        if (!geometry.IsFrozen) geometry.Freeze();
        return geometry;
    }
}
