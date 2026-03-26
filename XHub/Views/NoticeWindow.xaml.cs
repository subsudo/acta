using System.Windows;

namespace XHub.Views;

public partial class NoticeWindow : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme);
    }

    public NoticeWindow(string title, string message, string? subtitle = null)
    {
        InitializeComponent();
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            SubtitleTextBlock.Text = subtitle;
        }
    }

    public static void ShowNotice(Window? owner, string title, string message, string? subtitle = null)
    {
        var dialog = new NoticeWindow(title, message, subtitle);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
