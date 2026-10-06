// Applies System/Light/Dark by overriding the system-color keys the UI
// binds to. System = remove overrides (follow Windows). Interactive-only.
using System.Windows;

namespace Wff.App;

public static class Theme
{
    public static void Apply(string theme)
    {
        var app = Application.Current;
        if (app == null)
            return;
        app.Resources.Remove(SystemColors.WindowBrushKey);
        app.Resources.Remove(SystemColors.WindowTextBrushKey);
        app.Resources.Remove(SystemColors.HighlightBrushKey);
        app.Resources.Remove(SystemColors.GrayTextBrushKey);
        if (theme == "Light")
        {
            app.Resources.Add(SystemColors.WindowBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White));
            app.Resources.Add(SystemColors.WindowTextBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black));
            app.Resources.Add(SystemColors.HighlightBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)));
            app.Resources.Add(SystemColors.GrayTextBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray));
        }
        else if (theme == "Dark")
        {
            app.Resources.Add(SystemColors.WindowBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E)));
            app.Resources.Add(SystemColors.WindowTextBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)));
            app.Resources.Add(SystemColors.HighlightBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB9, 0x00)));
            app.Resources.Add(SystemColors.GrayTextBrushKey,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray));
        }
    }
}
