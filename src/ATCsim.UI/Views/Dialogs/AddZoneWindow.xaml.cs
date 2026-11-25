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
            3 => "RESTRICTED",
            _ => "STORM"
        };

        if (!TryReadNumber(CenterXBox.Text, 650m, out decimal cx) ||
            !TryReadNumber(CenterYBox.Text, 350m, out decimal cy) ||
            Math.Abs(cx) > 1_000_000_000 || Math.Abs(cy) > 1_000_000_000 ||
            !TryReadNumber(RadiusBox.Text, 45m, out decimal rad) || rad is <= 0 or > 1_000_000)
        {
            MessageBox.Show(this, "Вкажіть координати в межах ±1 000 000 000 і радіус понад 0 і до 1 000 000.",
                "Параметри зони", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!TryReadNumber(MinAltBox.Text, 0m, out decimal minAlt) ||
            !TryReadNumber(MaxAltBox.Text, 30_000m, out decimal maxAlt) ||
            minAlt < 0 || minAlt > maxAlt || maxAlt > 1_000_000)
        {
            MessageBox.Show(this, "Вкажіть межі висоти від 0 до 1 000 000 футів. Нижня межа має бути не більшою за верхню.",
                "Параметри зони", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CreatedZone = new WeatherZone
        {
            Name = name,
            ZoneType = zoneType,
            CenterX = cx,
            CenterY = cy,
            Radius = rad,
            MinAltitude = minAlt,
            MaxAltitude = maxAlt,
            IsActive = true
        };

        DialogResult = true;
        Close();
    }

    private static bool TryReadNumber(string text, decimal defaultValue, out decimal value)
    {
        if (!string.IsNullOrWhiteSpace(text)) return decimal.TryParse(text, out value);
        value = defaultValue;
        return true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
