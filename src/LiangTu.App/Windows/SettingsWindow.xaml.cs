using System.Windows;
using LiangTu.App.Infrastructure;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;

    public SettingsWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        var current = settings.Current;
        VersionText.Text = $"v{ProductInfo.Version}";
        DirectoryBox.Text = current.ScreenshotsDirectory;
        RegionHotkeyBox.Text = current.RegionHotkey;
        WindowHotkeyBox.Text = current.WindowHotkey;
        PinHotkeyBox.Text = current.PinHotkey;
        AutoHideSecondsBox.Text = current.AutoHideSeconds.ToString();
        AutoHideCheck.IsChecked = current.AutoHide;
        AutoCollectCheck.IsChecked = current.AutoCollect;
        RopeAnimationCheck.IsChecked = current.RopeAnimationEnabled;
        StartupCheck.IsChecked = current.StartWithWindows;
        AutoHideSecondsBox.IsEnabled = current.AutoHide;
        Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var availableHeight = SystemParameters.WorkArea.Height - 48;
        Height = Math.Max(MinHeight, Math.Min(Height, availableHeight));
    }

    private void AutoHideCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoHideSecondsBox is not null)
        {
            AutoHideSecondsBox.IsEnabled = AutoHideCheck.IsChecked == true;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择截图保存目录",
            InitialDirectory = Directory.Exists(DirectoryBox.Text)
                ? DirectoryBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog(this) == true)
        {
            DirectoryBox.Text = dialog.FolderName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AutoHideSecondsBox.Text, out var seconds) || seconds is < 10 or > 3600)
        {
            System.Windows.MessageBox.Show(
                this,
                "自动隐藏秒数应在 10 到 3600 之间。",
                "设置",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var candidate = _settings.Clone();
        candidate.ScreenshotsDirectory = DirectoryBox.Text;
        candidate.RegionHotkey = RegionHotkeyBox.Text;
        candidate.WindowHotkey = WindowHotkeyBox.Text;
        candidate.PinHotkey = PinHotkeyBox.Text;
        candidate.AutoHideSeconds = seconds;
        candidate.AutoHide = AutoHideCheck.IsChecked == true;
        candidate.AutoCollect = AutoCollectCheck.IsChecked == true;
        candidate.RopeAnimationEnabled = RopeAnimationCheck.IsChecked == true;
        candidate.StartWithWindows = StartupCheck.IsChecked == true;
        candidate.Normalize();

        var error = HotkeyService.Validate(candidate);
        if (error is not null)
        {
            System.Windows.MessageBox.Show(
                this,
                error,
                "快捷键无效",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(candidate.ScreenshotsDirectory);
            _settings.Save(candidate);
            StartupRegistration.SetEnabled(candidate.StartWithWindows);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "保存设置失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
