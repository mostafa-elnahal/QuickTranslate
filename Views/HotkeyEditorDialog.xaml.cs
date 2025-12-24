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

        // Build the hotkey string
        var modifiers = Keyboard.Modifiers;
        var hotkeyParts = new System.Collections.Generic.List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
            hotkeyParts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            hotkeyParts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            hotkeyParts.Add("Shift");

        // Convert key to readable string
        var keyName = key.ToString();

        // Handle special keys
        if (key >= Key.D0 && key <= Key.D9)
            keyName = ((int)key - (int)Key.D0).ToString();
        else if (key >= Key.NumPad0 && key <= Key.NumPad9)
            keyName = "Num " + ((int)key - (int)Key.NumPad0);
        else if (key == Key.Multiply)
            keyName = "Num *";
        else if (key == Key.Add)
            keyName = "Num +";
        else if (key == Key.Subtract)
            keyName = "Num -";
        else if (key == Key.Divide)
            keyName = "Num /";
        else if (key == Key.Decimal)
            keyName = "Num .";
        else if (key == Key.OemPlus)
            keyName = "+";
        else if (key == Key.OemMinus)
            keyName = "-";
        else if (key == Key.OemPeriod)
            keyName = ".";
        else if (key == Key.OemComma)
            keyName = ",";

        hotkeyParts.Add(keyName);
        CapturedHotkey = string.Join("+", hotkeyParts);
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
