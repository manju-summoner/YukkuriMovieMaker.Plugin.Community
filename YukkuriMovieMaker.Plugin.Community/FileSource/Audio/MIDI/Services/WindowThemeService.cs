using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

internal static class WindowThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_BORDER_COLOR = 34;

    public static void Bind(Window window)
    {
        if (window is null) return;
        window.SourceInitialized += (_, _) => ApplyCurrentTheme(window);
        window.Loaded += (_, _) => ApplyCurrentTheme(window);
    }

    private static void ApplyCurrentTheme(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var captionBrush = (window.TryFindResource(SystemColors.ControlBrushKey) as SolidColorBrush) ?? Brushes.White;
        var textBrush = (window.TryFindResource(SystemColors.WindowTextBrushKey) as SolidColorBrush) ?? Brushes.Black;

        SetDwmColor(hwnd, DWMWA_CAPTION_COLOR, captionBrush.Color);
        SetDwmColor(hwnd, DWMWA_BORDER_COLOR, captionBrush.Color);
        SetDwmColor(hwnd, DWMWA_TEXT_COLOR, textBrush.Color);
    }

    private static void SetDwmColor(IntPtr hwnd, int attribute, Color color)
    {
        uint colorRef = (uint)(color.R | (color.G << 8) | (color.B << 16));
        DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(uint));
    }
}
