using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data;
using ATCsim.Data.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace ATCsim.UI.ViewModels;

public partial class MainViewModel
{
    public ICommand ImportSaveCommand { get; }
    public ICommand ExportSaveCommand { get; }
    public ICommand ImportPresetCommand { get; }
    public ICommand ExportPresetCommand { get; }
    private bool _controlsEnabled = true;
    public bool ControlsEnabled
    {
        get => _controlsEnabled;
        private set => SetProperty(ref _controlsEnabled, value);
    }

    private async void TransferFile(bool import, bool preset)
    {
        if (!ControlsEnabled) return;
        string extension = preset ? "atcpreset" : "atcsave";
        string label = preset ? "Aircraft preset" : "Map save";
        FileDialog dialog = import ? new OpenFileDialog() : new SaveFileDialog
        {
            FileName = preset ? "Aircraft" : $"Sector-{_engine.CurrentSeed}"
        };
        dialog.Title = $"{(import ? "Import" : "Export")} {label.ToLowerInvariant()}";
        dialog.DefaultExt = "." + extension;
        dialog.Filter = $"{label} (*.{extension})|*.{extension}|JSON (*.json)|*.json";
        if (import) dialog.InitialDirectory = Path.Combine(AppContext.BaseDirectory, preset ? "Presets" : "Saves");
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

        bool resume = _engine.IsRunning;
        ControlsEnabled = false;
        try
        {
            if (resume) _engine.Pause();
            FlushLogs();
            string path = dialog.FileName;
            if (import)
            {
                await ImportScenario(path, preset);
                resume = false;
            }
            else if (preset)
            {
                await Task.Run(() => ScenarioFile.WritePreset(path, new AircraftPreset(
                    _engine.ActiveAircraft.Select(PresetAircraft.FromAircraft).ToList())));
            }
            else
            {
                await Task.Run(() => ScenarioFile.WriteSave(path, new SessionSnapshotDto(
                    _engine.CurrentSeed, _terrainVersion, _engine.SimTime, _engine.TimeScale,
                    _engine.Airports, _engine.WeatherZones, _engine.ActiveAircraft)));
            }
            Logs.AddLog("• FILE", label, $"{(import ? "Imported" : "Exported")} {Path.GetFileName(path)} ({_engine.ActiveAircraft.Count} aircraft).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or InvalidDataException or SqliteException or ArgumentException or OverflowException)
        {
            MessageBox.Show(Application.Current.MainWindow, ex.Message, dialog.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (resume) _engine.Start();
            ControlsEnabled = true;
        }
    }

    private async Task ImportScenario(string path, bool preset)
    {
        if (preset)
        {
            var traffic = await Task.Run(() =>
            {
                var aircraft = ScenarioFile.ReadPreset(path).CreateTraffic(_engine.Airports);
                _repoManager.ImportSector(new WorldSector { Aircraft = aircraft });
                return aircraft;
            });
            _engine.ReplaceAircraft(traffic);
            Radar.RefreshAircraft(traffic);
            UpdateAirborneCount();
        }
        else
        {
            var (save, sector) = await Task.Run(() =>
            {
                var snapshot = ScenarioFile.ReadSave(path);
                var map = snapshot.RestoreMap(_worldGenerator);
                _repoManager.ImportSector(map);
                return (snapshot, map);
            });
            SeedInput = save.WorldSeed.ToString();
            _terrainVersion = save.TerrainVersion;
            _engine.LoadSector(sector, save.SimTime);
            SetPlaybackSpeed(save.TimeScale);
            RefreshRadarSector(sector);
        }
    }
}
