using System.Windows;
using System.Windows.Input;

namespace QuickTranslate.Views;

public partial class CopyButton : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty IsCopiedProperty =
        DependencyProperty.Register(nameof(IsCopied), typeof(bool), typeof(CopyButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(CopyButton));

    public bool IsCopied
    {
        get => (bool)GetValue(IsCopiedProperty);
        set => SetValue(IsCopiedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public CopyButton()
    {
        InitializeComponent();
    }
}
