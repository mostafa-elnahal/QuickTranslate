using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using QuickTranslate.ViewModels;

namespace QuickTranslate.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private GridLength _lastPanelHeight = new(250);

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        SourceTextBox.Focus();

        // Listen for panel expansion changes to reset row height
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Intercept closing to hide to tray instead of closing
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
            _viewModel.IsVisible = false;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsBottomPanelExpanded))
        {
            if (_viewModel.IsBottomPanelExpanded)
                PanelContentRow.Height = _lastPanelHeight;
            else
            {
                _lastPanelHeight = PanelContentRow.Height;
                PanelContentRow.Height = new GridLength(0);
            }
        }
    }

    private void PanelResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double maxHeight = GetAvailableContentHeight();
        double currentHeight = Math.Max(PanelContentBorder.ActualHeight, 250);
        double newHeight = currentHeight - e.VerticalChange;
        newHeight = Math.Max(250, Math.Min(maxHeight, newHeight));
        _lastPanelHeight = new GridLength(newHeight);
        PanelContentRow.Height = _lastPanelHeight;
    }

    private double GetAvailableContentHeight()
    {
        var border = Content as Border;
        var outerGrid = border?.Child as Grid;
        if (outerGrid == null) return 500;

        double titleBar = outerGrid.RowDefinitions[0].ActualHeight;
        double langBar = outerGrid.RowDefinitions[1].ActualHeight;
        double header = 36;
        double thumb = 8;
        return Math.Max(250, outerGrid.ActualHeight - titleBar - langBar - header - thumb - 4);
    }

    private void RootBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Clip = new RectangleGeometry(
                new Rect(0, 0, border.ActualWidth, border.ActualHeight), 8, 8);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Center on screen
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        _viewModel.IsMaximized = WindowState == WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _viewModel.IsVisible = false;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        _viewModel.IsMaximized = WindowState == WindowState.Maximized;
    }

    private void SourceTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        string? word = GetWordAtSelection(textBox);
        if (!string.IsNullOrWhiteSpace(word))
        {
            _viewModel.LookupWord(word);
        }
    }

    private void TargetTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        string? word = GetWordAtSelection(textBox);
        if (!string.IsNullOrWhiteSpace(word))
        {
            _viewModel.LookupWord(word);
        }
    }

    private void SourceTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Select all when tabbing into the text box
        if (e.KeyboardDevice.IsKeyDown(Key.Tab))
        {
            SourceTextBox.SelectAll();
        }
    }

    private static string? GetWordAtSelection(TextBox textBox)
    {
        int caretIndex = textBox.CaretIndex;
        string text = textBox.Text;

        if (string.IsNullOrEmpty(text) || caretIndex < 0 || caretIndex > text.Length)
            return null;

        // Find word boundaries
        int start = caretIndex;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]) && !char.IsPunctuation(text[start - 1]))
            start--;

        int end = caretIndex;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && !char.IsPunctuation(text[end]))
            end++;

        if (start >= end)
            return null;

        string word = text[start..end].Trim();
        return string.IsNullOrWhiteSpace(word) ? null : word;
    }
}
