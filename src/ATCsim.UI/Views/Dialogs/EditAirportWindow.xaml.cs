using System.Windows;
using ATCsim.Core.Entities;

namespace ATCsim.UI.Views.Dialogs;

public partial class EditAirportWindow : Window
{
    private readonly Airport _targetAirport;

    public EditAirportWindow(Airport airport)
    {
        InitializeComponent();
        _targetAirport = airport;

        IcaoBox.Text = airport.IcaoCode;
        NameBox.Text = airport.Name;
        CoordXBox.Text = ((int)airport.CoordX).ToString();
        CoordYBox.Text = ((int)airport.CoordY).ToString();
        CapacityBox.Text = airport.MaxCapacity.ToString();

        var rwy = airport.Runways.FirstOrDefault();
        if (rwy != null)
        {
            RwyDesignatorBox.Text = rwy.Designator;
            RwyAngleBox.Text = rwy.Angle.ToString();
            RwyLengthBox.Text = ((int)rwy.Length).ToString();
            RwyAvailableCheck.IsChecked = rwy.IsAvailable;
        }
        else
        {
            RwyDesignatorBox.Text = "36";
            RwyAngleBox.Text = "360";
            RwyLengthBox.Text = "3500";
            RwyAvailableCheck.IsChecked = true;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Будь ласка, вкажіть назву аеропорту.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(CoordXBox.Text, out decimal x);
        decimal.TryParse(CoordYBox.Text, out decimal y);
        int.TryParse(CapacityBox.Text, out int cap);

        _targetAirport.Name = name;
        if (x > 0) _targetAirport.CoordX = x;
        if (y > 0) _targetAirport.CoordY = y;
        if (cap > 0) _targetAirport.MaxCapacity = cap;

        var rwy = _targetAirport.Runways.FirstOrDefault();
        if (rwy == null)
        {
            rwy = new Runway { AirportId = _targetAirport.Id };
            _targetAirport.Runways.Add(rwy);
        }

        rwy.Designator = string.IsNullOrWhiteSpace(RwyDesignatorBox.Text) ? "36" : RwyDesignatorBox.Text.Trim();
        int.TryParse(RwyAngleBox.Text, out int angle);
        rwy.Angle = angle > 0 ? angle : 360;

        decimal.TryParse(RwyLengthBox.Text, out decimal len);
        rwy.Length = len > 0 ? len : 3000m;
        rwy.IsAvailable = RwyAvailableCheck.IsChecked ?? true;

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
