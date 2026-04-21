using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<HistoryViewModel>();
    private readonly ICsvLogService _csvLogService;

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private string _filterJudgment = "All";
    [ObservableProperty] private ObservableCollection<JudgmentResult> _records = new();
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _okRecords;
    [ObservableProperty] private int _ngRecords;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _currentFilePath = string.Empty;

    public List<string> JudgmentFilters { get; } = new() { "All", "OK", "NG" };

    public HistoryViewModel(ICsvLogService csvLogService)
    {
        _csvLogService = csvLogService;
    }

    partial void OnSelectedDateChanged(DateTime value) => _ = LoadRecords();
    partial void OnFilterJudgmentChanged(string value) => _ = LoadRecords();

    [RelayCommand]
    private async Task LoadRecords()
    {
        try
        {
            CurrentFilePath = Path.Combine(_csvLogService.GetLogFilePath(SelectedDate, null).Replace(Path.GetFileName(_csvLogService.GetLogFilePath(SelectedDate, null)), ""));
            var allRecords = await _csvLogService.ReadLogAsync(SelectedDate);

            var filtered = FilterJudgment switch
            {
                "OK" => allRecords.Where(r => r.Judgment == JudgmentStatus.OK).ToList(),
                "NG" => allRecords.Where(r => r.Judgment == JudgmentStatus.NG).ToList(),
                _ => allRecords
            };

            Records.Clear();
            foreach (var rec in filtered)
                Records.Add(rec);

            TotalRecords = allRecords.Count;
            OkRecords = allRecords.Count(r => r.Judgment == JudgmentStatus.OK);
            NgRecords = allRecords.Count(r => r.Judgment == JudgmentStatus.NG);
            StatusMessage = $"Loaded {filtered.Count} records from {SelectedDate:yyyy-MM-dd}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Logger.Error(ex, "Failed to load history for {Date}", SelectedDate);
        }
    }

    [RelayCommand]
    private void OpenCsvFile()
    {
        try
        {
            var folder = Path.Combine(_csvLogService.GetLogFilePath(SelectedDate, null).Replace(Path.GetFileName(_csvLogService.GetLogFilePath(SelectedDate, null)), ""));
            if (Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            else
            {
                StatusMessage = "No log folder exists for this date.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var folder = Path.Combine(_csvLogService.GetLogFilePath(SelectedDate, null).Replace(Path.GetFileName(_csvLogService.GetLogFilePath(SelectedDate, null)), ""));
            if (folder != null && Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
