using System.Text.Json;
using System.Windows.Media.Imaging;
using LiangTu.App.Data;
using LiangTu.App.Infrastructure;
using LiangTu.Core;

namespace LiangTu.App.Services;

public sealed class ImageLibraryService
{
    private readonly AppSettingsAccessor _settings;
    private readonly ImageRepository _repository;
    private readonly ThumbnailService _thumbnails;
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _importGate = new(1, 1);

    public ImageLibraryService(
        AppSettingsAccessor settings,
        ImageRepository repository,
        ThumbnailService thumbnails,
        AppPaths paths)
    {
        _settings = settings;
        _repository = repository;
        _thumbnails = thumbnails;
        _paths = paths;
    }

    public event EventHandler? RopeChanged;

    public event EventHandler<ImageAsset>? AssetUpdated;

    public event EventHandler<ImageAsset>? AssetDeleted;

    public Task InitializeAsync()
    {
        return Task.Run(_repository.Initialize);
    }

    public Task<IReadOnlyList<RopeItem>> GetRopeItemsAsync()
    {
        return Task.Run(_repository.GetRopeItems);
    }

    public Task<IReadOnlyList<ImageAsset>> GetAssetsAsync()
    {
        return Task.Run(_repository.GetAssets);
    }

    public Task<ImageAsset?> GetAssetAsync(Guid id)
    {
        return Task.Run(() => _repository.GetAsset(id));
    }

    public Task<ImageAsset?> FindMatchingAssetAsync(BitmapSource image)
    {
        if (image.CanFreeze)
        {
            image.Freeze();
        }

        return Task.Run(() =>
        {
            var hash = ImageCodec.ComputeContentHash(image);
            return _repository.FindByHash(hash);
        });
    }

    public Task<BitmapSource> GetThumbnailAsync(ImageAsset asset)
    {
        return _thumbnails.GetThumbnailAsync(asset);
    }

    public Task<BitmapSource> LoadFullImageAsync(ImageAsset asset)
    {
        return Task.Run(() => ImageCodec.Load(asset.FilePath));
    }

    public async Task<ImageAsset> ImportBitmapAsync(BitmapSource image, ImageSourceType sourceType)
    {
        if (image.CanFreeze)
        {
            image.Freeze();
        }

        await _importGate.WaitAsync();
        try
        {
            var asset = await Task.Run(() => ImportCore(image, sourceType));
            RopeChanged?.Invoke(this, EventArgs.Empty);
            return asset;
        }
        finally
        {
            _importGate.Release();
        }
    }

    public Task<ImageAsset> ImportFileAsync(string filePath)
    {
        return ImportFileCoreAsync(filePath);
    }

    public async Task MoveRopeItemAsync(Guid ropeItemId, int targetIndex)
    {
        var items = await GetRopeItemsAsync();
        var list = items.ToList();
        var sourceIndex = list.FindIndex(item => item.Id == ropeItemId);
        if (sourceIndex < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, list.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return;
        }

        var item = list[sourceIndex];
        list.RemoveAt(sourceIndex);
        list.Insert(targetIndex, item);
        await Task.Run(() => _repository.SetRopeOrder(list.Select(entry => entry.Id).ToList()));
        RopeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveFromRopeAsync(Guid ropeItemId)
    {
        await Task.Run(() => _repository.RemoveFromRope(ropeItemId));
        RopeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RemoveAssetFromRopeAsync(Guid imageId)
    {
        await Task.Run(() => _repository.RemoveAssetFromRope(imageId));
        RopeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> DeleteAssetsAsync(IReadOnlyCollection<ImageAsset> assets)
    {
        if (assets.Count == 0)
        {
            return 0;
        }

        await _importGate.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                foreach (var asset in assets)
                {
                    _repository.DeleteAsset(asset.Id);
                    TryDelete(asset.FilePath);
                    _thumbnails.DeleteCachedThumbnails(asset.Id);
                }
            });

            foreach (var asset in assets)
            {
                AssetDeleted?.Invoke(this, asset);
            }

            RopeChanged?.Invoke(this, EventArgs.Empty);
            return assets.Count;
        }
        finally
        {
            _importGate.Release();
        }
    }

    public async Task PromoteToRopeAsync(ImageAsset asset)
    {
        await Task.Run(() => _repository.PromoteToRope(asset.Id));
        RopeChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ImageAsset> SaveEditedImageAsync(ImageAsset asset, byte[] pngBytes)
    {
        var result = await Task.Run(() => SaveEditedImageCore(asset, pngBytes));
        AssetUpdated?.Invoke(this, result);
        RopeChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public Task RecoverPendingEditAsync()
    {
        return Task.Run(RecoverPendingEdit);
    }

    private ImageAsset ImportCore(BitmapSource image, ImageSourceType sourceType)
    {
        var contentHash = ImageCodec.ComputeContentHash(image);
        var existing = _repository.FindByHash(contentHash);
        if (existing is not null)
        {
            if (!File.Exists(existing.FilePath))
            {
                WriteNewImageFile(existing.FilePath, ImageCodec.EncodePng(image));
            }

            _repository.PromoteToRope(existing.Id);
            return existing;
        }

        var directory = _settings.Current.ScreenshotsDirectory;
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(
            directory,
            $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
        var pngBytes = ImageCodec.EncodePng(image);
        WriteNewImageFile(filePath, pngBytes);

        var now = DateTimeOffset.UtcNow;
        var asset = new ImageAsset(
            Guid.NewGuid(),
            filePath,
            contentHash,
            now,
            now,
            image.PixelWidth,
            image.PixelHeight,
            sourceType);

        try
        {
            _repository.InsertAsset(asset);
            _repository.PromoteToRope(asset.Id);
            return asset;
        }
        catch
        {
            TryDelete(filePath);
            throw;
        }
    }

    private async Task<ImageAsset> ImportFileCoreAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("图片文件不存在。", filePath);
        }

        var image = await Task.Run(() => ImageCodec.Load(filePath));
        return await ImportBitmapAsync(image, ImageSourceType.FileImport);
    }

    private ImageAsset SaveEditedImageCore(ImageAsset asset, byte[] pngBytes)
    {
        var editedImage = ImageCodec.LoadBytes(pngBytes);
        var newHash = ImageCodec.ComputeContentHash(editedImage);
        var targetPath = asset.FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileNameWithoutExtension(targetPath)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(temporaryPath, pngBytes);

        try
        {
            var verified = ImageCodec.Load(temporaryPath);
            var verifiedHash = ImageCodec.ComputeContentHash(verified);
            if (!string.Equals(verifiedHash, newHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("编辑结果校验失败。");
            }

            WriteRecoveryJournal(new EditRecoveryJournal(
                asset.Id,
                targetPath,
                temporaryPath,
                asset.ContentHash,
                newHash,
                editedImage.PixelWidth,
                editedImage.PixelHeight));

            if (File.Exists(targetPath))
            {
                File.Replace(temporaryPath, targetPath, null);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }

            var modifiedAt = DateTimeOffset.UtcNow;
            _repository.UpdateAssetContent(
                asset.Id,
                newHash,
                modifiedAt,
                editedImage.PixelWidth,
                editedImage.PixelHeight);
            DeleteRecoveryJournal();

            return asset with
            {
                ContentHash = newHash,
                ModifiedAt = modifiedAt,
                PixelWidth = editedImage.PixelWidth,
                PixelHeight = editedImage.PixelHeight,
            };
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private void RecoverPendingEdit()
    {
        if (!File.Exists(_paths.RecoveryPath))
        {
            return;
        }

        EditRecoveryJournal? journal;
        try
        {
            journal = JsonSerializer.Deserialize<EditRecoveryJournal>(File.ReadAllText(_paths.RecoveryPath));
        }
        catch (JsonException)
        {
            return;
        }

        if (journal is null || !File.Exists(journal.TargetPath))
        {
            return;
        }

        var recovery = journal;
        var currentImage = ImageCodec.Load(recovery.TargetPath);
        var currentHash = ImageCodec.ComputeContentHash(currentImage);
        if (string.Equals(currentHash, recovery.NewHash, StringComparison.Ordinal))
        {
            _repository.UpdateAssetContent(
                recovery.AssetId,
                recovery.NewHash,
                DateTimeOffset.UtcNow,
                recovery.NewPixelWidth,
                recovery.NewPixelHeight);
            var recovered = _repository.GetAsset(recovery.AssetId);
            if (recovered is not null)
            {
                AssetUpdated?.Invoke(this, recovered);
            }

            DeleteRecoveryJournal();
        }
        else if (string.Equals(currentHash, recovery.OldHash, StringComparison.Ordinal))
        {
            DeleteRecoveryJournal();
        }

        TryDelete(recovery.TemporaryPath);
    }

    private void WriteRecoveryJournal(EditRecoveryJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.RecoveryPath)!);
        var temporaryPath = _paths.RecoveryPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, _paths.RecoveryPath, overwrite: true);
    }

    private void DeleteRecoveryJournal()
    {
        if (File.Exists(_paths.RecoveryPath))
        {
            File.Delete(_paths.RecoveryPath);
        }
    }

    private static void WriteNewImageFile(string filePath, byte[] pngBytes)
    {
        var temporaryPath = filePath + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllBytes(temporaryPath, pngBytes);
        try
        {
            var verified = ImageCodec.Load(temporaryPath);
            _ = ImageCodec.ComputeContentHash(verified);
            File.Move(temporaryPath, filePath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record EditRecoveryJournal(
        Guid AssetId,
        string TargetPath,
        string TemporaryPath,
        string OldHash,
        string NewHash,
        int NewPixelWidth,
        int NewPixelHeight);
}

public interface AppSettingsAccessor
{
    AppSettings Current { get; }
}
