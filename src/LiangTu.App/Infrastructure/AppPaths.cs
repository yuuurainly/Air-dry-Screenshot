namespace LiangTu.App.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? rootDirectory = null)
    {
        var configuredRoot = Environment.GetEnvironmentVariable("LIANGTU_DATA_DIR");
        RootDirectory = rootDirectory
                        ?? (!string.IsNullOrWhiteSpace(configuredRoot)
                            ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredRoot))
                            : Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "LiangTu"));
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        ThumbnailDirectory = Path.Combine(CacheDirectory, "thumbnails");
        TemporaryDirectory = Path.Combine(CacheDirectory, "temporary");
        DatabasePath = Path.Combine(RootDirectory, "liangtu.db");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
        RecoveryPath = Path.Combine(RootDirectory, "pending-edit.json");
    }

    public string RootDirectory { get; }

    public string CacheDirectory { get; }

    public string ThumbnailDirectory { get; }

    public string TemporaryDirectory { get; }

    public string DatabasePath { get; }

    public string SettingsPath { get; }

    public string RecoveryPath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ThumbnailDirectory);
        Directory.CreateDirectory(TemporaryDirectory);
    }
}
