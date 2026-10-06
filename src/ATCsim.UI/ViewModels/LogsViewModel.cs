using System.Collections.ObjectModel;
using System.Windows.Input;
using ATCsim.Core.Entities;

namespace ATCsim.UI.ViewModels;

public record LogDisplayItem(
    string Time,
    string Tag,
    string Sender,
    string Message,
    bool IsAlert,
    bool IsRadio);

public class LogsViewModel : ViewModelBase
{
    private string _currentFilter = "ALL";
    private readonly List<LogDisplayItem> _allLogs = [];

    public ObservableCollection<LogDisplayItem> FilteredLogs { get; } = [];

    public string CurrentFilter
    {
        get => _currentFilter;
        set
        {
            if (SetProperty(ref _currentFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand FilterAllCommand { get; }
    public ICommand FilterRadioCommand { get; }
    public ICommand FilterAlertsCommand { get; }

    public LogsViewModel()
    {
        FilterAllCommand = new RelayCommand(() => CurrentFilter = "ALL");
        FilterRadioCommand = new RelayCommand(() => CurrentFilter = "RADIO");
        FilterAlertsCommand = new RelayCommand(() => CurrentFilter = "ALERTS");
    }

    public void AddLog(string tag, string sender, string message, bool isAlert = false, bool isRadio = false)
    {
        var item = new LogDisplayItem(
            DateTime.UtcNow.ToString("HH:mm:ss"),
            tag,
            sender,
            message,
            isAlert,
            isRadio);

        _allLogs.Insert(0, item);
        ApplyFilter();
    }

    public void AddLog(CommunicationLog log)
    {
        bool isRadio = log.SenderType is "ATC" or "AIRCRAFT";
        string tag = log.SenderType switch
        {
            "ATC" => "• ATC RADIO",
            "AIRCRAFT" => "• READBACK",
            _ => "• ATC RADAR"
        };

        AddLog(tag, log.AirportIcao ?? "RADAR CONTROL", log.Message, isAlert: false, isRadio: isRadio);
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();
        var matches = CurrentFilter switch
        {
            "RADIO" => _allLogs.Where(x => x.IsRadio),
            "ALERTS" => _allLogs.Where(x => x.IsAlert),
            _ => _allLogs
        };

        foreach (var item in matches.Take(50))
        {
            FilteredLogs.Add(item);
        }
    }
}
