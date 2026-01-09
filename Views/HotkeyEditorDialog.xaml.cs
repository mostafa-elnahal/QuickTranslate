using System.Windows;
using System.Windows.Input;

namespace QuickTranslate.Views;

/// <summary>
/// Dialog for capturing and setting a hotkey combination.
/// </summary>
public partial class HotkeyEditorDialog : Window
{
    /// <summary>
    /// Gets or sets the captured hotkey string.
    /// </summary>
    public string CapturedHotkey
    {
        get => (string)GetValue(CapturedHotkeyProperty);
        set => SetValue(CapturedHotkeyProperty, value);
    }

    public static readonly DependencyProperty CapturedHotkeyProperty =
        DependencyProperty.Register(nameof(CapturedHotkey), typeof(string), typeof(HotkeyEditorDialog), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Gets the result hotkey if user clicked Set, otherwise null.
    /// </summary>
    public string? ResultHotkey { get; private set; }

    public HotkeyEditorDialog(string currentHotkey, string actionName = "Set Hotkey")
    {
        InitializeComponent();
        Title = actionName;
        CapturedHotkey = currentHotkey;
        ResultHotkey = null;
        Loaded += (s, e) =>
        {
            HotkeyTextBox.Focus();
            HotkeyTextBox.SelectAll();
        };
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Get the actual key (not system key)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        // Format using helper
        CapturedHotkey = QuickTranslate.Helpers.HotkeyHelper.FormatHotkey(key, modifiers);
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyTextBox.SelectAll();
    }

    private void SetButton_Click(object sender, RoutedEventArgs e)
    {
        ResultHotkey = CapturedHotkey;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultHotkey = null;
        DialogResult = false;
        Close();
    }
}
