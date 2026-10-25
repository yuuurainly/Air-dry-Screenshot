using System.Windows;
using System.Windows.Media.Imaging;
using LiangTu.App.Data;
using LiangTu.App.Windows;
using LiangTu.Core;

namespace LiangTu.App.Services;

public sealed class PinManager
{
    private readonly ImageRepository _repository;
    private readonly ImageLibraryService _library;
    private readonly Dictionary<Guid, PinWindow> _windows = [];
    private int _cascadeIndex;
    private bool _shuttingDown;

    public PinManager(ImageRepository repository, ImageLibraryService library)
    {
        _repository = repository;
        _library = library;
        _library.AssetUpdated += Library_AssetUpdated;
        _library.AssetDeleted += Library_AssetDeleted;
    }

    public event EventHandler<string>? NotificationRequested;

    public async Task RestoreAsync()
    {
        var states = await Task.Run(_repository.GetPinInstances);
        var ropeImageIds = (await _library.GetRopeItemsAsync())
            .Select(item => item.Image.Id)
            .ToHashSet();
        foreach (var state in states)
        {
            if (state.ImageId is null)
            {
                continue;
            }

            var asset = await _library.GetAssetAsync(state.ImageId.Value);
            if (asset is null)
            {
                continue;
            }

            if (!File.Exists(asset.FilePath))
            {
                continue;
            }

            try
            {
                var image = await _library.LoadFullImageAsync(asset);
                OpenWindow(
                    state.Id,
                    state.ImageId,
                    image,
                    state,
                    showReturnToRope: !ropeImageIds.Contains(asset.Id));
            }
            catch (IOException)
            {
            }
        }
    }

    public async Task PinClipboardImageAsync(BitmapSource image)
    {
        var matchingAsset = await _library.FindMatchingAssetAsync(image);
        var showReturnToRope = matchingAsset is not null
                               && !await IsAssetOnRopeAsync(matchingAsset.Id);
        OpenNew(matchingAsset?.Id, image, showReturnToRope);
    }

    public async Task PinAssetAsync(ImageAsset asset)
    {
        var image = await _library.LoadFullImageAsync(asset);
        OpenNew(asset.Id, image, !await IsAssetOnRopeAsync(asset.Id));
    }

    public async Task DetachToPinAsync(ImageAsset asset, int screenX, int screenY)
    {
        var image = await _library.LoadFullImageAsync(asset);
        await _library.RemoveAssetFromRopeAsync(asset.Id);
        try
        {
            var monitor = Infrastructure.ScreenGeometry.GetMonitorFromPoint(screenX, screenY);
            var width = Math.Min(600, Math.Max(280, image.PixelWidth / 1.5));
            var height = Math.Min(480, Math.Max(180, image.PixelHeight / 1.5));
            var left = Math.Clamp(
                (screenX / monitor.Scale) - (width / 2),
                monitor.WorkArea.Left / monitor.Scale,
                Math.Max(monitor.WorkArea.Left / monitor.Scale, (monitor.WorkArea.Right / monitor.Scale) - width));
            var top = Math.Clamp(
                (screenY / monitor.Scale) - 24,
                monitor.WorkArea.Top / monitor.Scale,
                Math.Max(monitor.WorkArea.Top / monitor.Scale, (monitor.WorkArea.Bottom / monitor.Scale) - height));
            var state = new PinInstanceState(
                Guid.NewGuid(),
                asset.Id,
                left,
                top,
                width,
                height,
                1,
                true,
                DateTimeOffset.UtcNow);
            OpenWindow(
                state.Id,
                asset.Id,
                image,
                state,
                persistImmediately: true,
                showReturnToRope: true);
        }
        catch
        {
            await _library.PromoteToRopeAsync(asset);
            throw;
        }
    }

    public void Shutdown()
    {
        _shuttingDown = true;
        foreach (var window in _windows.Values.ToList())
        {
            _repository.UpsertPin(window.State);
            window.Close();
        }

        _library.AssetUpdated -= Library_AssetUpdated;
        _library.AssetDeleted -= Library_AssetDeleted;
    }

    private void OpenNew(
        Guid? imageId,
        BitmapSource image,
        bool showReturnToRope = false)
    {
        var monitor = Infrastructure.ScreenGeometry.GetMonitorFromCursor();
        var width = Math.Min(560, Math.Max(280, image.PixelWidth / 1.5));
        var height = Math.Min(460, Math.Max(180, image.PixelHeight / 1.5));
        var offset = (_cascadeIndex++ % 8) * 24;
        var x = (monitor.WorkArea.Left / monitor.Scale)
                + ((monitor.WorkArea.Width / monitor.Scale) - width - 32 - offset);
        var y = (monitor.WorkArea.Top / monitor.Scale)
                + ((monitor.WorkArea.Height / monitor.Scale) - height - 32 - offset);
        var state = new PinInstanceState(
            Guid.NewGuid(),
            imageId,
            Math.Max(0, x),
            Math.Max(0, y),
            width,
            height,
            1,
            true,
            DateTimeOffset.UtcNow);
        OpenWindow(
            state.Id,
            imageId,
            image,
            state,
            persistImmediately: true,
            showReturnToRope: showReturnToRope);
    }

    private void OpenWindow(
        Guid pinId,
        Guid? imageId,
        BitmapSource image,
        PinInstanceState state,
        bool persistImmediately = false,
        bool showReturnToRope = false)
    {
        var window = new PinWindow(pinId, imageId, image, state, showReturnToRope);
        _windows[pinId] = window;
        window.PinStateChanged += PinWindow_StateChanged;
        window.ReturnToRopeRequested += PinWindow_ReturnToRopeRequested;
        window.Closed += PinWindow_Closed;
        window.Show();
        if (persistImmediately)
        {
            _repository.UpsertPin(state);
        }
    }

    private async Task<bool> IsAssetOnRopeAsync(Guid imageId)
    {
        var ropeItems = await _library.GetRopeItemsAsync();
        return ropeItems.Any(item => item.Image.Id == imageId);
    }

    private void PinWindow_StateChanged(object? sender, EventArgs e)
    {
        if (sender is PinWindow window)
        {
            _repository.UpsertPin(window.State);
        }
    }

    private void PinWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not PinWindow window)
        {
            return;
        }

        window.PinStateChanged -= PinWindow_StateChanged;
        window.ReturnToRopeRequested -= PinWindow_ReturnToRopeRequested;
        window.Closed -= PinWindow_Closed;
        _windows.Remove(window.Id);

        if (window.ImageId is not null && !_shuttingDown)
        {
            _repository.DeletePin(window.Id);
        }
    }

    private async void PinWindow_ReturnToRopeRequested(object? sender, EventArgs e)
    {
        if (sender is not PinWindow { ImageId: Guid imageId } window)
        {
            return;
        }

        var asset = await _library.GetAssetAsync(imageId);
        if (asset is null)
        {
            return;
        }

        try
        {
            await _library.PromoteToRopeAsync(asset);
            window.Close();
        }
        catch (Exception ex)
        {
            NotificationRequested?.Invoke(this, $"无法钉回晾衣绳：{ex.Message}");
        }
    }

    private async void Library_AssetUpdated(object? sender, ImageAsset asset)
    {
        foreach (var window in _windows.Values.Where(window => window.ImageId == asset.Id))
        {
            try
            {
                var image = await _library.LoadFullImageAsync(asset);
                window.Dispatcher.Invoke(() => window.UpdateImage(image));
            }
            catch (IOException)
            {
            }
        }
    }

    private void Library_AssetDeleted(object? sender, ImageAsset asset)
    {
        foreach (var window in _windows.Values.Where(window => window.ImageId == asset.Id).ToList())
        {
            window.Dispatcher.Invoke(window.Close);
        }
    }
}
