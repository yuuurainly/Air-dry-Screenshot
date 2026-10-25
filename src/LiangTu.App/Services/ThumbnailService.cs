using System.Windows.Media.Imaging;
using LiangTu.App.Infrastructure;
using LiangTu.Core;

namespace LiangTu.App.Services;

public sealed class ThumbnailService
{
    private const int ThumbnailWidth = 320;
    private const int ThumbnailHeight = 220;
    private readonly string _thumbnailDirectory;

    public ThumbnailService(string thumbnailDirectory)
    {
        _thumbnailDirectory = thumbnailDirectory;
    }

    public Task<BitmapSource> GetThumbnailAsync(ImageAsset asset)
    {
        return Task.Run(() => GetThumbnail(asset));
    }

    public void DeleteCachedThumbnails(Guid imageId)
    {
        if (!Directory.Exists(_thumbnailDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
                     _thumbnailDirectory,
                     $"{imageId:N}-*",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private BitmapSource GetThumbnail(ImageAsset asset)
    {
        Directory.CreateDirectory(_thumbnailDirectory);
        var cachePath = Path.Combine(
            _thumbnailDirectory,
            $"{asset.Id:N}-{asset.ContentHash[..12]}.png");

        if (File.Exists(cachePath))
        {
            try
            {
                return ImageCodec.Load(cachePath);
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException)
            {
                File.Delete(cachePath);
            }
        }

        var source = ImageCodec.Load(asset.FilePath, ThumbnailWidth);
        var thumbnailBytes = ImageCodec.EncodeThumbnail(source, ThumbnailWidth, ThumbnailHeight);
        var temporaryPath = cachePath + ".tmp";
        File.WriteAllBytes(temporaryPath, thumbnailBytes);
        File.Move(temporaryPath, cachePath, overwrite: true);
        return ImageCodec.LoadBytes(thumbnailBytes);
    }
}
