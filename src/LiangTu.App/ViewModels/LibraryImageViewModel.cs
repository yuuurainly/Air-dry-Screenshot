using System.Windows.Media.Imaging;
using LiangTu.Core;

namespace LiangTu.App.ViewModels;

public sealed record LibraryImageViewModel(
    ImageAsset Asset,
    BitmapSource Thumbnail)
{
    public string ModifiedText => Asset.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string Dimensions => $"{Asset.PixelWidth} × {Asset.PixelHeight}";

    public string SourceText => Asset.SourceType switch
    {
        ImageSourceType.Screenshot => "截图",
        ImageSourceType.Clipboard => "剪贴板",
        _ => "导入",
    };
}
