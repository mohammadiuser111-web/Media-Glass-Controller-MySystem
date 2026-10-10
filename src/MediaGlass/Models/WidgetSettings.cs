namespace MediaGlass.Models;

internal sealed class WidgetSettings
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public double Left { get; set; } = double.NaN;

    public double Top { get; set; } = double.NaN;

    public double Width { get; set; } = 300;

    public double Height { get; set; } = 100;

    public bool IsPositionLocked { get; set; }
}
