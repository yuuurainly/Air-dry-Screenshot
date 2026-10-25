using System.Text.Json;
using LiangTu.App.Services;

namespace LiangTu.App.Infrastructure;

public sealed class SettingsService : AppSettingsAccessor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _settingsPath;

    public SettingsService(AppPaths paths)
    {
        _settingsPath = paths.SettingsPath;
        Current = Load();
    }

    public AppSettings Current { get; }

    public event EventHandler? SettingsChanged;

    public AppSettings Clone()
    {
        return JsonSerializer.Deserialize<AppSettings>(
                   JsonSerializer.Serialize(Current, SerializerOptions),
                   SerializerOptions)
               ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));

        if (File.Exists(_settingsPath))
        {
            File.Replace(temporaryPath, _settingsPath, null);
        }
        else
        {
            File.Move(temporaryPath, _settingsPath);
        }

        CopySettings(settings, Current);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(_settingsPath),
                SerializerOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        target.ScreenshotsDirectory = source.ScreenshotsDirectory;
        target.AutoCollect = source.AutoCollect;
        target.AutoHide = source.AutoHide;
        target.AutoHideSeconds = source.AutoHideSeconds;
        target.RopeAnimationEnabled = source.RopeAnimationEnabled;
        target.RegionHotkey = source.RegionHotkey;
        target.WindowHotkey = source.WindowHotkey;
        target.PinHotkey = source.PinHotkey;
        target.StartWithWindows = source.StartWithWindows;
    }
}
