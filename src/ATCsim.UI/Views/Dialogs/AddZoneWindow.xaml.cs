using System.Windows;
using ATCsim.Core.Entities;

namespace ATCsim.UI.Views.Dialogs;

public partial class AddZoneWindow : Window
{
    public WeatherZone? CreatedZone { get; private set; }

    public AddZoneWindow()
    {
        InitializeComponent();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Будь ласка, вкажіть назву зони.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string zoneType = TypeCombo.SelectedIndex switch
        {
            1 => "TURBULENCE",
            2 => "MILITARY",
            _ => "STORM"
        };

        decimal.TryParse(CenterXBox.Text, out decimal cx);
        decimal.TryParse(CenterYBox.Text, out decimal cy);
        decimal.TryParse(RadiusBox.Text, out decimal rad);
        decimal.TryParse(MinAltBox.Text, out decimal minAlt);
        decimal.TryParse(MaxAltBox.Text, out decimal maxAlt);

        if (rad <= 0) rad = 45m;

        CreatedZone = new WeatherZone
        {
            Name = name,
            ZoneType = zoneType,
            CenterX = cx > 0 ? cx : 650m,
            CenterY = cy > 0 ? cy : 350m,
            Radius = rad,
            MinAltitude = minAlt,
            MaxAltitude = maxAlt > 0 ? maxAlt : 30000m,
            IsActive = true
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
