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
    private const int DisplayLimit = 50;
    private readonly Queue<LogDisplayItem> _recent = new();
    private readonly Queue<LogDisplayItem> _radio = new();
    private readonly Queue<LogDisplayItem> _alerts = new();

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

    public void AddLog(string tag, string sender, string message, bool isAlert = false, bool isRadio = false,
        DateTime? time = null)
    {
        var item = new LogDisplayItem(
            (time ?? DateTime.UtcNow).ToString("HH:mm:ss"),
            tag,
            sender,
            message,
            isAlert,
            isRadio);

        Remember(_recent, item);
        if (isRadio) Remember(_radio, item);
        if (isAlert) Remember(_alerts, item);
        if (MatchesFilter(item))
        {
            FilteredLogs.Insert(0, item);
            if (FilteredLogs.Count > DisplayLimit) FilteredLogs.RemoveAt(DisplayLimit);
        }
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

        AddLog(tag, log.AirportIcao ?? "RADAR CONTROL", log.Message, isRadio: isRadio, time: log.Timestamp);
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();
        var history = CurrentFilter switch { "RADIO" => _radio, "ALERTS" => _alerts, _ => _recent };
        foreach (var item in history.Reverse())
        {
            FilteredLogs.Add(item);
        }
    }

    private static void Remember(Queue<LogDisplayItem> history, LogDisplayItem item)
    {
        if (history.Count == DisplayLimit) history.Dequeue();
        history.Enqueue(item);
    }

    private bool MatchesFilter(LogDisplayItem item) => CurrentFilter switch
    {
        "RADIO" => item.IsRadio,
        "ALERTS" => item.IsAlert,
        _ => true
    };
}
