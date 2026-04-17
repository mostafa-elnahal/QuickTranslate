using System.Windows;

namespace QuickTranslate.Services;

public interface IWindowPositioningService
{
    /// <summary>
    /// Positions the specified window near the current mouse cursor, 
    /// ensuring it stays within the visible screen bounds.
    /// </summary>
    /// <param name="window">The window to position.</param>
    void PositionNearCursor(Window window);
}
