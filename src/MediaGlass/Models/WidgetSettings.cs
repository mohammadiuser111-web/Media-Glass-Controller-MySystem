namespace MediaGlass.Models;

internal sealed class WidgetSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public double Left { get; set; } = double.NaN;

    public double Top { get; set; } = double.NaN;

    public double Width { get; set; } = 330;

    public double Height { get; set; } = 112;

    public bool IsPositionLocked { get; set; }
}
