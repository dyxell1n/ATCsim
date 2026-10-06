using System.Windows;
using ATCsim.Core.Entities;

namespace ATCsim.UI.Views.Dialogs;

public partial class AddAirportWindow : Window
{
    public Airport? CreatedAirport { get; private set; }

    public AddAirportWindow()
    {
        InitializeComponent();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        string icao = IcaoBox.Text.Trim().ToUpperInvariant();
        string name = NameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(icao) || icao.Length < 3)
        {
            MessageBox.Show("Будь ласка, вкажіть коректний ICAO код (3-4 символи).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Будь ласка, вкажіть назву аеропорту.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(CoordXBox.Text, out decimal x);
        decimal.TryParse(CoordYBox.Text, out decimal y);
        int.TryParse(CapacityBox.Text, out int cap);
        if (cap <= 0) cap = 15;

        string rwyDes = string.IsNullOrWhiteSpace(RwyDesignatorBox.Text) ? "08" : RwyDesignatorBox.Text.Trim();
        int.TryParse(RwyAngleBox.Text, out int angle);
        decimal.TryParse(RwyLengthBox.Text, out decimal length);
        if (length <= 0) length = 2500m;

        CreatedAirport = new Airport
        {
            IcaoCode = icao,
            Name = name,
            CoordX = x > 0 ? x : 700m,
            CoordY = y > 0 ? y : 400m,
            MaxCapacity = cap,
            CreatedAt = DateTime.UtcNow,
            Runways =
            [
                new Runway
                {
                    Designator = rwyDes,
                    Angle = angle > 0 ? angle : 80,
                    Length = length,
                    IsAvailable = true
                }
            ]
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
