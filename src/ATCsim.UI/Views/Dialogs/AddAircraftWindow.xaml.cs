using System.Windows;
using System.Globalization;
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
    private bool _applyingModelDefaults;
    private bool _altitudeEdited;
    private bool _speedEdited;

    public Aircraft? CreatedAircraft { get; private set; }

    public AddAircraftWindow(IReadOnlyList<Airport> airports)
    {
        InitializeComponent();

        AltitudeBox.TextChanged += (_, _) =>
        {
            if (!_applyingModelDefaults) _altitudeEdited = true;
        };
        SpeedBox.TextChanged += (_, _) =>
        {
            if (!_applyingModelDefaults) _speedEdited = true;
        };
        ModelCombo.SelectionChanged += (_, _) => ApplyModelDefaults();
        ModelCombo.ItemsSource = AircraftModelCatalog.All;
        ModelCombo.SelectedIndex = 0;

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

    private void ApplyModelDefaults()
    {
        if (ModelCombo.SelectedItem is not AircraftModelDefinition definition) return;

        _applyingModelDefaults = true;
        try
        {
            if (!_altitudeEdited)
                AltitudeBox.Text = definition.DefaultAltitude.ToString(CultureInfo.InvariantCulture);
            if (!_speedEdited)
                SpeedBox.Text = definition.CruiseSpeed.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _applyingModelDefaults = false;
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

        if (ModelCombo.SelectedItem is not AircraftModelDefinition definition) return;
        var model = definition.CreateModel();

        if (!decimal.TryParse(AltitudeBox.Text, out decimal alt) || alt <= 0 || alt > model.MaxAltitude)
        {
            MessageBox.Show($"Вкажіть висоту понад 0 і до {model.MaxAltitude:0} ft для {model.ModelName}.",
                "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(SpeedBox.Text, out decimal speed) || speed <= 0)
        {
            MessageBox.Show("Вкажіть швидкість понад 0 kts.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Spawn initially near departure airport
        double spawnX = dep.X + 15.0;
        double spawnY = dep.Y - 15.0;

        CreatedAircraft = new Aircraft
        {
            Callsign = callsign,
            ModelId = model.Id,
            IsActive = true,
            CoordX = (decimal)spawnX,
            CoordY = (decimal)spawnY,
            Altitude = alt,
            TargetAltitude = alt,
            GroundSpeed = speed,
            FuelRemaining = model.FuelCapacity * 0.45m,
            DepartureAirportIcao = dep.IcaoCode,
            ArrivalAirportIcao = arr.IcaoCode,
            OriginX = dep.X,
            OriginY = dep.Y,
            DestinationX = arr.X,
            DestinationY = arr.Y,
            TrailPoints = [new(dep.X, dep.Y, alt), new(spawnX, spawnY, alt)],
            Model = model
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
