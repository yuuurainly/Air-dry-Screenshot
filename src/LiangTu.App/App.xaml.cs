using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiangTu.App.Data;
using LiangTu.App.Infrastructure;
using LiangTu.App.Services;
using LiangTu.App.Windows;
using LiangTu.Core;

namespace LiangTu.App;

public partial class App : Application
{
    private AppPaths _paths = null!;
    private SettingsService _settings = null!;
    private ImageRepository _repository = null!;
    private ImageLibraryService _library = null!;
    private ClipboardService _clipboard = null!;
    private ClipboardMonitor _clipboardMonitor = null!;
    private HotkeyService _hotkeys = null!;
    private ToastService _toast = null!;
    private PinManager _pinManager = null!;
    private TrayService _tray = null!;
    private RopeWindow _rope = null!;
    private EventWaitHandle? _showRequestedEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private bool _captureInProgress;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        _showRequestedEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            "Local\\LiangTu.ShowRequested",
            out var createdNew);
        if (!createdNew)
        {
            _showRequestedEvent.Set();
            _showRequestedEvent.Dispose();
            _showRequestedEvent = null;
            Shutdown();
            return;
        }

        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showRequestedEvent,
            (_, _) => Dispatcher.BeginInvoke((Action)ActivateExistingInstance),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        try
        {
            _paths = new AppPaths();
            _paths.EnsureDirectories();
            _settings = new SettingsService(_paths);
            _settings.Current.Normalize();

            _repository = new ImageRepository(_paths.DatabasePath);
            _library = new ImageLibraryService(
                _settings,
                _repository,
                new ThumbnailService(_paths.ThumbnailDirectory),
                _paths);
            await _library.InitializeAsync();
            await _library.RecoverPendingEditAsync();

            _clipboard = new ClipboardService();
            _clipboardMonitor = new ClipboardMonitor();
            _hotkeys = new HotkeyService();
            _toast = new ToastService();
            _pinManager = new PinManager(_repository, _library);
            _pinManager.NotificationRequested += (_, message) => _toast.Show(message);

            _rope = new RopeWindow(_library, _settings, _clipboard, _hotkeys, _clipboardMonitor);
            MainWindow = _rope;
            WireRopeEvents();
            _rope.Show();

            await _pinManager.RestoreAsync();

            _tray = new TrayService(
                _rope.IsExpanded,
                _settings.Current.AutoCollect,
                StartupRegistration.IsEnabled());
            WireTrayEvents();
            _settings.SettingsChanged += Settings_SettingsChanged;
        }
        catch (Exception ex)
        {
            LogException(ex);
            System.Windows.MessageBox.Show(
                $"晾图启动失败：{ex.Message}",
                "晾图",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_settings is not null)
        {
            _settings.SettingsChanged -= Settings_SettingsChanged;
        }

        _pinManager?.Shutdown();
        _hotkeys?.Dispose();
        _clipboardMonitor?.Dispose();
        _tray?.Dispose();
        _activationRegistration?.Unregister(null);
        _showRequestedEvent?.Dispose();

        base.OnExit(e);
    }

    private void ActivateExistingInstance()
    {
        if (_exiting)
        {
            return;
        }

        _rope?.ShowRope();
        _tray?.SetRopeState(true);
    }

    private void WireRopeEvents()
    {
        _rope.EditRequested += async (_, asset) => await OpenEditorAsync(asset);
        _rope.DetachRequested += async (_, request) => await DetachImageAsync(request);
        _rope.ToastRequested += (_, message) => _toast.Show(message);
        _rope.RegionScreenshotRequested += async (_, _) => await CaptureRegionAsync();
        _rope.WindowScreenshotRequested += async (_, _) => await CaptureWindowAsync();
        _rope.PinClipboardRequested += async (_, _) => await PinClipboardAsync();
        _rope.LibraryRequested += (_, _) => OpenLibraryWindow();
    }

    private void WireTrayEvents()
    {
        _tray.ToggleRopeRequested += (_, _) =>
        {
            if (_rope.IsExpanded)
            {
                _rope.HideImmediately();
            }
            else
            {
                _rope.ShowRope();
            }

            _tray.SetRopeState(_rope.IsExpanded);
        };
        _tray.SettingsRequested += (_, _) => OpenSettingsWindow();
        _tray.AutoCollectToggleRequested += (_, _) =>
        {
            var settings = _settings.Clone();
            settings.AutoCollect = !settings.AutoCollect;
            _settings.Save(settings);
        };
        _tray.StartupToggleRequested += (_, _) =>
        {
            var settings = _settings.Clone();
            settings.StartWithWindows = !settings.StartWithWindows;
            _settings.Save(settings);
        };
        _tray.ImportRequested += async (_, _) => await ImportFilesAsync();
        _tray.OpenFolderRequested += (_, _) => OpenScreenshotDirectory();
        _tray.LibraryRequested += (_, _) => OpenLibraryWindow();
        _tray.ExitRequested += (_, _) => ExitApplication();
    }

    private async Task OpenPreviewAsync(ImageAsset asset)
    {
        _rope.PauseAutoHide();
        try
        {
            var image = await _library.LoadFullImageAsync(asset);
            var preview = new PreviewWindow(asset, image) { Owner = _rope };
            preview.ShowDialog();
        }
        catch (Exception ex)
        {
            _toast.Show($"无法预览：{ex.Message}");
        }
        finally
        {
            _rope.ResumeAutoHide();
        }
    }

    private async Task OpenEditorAsync(ImageAsset asset, Window? owner = null)
    {
        _rope.PauseAutoHide();
        try
        {
            var image = await _library.LoadFullImageAsync(asset);
            var editor = new EditorWindow(_library, asset, image)
            {
                Owner = owner ?? _rope,
            };
            editor.ShowDialog();
        }
        catch (Exception ex)
        {
            _toast.Show($"无法打开标注：{ex.Message}");
        }
        finally
        {
            _rope.ResumeAutoHide();
        }
    }

    private async Task CaptureRegionAsync()
    {
        if (_captureInProgress)
        {
            return;
        }

        _captureInProgress = true;
        try
        {
            _rope.HideImmediately();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(100);
            var monitor = ScreenGeometry.GetMonitorFromCursor();
            var background = ScreenCaptureService.Capture(monitor.Bounds);
            var captured = await ShowRegionCaptureAsync(monitor, background);
            if (captured is not null)
            {
                await _library.ImportBitmapAsync(captured, ImageSourceType.Screenshot);
                _toast.Show("截图已保存");
            }
        }
        catch (Exception ex)
        {
            _toast.Show($"区域截图失败：{ex.Message}");
        }
        finally
        {
            _captureInProgress = false;
            _rope.ShowRope();
            _tray?.SetRopeState(true);
        }
    }

    private async Task CaptureWindowAsync()
    {
        if (_captureInProgress)
        {
            return;
        }

        _captureInProgress = true;
        try
        {
            _rope.HideImmediately();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(100);
            var monitor = ScreenGeometry.GetMonitorFromCursor();
            var background = ScreenCaptureService.Capture(monitor.Bounds);
            var captureWindow = new WindowCaptureWindow(monitor, background);
            captureWindow.ShowDialog();
            var captured = captureWindow.CapturedImage;
            if (captured is null)
            {
                _toast.Show("未识别到窗口，请拖动选择区域");
                captured = await ShowRegionCaptureAsync(monitor, background);
            }

            if (captured is not null)
            {
                await _library.ImportBitmapAsync(captured, ImageSourceType.Screenshot);
                _toast.Show("截图已保存");
            }
        }
        catch (Exception ex)
        {
            _toast.Show($"窗口截图失败：{ex.Message}");
        }
        finally
        {
            _captureInProgress = false;
            _rope.ShowRope();
            _tray?.SetRopeState(true);
        }
    }

    private async Task<BitmapSource?> ShowRegionCaptureAsync(
        MonitorGeometry monitor,
        BitmapSource background)
    {
        var window = new RegionCaptureWindow(monitor, background);
        window.ShowDialog();
        await Task.CompletedTask;
        return window.CapturedImage;
    }

    private async Task PinClipboardAsync()
    {
        try
        {
            if (!_clipboard.TryReadImage(out var image) || image is null)
            {
                _toast.Show("剪贴板中没有图片");
                return;
            }

            await _pinManager.PinClipboardImageAsync(image);
        }
        catch (Exception ex)
        {
            _toast.Show($"贴图失败：{ex.Message}");
        }
    }

    private async Task DetachImageAsync(RopeDetachRequest request)
    {
        try
        {
            await _pinManager.DetachToPinAsync(request.Asset, request.ScreenX, request.ScreenY);
        }
        catch (Exception ex)
        {
            _toast.Show($"无法取下图片：{ex.Message}");
        }
    }

    private async Task ImportFilesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入图片",
            Multiselect = true,
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*",
        };
        if (dialog.ShowDialog(_rope) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            try
            {
                await _library.ImportFileAsync(file);
            }
            catch (Exception ex)
            {
                _toast.Show($"无法导入 {Path.GetFileName(file)}：{ex.Message}");
            }
        }

        _rope.ShowRope();
    }

    private void OpenSettingsWindow()
    {
        _rope.PauseAutoHide();
        try
        {
            var settingsWindow = new SettingsWindow(_settings) { Owner = _rope };
            settingsWindow.ShowDialog();
        }
        finally
        {
            _rope.ResumeAutoHide();
        }
    }

    private void OpenLibraryWindow()
    {
        _rope.PauseAutoHide();
        try
        {
            var libraryWindow = new LibraryWindow(_library, _clipboard) { Owner = _rope };
            libraryWindow.ToastRequested += (_, message) => _toast.Show(message);
            libraryWindow.PreviewRequested += async (_, asset) => await OpenPreviewAsync(asset);
            libraryWindow.EditRequested += async (_, asset) => await OpenEditorAsync(asset, libraryWindow);
            libraryWindow.PinRequested += async (_, asset) => await _pinManager.PinAssetAsync(asset);
            libraryWindow.ShowDialog();
        }
        finally
        {
            _rope.ResumeAutoHide();
        }
    }

    private void OpenScreenshotDirectory()
    {
        try
        {
            Directory.CreateDirectory(_settings.Current.ScreenshotsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _settings.Current.ScreenshotsDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _toast.Show($"无法打开图片目录：{ex.Message}");
        }
    }

    private void Settings_SettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            StartupRegistration.SetEnabled(_settings.Current.StartWithWindows);
            _tray?.SetAutoCollect(_settings.Current.AutoCollect);
            _tray?.SetStartup(_settings.Current.StartWithWindows);
            _rope.ApplySettings();
        }
        catch (Exception ex)
        {
            _toast.Show($"应用设置失败：{ex.Message}");
        }
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _rope.Close();
        Shutdown();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        _toast?.Show($"发生错误：{e.Exception.Message}");
        e.Handled = true;
    }

    private void LogException(Exception exception)
    {
        try
        {
            if (_paths is null)
            {
                return;
            }

            File.AppendAllText(
                Path.Combine(_paths.RootDirectory, "errors.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

}
