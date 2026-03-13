using System.Windows;

namespace XHub.Views;

public partial class AttendanceImportWindow : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme);
    }

    public AttendanceImportWindow()
    {
        InitializeComponent();
    }

    public string RawText => RawInputTextBox.Text;

    private void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
