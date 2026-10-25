using System.Windows.Media.Imaging;
using LiangTu.Core;

namespace LiangTu.App.ViewModels;

public sealed record RopeImageViewModel(
    RopeItem Item,
    BitmapSource Thumbnail)
{
    public Guid Id => Item.Image.Id;

    public string DisplayTime => Item.Image.ModifiedAt.ToLocalTime().ToString("MM-dd HH:mm");

    public string Dimensions => $"{Item.Image.PixelWidth} × {Item.Image.PixelHeight}";

    public double DisplayWidth => CalculateDisplaySize().Width;

    public double DisplayHeight => CalculateDisplaySize().Height;

    public double FrameWidth => DisplayWidth + 16;

    public double FrameHeight => DisplayHeight + 36;

    private (double Width, double Height) CalculateDisplaySize()
    {
        const double maximumWidth = 150;
        const double maximumHeight = 104;
        const double minimumWidth = 56;
        const double minimumHeight = 48;

        var aspect = (double)Item.Image.PixelWidth / Item.Image.PixelHeight;
        var scale = Math.Min(
            maximumWidth / Item.Image.PixelWidth,
            maximumHeight / Item.Image.PixelHeight);
        var width = Item.Image.PixelWidth * scale;
        var height = Item.Image.PixelHeight * scale;

        if (width < minimumWidth)
        {
            width = minimumWidth;
            height = Math.Min(maximumHeight, width / aspect);
        }

        if (height < minimumHeight)
        {
            height = minimumHeight;
            width = Math.Min(maximumWidth, height * aspect);
        }

        return (Math.Round(width), Math.Round(height));
    }
}
