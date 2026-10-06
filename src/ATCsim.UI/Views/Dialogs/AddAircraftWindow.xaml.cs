using System.Windows;
using ATCsim.Core.Entities;

namespace ATCsim.UI.Views.Dialogs;

public class AirportLookupItem
{
    public string IcaoCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string Display => $"{IcaoCode} - {Name}";
}

public partial class AddAircraftWindow : Window
{
    public Aircraft? CreatedAircraft { get; private set; }

    public AddAircraftWindow(IReadOnlyList<Airport> airports)
    {
        InitializeComponent();

        var airportList = airports.Select(a => new AirportLookupItem
        {
            IcaoCode = a.IcaoCode,
            Name = a.Name,
            X = (double)a.CoordX,
            Y = (double)a.CoordY
        }).ToList();

        DepartureCombo.ItemsSource = airportList;
        ArrivalCombo.ItemsSource = airportList;

        if (airportList.Count > 0)
        {
            DepartureCombo.SelectedIndex = 0;
            ArrivalCombo.SelectedIndex = airportList.Count > 1 ? 1 : 0;
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        string callsign = CallsignBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(callsign))
        {
            MessageBox.Show("Будь ласка, вкажіть позивний літака.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dep = DepartureCombo.SelectedItem as AirportLookupItem;
        var arr = ArrivalCombo.SelectedItem as AirportLookupItem;

        if (dep == null || arr == null)
        {
            MessageBox.Show("Будь ласка, оберіть аеропорти вильоту та прильоту.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(AltitudeBox.Text, out decimal alt);
        if (alt <= 0) alt = 28000m;

        decimal.TryParse(SpeedBox.Text, out decimal speed);
        if (speed <= 0) speed = 420m;

        int modelIdx = ModelCombo.SelectedIndex;
        var (modelName, iconType, maxAlt, fuelCap) = modelIdx switch
        {
            1 => ("Airbus A320", "A320", 39000m, 19000m),
            2 => ("Embraer E195", "E195", 37000m, 12971m),
            3 => ("Airbus A321", "A321", 39800m, 23700m),
            _ => ("Boeing 737-800", "B738", 41000m, 20894m)
        };

        // Spawn initially near departure airport
        double spawnX = dep.X + 15.0;
        double spawnY = dep.Y - 15.0;

        CreatedAircraft = new Aircraft
        {
            Callsign = callsign,
            ModelId = modelIdx + 1,
            IsActive = true,
            CoordX = (decimal)spawnX,
            CoordY = (decimal)spawnY,
            Altitude = alt,
            TargetAltitude = alt,
            GroundSpeed = speed,
            FuelRemaining = fuelCap * 0.45m,
            DepartureAirportIcao = dep.IcaoCode,
            ArrivalAirportIcao = arr.IcaoCode,
            OriginX = dep.X,
            OriginY = dep.Y,
            DestinationX = arr.X,
            DestinationY = arr.Y,
            TrailPoints = [new(dep.X, dep.Y, alt), new(spawnX, spawnY, alt)],
            Model = new AircraftModel
            {
                Id = modelIdx + 1,
                ModelName = modelName,
                IconType = iconType,
                Category = "PASSENGER",
                CruiseSpeed = speed,
                MaxAltitude = maxAlt,
                FuelCapacity = fuelCap
            }
        };

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
