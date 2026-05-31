using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace QuickTranslate.Services.Input;

public class UiAutomationService : IUiAutomationService
{
    public string? TryGetSelectedText()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element == null)
                return null;

            var textPattern = GetTextPattern(element);
            if (textPattern == null)
                return null;

            var selection = textPattern.GetSelection();
            if (selection == null || selection.Length == 0)
                return null;

            // Collapsed selection (cursor only, no text highlighted)
            var range = selection[0];
            if (range.CompareEndpoints(
                    TextPatternRangeEndpoint.Start, range,
                    TextPatternRangeEndpoint.End) == 0)
                return null;

            return range.GetText(-1);
        }
        catch
        {
            return null;
        }
    }

    private static TextPattern? GetTextPattern(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) &&
            pattern is TextPattern tp)
            return tp;

        // Some apps have TextPattern on the parent container
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        if (parent != null &&
            parent.TryGetCurrentPattern(TextPattern.Pattern, out pattern) &&
            pattern is TextPattern tp2)
            return tp2;

        return null;
    }
}
