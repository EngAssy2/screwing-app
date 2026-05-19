using System.Windows;

namespace ScrewingHub.App.Views;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(string message, string title = "Confirmation")
    {
        InitializeComponent();
        MessageText.Text = message;
        TitleText.Text = title;
        
        // Try to set the owner to the main window
        if (Application.Current.MainWindow != null && Application.Current.MainWindow != this)
        {
            this.Owner = Application.Current.MainWindow;
        }
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static bool Show(string message, string title = "Confirmation")
    {
        try
        {
            var dialog = new ConfirmationDialog(message, title);
            dialog.Topmost = true; // Ensure it stays on top
            return dialog.ShowDialog() == true;
        }
        catch (System.Exception ex)
        {
            // Fallback to standard MessageBox if custom dialog fails
            System.Diagnostics.Debug.WriteLine($"Custom dialog failed: {ex}");
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}
