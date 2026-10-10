namespace MediaGlass.Models;

internal sealed class WidgetSettings
{
    public double Left { get; set; } = double.NaN;

    public double Top { get; set; } = double.NaN;

    public double Width { get; set; } = 470;

    public double Height { get; set; } = 176;

    public bool IsPositionLocked { get; set; }
}
