namespace LiangTu.App.Infrastructure;

public sealed class AppSettings
{
    public string ScreenshotsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Screenshots");

    public bool AutoCollect { get; set; } = true;

    public bool AutoHide { get; set; } = true;

    public int AutoHideSeconds { get; set; } = 120;

    public bool RopeAnimationEnabled { get; set; } = true;

    public string RegionHotkey { get; set; } = "Alt+S";

    public string WindowHotkey { get; set; } = "F1";

    public string PinHotkey { get; set; } = "F3";

    public bool StartWithWindows { get; set; }

    public void Normalize()
    {
        ScreenshotsDirectory = string.IsNullOrWhiteSpace(ScreenshotsDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots")
            : Environment.ExpandEnvironmentVariables(ScreenshotsDirectory.Trim());
        AutoHideSeconds = Math.Clamp(AutoHideSeconds, 10, 3600);
        RegionHotkey = NormalizeHotkey(RegionHotkey, "Alt+S");
        WindowHotkey = NormalizeHotkey(WindowHotkey, "F1");
        PinHotkey = NormalizeHotkey(PinHotkey, "F3");
    }

    private static string NormalizeHotkey(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
