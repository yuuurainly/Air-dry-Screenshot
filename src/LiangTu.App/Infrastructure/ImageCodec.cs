using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiangTu.App.Infrastructure;

public static class ImageCodec
{
    private const long MaximumPixelCount = 80_000_000;

    public static BitmapSource Load(string path, int? decodePixelWidth = null)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.StreamSource = stream;
        if (decodePixelWidth is > 0)
        {
            image.DecodePixelWidth = decodePixelWidth.Value;
        }

        image.EndInit();
        image.Freeze();
        ValidateSize(image);
        return image;
    }

    public static BitmapSource LoadBytes(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        ValidateSize(image);
        return image;
    }

    public static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static byte[] EncodeThumbnail(BitmapSource source, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(
            1d,
            Math.Min((double)maxWidth / source.PixelWidth, (double)maxHeight / source.PixelHeight));
        BitmapSource thumbnail = source;
        if (scale < 1)
        {
            thumbnail = new TransformedBitmap(
                source,
                new ScaleTransform(scale, scale));
            thumbnail.Freeze();
        }

        return EncodePng(thumbnail);
    }

    public static string ComputeContentHash(BitmapSource source)
    {
        ValidateSize(source);
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        if (converted.CanFreeze)
        {
            converted.Freeze();
        }

        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> dimensions = stackalloc byte[8];
        BitConverter.TryWriteBytes(dimensions[..4], converted.PixelWidth);
        BitConverter.TryWriteBytes(dimensions[4..], converted.PixelHeight);
        hash.AppendData(dimensions);
        hash.AppendData(pixels);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static int GetScaleAdjustedPixelLength(double value, double scale)
    {
        return Math.Max(1, (int)Math.Round(value * scale));
    }

    private static void ValidateSize(BitmapSource source)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new InvalidDataException("图片尺寸无效。");
        }

        if ((long)source.PixelWidth * source.PixelHeight > MaximumPixelCount)
        {
            throw new InvalidDataException("图片像素数量过大，无法安全处理。");
        }
    }
}
