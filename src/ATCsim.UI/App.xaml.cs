using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ATCsim.Core.Entities;

namespace ATCsim.UI;

/// <summary>
/// Application entry point with global exception protection.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var args = e.Args;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length)
            {
                string outputPath = args[i + 1];
                int waitSec = 4;
                string? modalToOpen = null;
                bool selectFlight = false;

                for (int j = 0; j < args.Length; j++)
                {
                    if (args[j] == "--wait" && j + 1 < args.Length && int.TryParse(args[j + 1], out int sec))
                        waitSec = sec;
                    if (args[j] == "--modal" && j + 1 < args.Length)
                        modalToOpen = args[j + 1];
                    if (args[j] == "--select-flight")
                        selectFlight = true;
                }

                await RunScreenshotCaptureAsync(outputPath, waitSec, modalToOpen, selectFlight);
                break;
            }
        }
    }

    private async System.Threading.Tasks.Task RunScreenshotCaptureAsync(
        string outputPath, 
        int waitSec, 
        string? modalToOpen, 
        bool selectFlight)
    {
        await System.Threading.Tasks.Task.Delay(waitSec * 1000);

        var mainWindow = Current.MainWindow as Views.MainWindow;
        if (mainWindow == null) return;
        var vm = mainWindow.DataContext as ViewModels.MainViewModel;

        if (selectFlight && vm != null)
        {
            var firstTarget = vm.Radar.Tracks.FirstOrDefault();
            if (firstTarget != null)
            {
                vm.Radar.SelectedTrack = firstTarget;
            }
        }

        if (!string.IsNullOrEmpty(modalToOpen))
        {
            Window? dialog = null;
            var airports = vm?.Radar.Airports.Select(a => a.Airport).ToList() ?? new List<Airport>();

            if (modalToOpen.Equals("AddAircraft", System.StringComparison.OrdinalIgnoreCase))
            {
                dialog = new Views.Dialogs.AddAircraftWindow(airports) { Owner = mainWindow };
            }
            else if (modalToOpen.Equals("AddZone", System.StringComparison.OrdinalIgnoreCase))
            {
                dialog = new Views.Dialogs.AddZoneWindow { Owner = mainWindow };
            }
            else if (modalToOpen.Equals("AddAirport", System.StringComparison.OrdinalIgnoreCase))
            {
                dialog = new Views.Dialogs.AddAirportWindow { Owner = mainWindow };
            }
            else if (modalToOpen.Equals("EditAirport", System.StringComparison.OrdinalIgnoreCase))
            {
                var apt = airports.FirstOrDefault() ?? new Airport { IcaoCode = "UKBB", Name = "Kyiv Boryspil", CoordX = 640, CoordY = 280, MaxCapacity = 45 };
                dialog = new Views.Dialogs.EditAirportWindow(apt) { Owner = mainWindow };
            }

            if (dialog != null)
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Show();
                await System.Threading.Tasks.Task.Delay(500);

                CaptureWindow(dialog, outputPath);
                dialog.Close();
                Shutdown(0);
                return;
            }
        }

        CaptureWindow(mainWindow, outputPath);
        Shutdown(0);
    }

    private static void CaptureWindow(Window window, string outputPath)
    {
        int width = (int)window.ActualWidth;
        int height = (int)window.ActualHeight;
        if (width <= 0) width = (int)window.Width;
        if (height <= 0) height = (int)window.Height;

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        var dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        using var stream = System.IO.File.Create(outputPath);
        encoder.Save(stream);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "ATCsim Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
