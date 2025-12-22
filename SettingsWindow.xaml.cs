using System.Windows;
using System.Windows.Input;
using QuickTranslate.ViewModels;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close request
        viewModel.RequestClose += (s, saved) => Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Allow dragging the window from the title bar area
        if (e.GetPosition(this).Y < 40)
        {
            DragMove();
        }
    }

    private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.FontSize < 48)
        {
            vm.FontSize++;
        }
    }

    private void DecreaseFontSize_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.FontSize > 8)
        {
            vm.FontSize--;
        }
    }
}
