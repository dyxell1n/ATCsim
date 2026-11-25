using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using ATCsim.Core.WorldGeneration;
using ATCsim.Data;
using ATCsim.Data.Diagnostics;
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
        string operation = import ? "Import" : "Export";
        string title = $"{operation} {label.ToLowerInvariant()}";

        bool resume = false;
        try
        {
            FileDialog dialog = import ? new OpenFileDialog() : new SaveFileDialog
            {
                FileName = preset ? "Aircraft" : $"Sector-{_engine.CurrentSeed}"
            };
            dialog.Title = title;
            dialog.DefaultExt = "." + extension;
            dialog.Filter = $"{label} (*.{extension})|*.{extension}|JSON (*.json)|*.json";
            if (import) dialog.InitialDirectory = BundledScenarios.GetImportDirectory(preset);
            if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

            resume = _engine.IsRunning;
            ControlsEnabled = false;
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
            if (ex is SqliteException)
                AppDiagnostics.Error(ex, "Scenario {Operation} failed in SQLite for {ScenarioType}", operation, extension);
            else
                AppDiagnostics.Warning(ex, "Scenario {Operation} failed for {ScenarioType}", operation, extension);

            string message = ex switch
            {
                InvalidDataException => ex.Message,
                JsonException => "Файл містить некоректний JSON або неповні дані сценарію.",
                SqliteException => "Не вдалося записати сценарій у локальну базу даних.",
                UnauthorizedAccessException => "Немає дозволу на доступ до файла або каталогу.",
                IOException => "Не вдалося відкрити або записати файл. Перевірте вибраний шлях.",
                _ => "Файл містить некоректний шлях або значення параметра сценарію."
            };
            MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
