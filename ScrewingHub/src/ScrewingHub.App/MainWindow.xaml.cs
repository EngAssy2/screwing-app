using System.Windows;
using System.Windows.Controls;
using ScrewingHub.App.ViewModels;

namespace ScrewingHub.App;

public partial class MainWindow : Window
{
    public MainWindow(
        DashboardViewModel dashboardVm,
        SettingsViewModel settingsVm,
        HistoryViewModel historyVm)
    {
        InitializeComponent();

        DashboardPage.DataContext = dashboardVm;
        SettingsPage.DataContext = settingsVm;
        HistoryPage.DataContext = historyVm;
    }

    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        // Guard: this event fires during InitializeComponent before views are created
        if (DashboardPage == null || HistoryPage == null || SettingsPage == null)
            return;

        if (sender is RadioButton rb)
        {
            DashboardPage.Visibility = rb.Name == "NavDashboard" ? Visibility.Visible : Visibility.Collapsed;
            HistoryPage.Visibility = rb.Name == "NavHistory" ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = rb.Name == "NavSettings" ? Visibility.Visible : Visibility.Collapsed;

            // Load history data when switching to history tab
            if (rb.Name == "NavHistory" && HistoryPage.DataContext is HistoryViewModel hvm)
            {
                hvm.LoadRecordsCommand.Execute(null);
            }
        }
    }
}