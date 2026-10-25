using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiangTu.App.Data;
using LiangTu.App.Infrastructure;
using LiangTu.App.Services;
using LiangTu.Core;
using Microsoft.Data.Sqlite;

namespace LiangTu.App.Tests;

[TestClass]
public sealed class ImageLibraryServiceTests
{
    [TestMethod]
    public async Task Import_IsDeduplicatedAndRopeOrderPersists()
    {
        await WithLibraryAsync(async (library, imageDirectory) =>
        {
            var image = CreateBitmap(64, 48, Colors.SteelBlue);

            var first = await library.ImportBitmapAsync(image, ImageSourceType.Clipboard);
            var second = await library.ImportBitmapAsync(image, ImageSourceType.Clipboard);

            Assert.AreEqual(first.Id, second.Id);
            Assert.HasCount(1, Directory.GetFiles(imageDirectory, "*.png"));
            Assert.HasCount(1, await library.GetRopeItemsAsync());
        });
    }

    [TestMethod]
    public async Task EditedImage_ReplacesOriginalWithoutNewAssetOrRopeItem()
    {
        await WithLibraryAsync(async (library, imageDirectory) =>
        {
            var original = CreateBitmap(80, 60, Colors.DarkGreen);
            var asset = await library.ImportBitmapAsync(original, ImageSourceType.Screenshot);
            var edited = CreateBitmap(80, 60, Colors.OrangeRed);
            var editedBytes = ImageCodec.EncodePng(edited);

            var updated = await library.SaveEditedImageAsync(asset, editedBytes);

            Assert.AreEqual(asset.Id, updated.Id);
            Assert.AreNotEqual(asset.ContentHash, updated.ContentHash);
            Assert.HasCount(1, Directory.GetFiles(imageDirectory, "*.png"));
            Assert.HasCount(1, await library.GetRopeItemsAsync());
            Assert.AreEqual(updated.ContentHash, (await library.GetAssetAsync(asset.Id))?.ContentHash);
        });
    }

    [TestMethod]
    public async Task RemovingFromRope_KeepsImageInLibrary()
    {
        await WithLibraryAsync(async (library, imageDirectory) =>
        {
            var asset = await library.ImportBitmapAsync(
                CreateBitmap(90, 60, Colors.DarkSlateBlue),
                ImageSourceType.Clipboard);

            await library.RemoveAssetFromRopeAsync(asset.Id);

            Assert.IsEmpty(await library.GetRopeItemsAsync());
            Assert.HasCount(1, await library.GetAssetsAsync());
            Assert.IsTrue(File.Exists(asset.FilePath));
        });
    }

    [TestMethod]
    public async Task DeletingAssets_RemovesFilesAndDatabaseRecords()
    {
        await WithLibraryAsync(async (library, imageDirectory) =>
        {
            var asset = await library.ImportBitmapAsync(
                CreateBitmap(90, 60, Colors.DarkGoldenrod),
                ImageSourceType.FileImport);

            var deletedCount = await library.DeleteAssetsAsync([asset]);

            Assert.AreEqual(1, deletedCount);
            Assert.IsEmpty(await library.GetAssetsAsync());
            Assert.IsEmpty(await library.GetRopeItemsAsync());
            Assert.IsFalse(File.Exists(asset.FilePath));
        });
    }

    private static async Task WithLibraryAsync(
        Func<ImageLibraryService, string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "LiangTu.Tests", Guid.NewGuid().ToString("N"));
        var imageDirectory = Path.Combine(root, "images");
        var thumbnailDirectory = Path.Combine(root, "thumbnails");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(imageDirectory);
        try
        {
            var settings = new TestSettingsAccessor(imageDirectory);
            var repository = new ImageRepository(Path.Combine(root, "liangtu.db"));
            var library = new ImageLibraryService(
                settings,
                repository,
                new ThumbnailService(thumbnailDirectory),
                new AppPaths(root));
            await library.InitializeAsync();
            await test(library, imageDirectory);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static BitmapSource CreateBitmap(int width, int height, Color color)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
            pixels[index + 3] = 255;
        }

        var image = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        image.Freeze();
        return image;
    }

    private sealed class TestSettingsAccessor(string imageDirectory) : AppSettingsAccessor
    {
        public AppSettings Current { get; } = new() { ScreenshotsDirectory = imageDirectory };
    }
}
