using System.Collections.ObjectModel;  // ObservableCollection
using Silencer.Models;                 // LogEntry
using Silencer.Services;               // LoggingService

namespace Silencer.Pages;

public partial class ConsolePage : ContentPage
{
    public ConsolePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<LogEntry> Logs => LoggingService.Logs;

    private void OnClearLogsClicked(object sender, EventArgs e)
    {
        LoggingService.Logs.Clear();
        LoggingService.LogInfo("Console cleared");
    }
}