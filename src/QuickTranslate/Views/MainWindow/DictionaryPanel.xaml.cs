using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickTranslate.Views;

public partial class DictionaryPanel : System.Windows.Controls.UserControl
{
    public DictionaryPanel()
    {
        InitializeComponent();
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = sender as ScrollViewer;
        if (scrollViewer == null) return;

        if (e.Delta > 0)
            scrollViewer.LineUp();
        else
            scrollViewer.LineDown();

        e.Handled = true;
    }
}
