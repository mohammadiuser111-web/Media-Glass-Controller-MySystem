using System.IO;
using System.Text.Json;
using MediaGlass.Models;

namespace MediaGlass.Services;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(appData, "MySystem", "MediaGlass", "settings.json");
    }

    public WidgetSettings Load()
    {
        try
        {
            return File.Exists(_settingsPath)
                ? JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(_settingsPath), JsonOptions) ?? new WidgetSettings()
                : new WidgetSettings();
        }
        catch (JsonException)
        {
            return new WidgetSettings();
        }
        catch (IOException)
        {
            return new WidgetSettings();
        }
    }

    public void Save(WidgetSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_settingsPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (IOException)
        {
            // Settings persistence must never prevent the widget from closing.
        }
        catch (UnauthorizedAccessException)
        {
            // The widget continues with in-memory settings if the profile is read-only.
        }
    }
}
