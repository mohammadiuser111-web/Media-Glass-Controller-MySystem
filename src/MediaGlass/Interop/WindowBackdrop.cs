using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MediaGlass.Interop;

internal static class WindowBackdrop
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;
    private const int RoundedCorners = 2;
    private const int AcrylicBackdrop = 3;

    public static void Apply(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: { } target })
        {
            target.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
        }

        int enabled = 1;
        int corners = RoundedCorners;
        int backdrop = AcrylicBackdrop;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref corners, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdrop, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
