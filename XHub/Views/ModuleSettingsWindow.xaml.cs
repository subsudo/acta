using System.Collections.ObjectModel;
using System.Windows;
using XHub.Models;

namespace XHub.Views;

public partial class ModuleSettingsWindow : Window
{
    public ObservableCollection<DetailModuleConfig> Modules { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme);
    }

    public ModuleSettingsWindow(IEnumerable<DetailModuleConfig> modules)
    {
        InitializeComponent();
        Modules = new ObservableCollection<DetailModuleConfig>(modules.Select(module => module.Clone()).OrderBy(module => module.Order));
        DataContext = this;
    }

    public List<DetailModuleConfig> Result => Modules
        .Select((module, index) => new DetailModuleConfig
        {
            Key = module.Key,
            Title = module.Title,
            IsEnabled = module.IsEnabled,
            Order = index
        })
        .ToList();

    private void MoveUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelected(-1);
    }

    private void MoveDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        MoveSelected(1);
    }

    private void MoveSelected(int delta)
    {
        var selected = ModulesListBox.SelectedItem as DetailModuleConfig;
        if (selected is null)
        {
            return;
        }

        var currentIndex = Modules.IndexOf(selected);
        var newIndex = currentIndex + delta;
        if (newIndex < 0 || newIndex >= Modules.Count)
        {
            return;
        }

        Modules.Move(currentIndex, newIndex);
        ModulesListBox.SelectedItem = selected;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
