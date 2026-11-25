using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ATCsim.Core.Entities;

namespace ATCsim.UI.Views.Dialogs;

public partial class EditAirportWindow : Window
{
    private readonly ObservableCollection<Runway> _runways;
    public Airport EditedAirport { get; }

    public EditAirportWindow(Airport airport)
    {
        InitializeComponent();
        EditedAirport = new Airport
        {
            Id = airport.Id, IcaoCode = airport.IcaoCode, Name = airport.Name,
            CoordX = airport.CoordX, CoordY = airport.CoordY, MaxCapacity = airport.MaxCapacity,
            CreatedAt = airport.CreatedAt,
            Runways = airport.Runways.Select(runway => new Runway
            {
                Id = runway.Id, AirportId = runway.AirportId, Designator = runway.Designator,
                Angle = runway.Angle, Length = runway.Length, IsAvailable = runway.IsAvailable
            }).ToList()
        };

        IcaoBox.Text = airport.IcaoCode;
        NameBox.Text = airport.Name;
        CoordXBox.Text = airport.CoordX.ToString();
        CoordYBox.Text = airport.CoordY.ToString();
        CapacityBox.Text = airport.MaxCapacity.ToString();

        _runways = new ObservableCollection<Runway>(EditedAirport.Runways);
        RunwaysGrid.ItemsSource = _runways;
    }

    private void OnAddRunwayClick(object sender, RoutedEventArgs e)
    {
        if (!RunwaysGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;
        var runway = new Runway { AirportId = EditedAirport.Id, Length = 3000 };
        _runways.Add(runway);
        RunwaysGrid.SelectedItem = runway;
        RunwaysGrid.ScrollIntoView(runway);
    }

    private void OnRemoveRunwayClick(object sender, RoutedEventArgs e)
    {
        if (RunwaysGrid.SelectedItem is not Runway runway) return;
        RunwaysGrid.CancelEdit(DataGridEditingUnit.Row);
        _runways.Remove(runway);
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
        if (x > 1_000_000_000 || y > 1_000_000_000)
        {
            MessageBox.Show(this, "Координати аеропорту мають бути не більшими за 1 000 000 000.",
                "Параметри аеропорту", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        int.TryParse(CapacityBox.Text, out int cap);

        EditedAirport.Name = name;
        if (x > 0) EditedAirport.CoordX = x;
        if (y > 0) EditedAirport.CoordY = y;
        if (cap > 0) EditedAirport.MaxCapacity = cap;

        if (!RunwaysGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !RunwaysGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;
        var designators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var runway in _runways)
        {
            runway.Designator = runway.Designator.Trim().ToUpperInvariant();
            if (runway.Designator.Length == 0 || !designators.Add(runway.Designator) ||
                runway.Angle is < 0 or > 360 || runway.Length <= 0)
            {
                MessageBox.Show(this, "Вкажіть унікальний номер смуги, курс 0–360° і додатну довжину.",
                    "Характеристики смуги", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        EditedAirport.Runways = [.. _runways];

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
