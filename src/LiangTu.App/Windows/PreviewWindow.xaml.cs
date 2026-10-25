using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class PreviewWindow : Window
{
    private double _zoom = 1;

    public PreviewWindow(ImageAsset asset, BitmapSource image)
    {
        InitializeComponent();
        Title = $"预览 · {asset.PixelWidth} × {asset.PixelHeight}";
        PreviewImage.Source = image;
        KeyDown += PreviewWindow_KeyDown;
    }

    private void ImageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        _zoom = Math.Clamp(_zoom + (e.Delta > 0 ? 0.1 : -0.1), 0.2, 4);
        ImageScale.ScaleX = _zoom;
        ImageScale.ScaleY = _zoom;
        e.Handled = true;
    }

    private void PreviewWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
