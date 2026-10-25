using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LiangTu.App.Infrastructure;

namespace LiangTu.App.Windows;

public partial class RegionCaptureWindow : Window
{
    private readonly MonitorGeometry _monitor;
    private readonly BitmapSource _screenImage;
    private Point? _start;

    public RegionCaptureWindow(MonitorGeometry monitor, BitmapSource screenImage)
    {
        _monitor = monitor;
        _screenImage = screenImage;
        InitializeComponent();
        BackgroundImage.Source = screenImage;
        Left = monitor.Bounds.Left / monitor.Scale;
        Top = monitor.Bounds.Top / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;
        Loaded += (_, _) => Focus();
    }

    public BitmapSource? CapturedImage { get; private set; }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_start.Value, _start.Value);
        CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_start is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelection(_start.Value, e.GetPosition(Root));
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        var end = e.GetPosition(Root);
        ReleaseMouseCapture();
        var selection = Normalize(_start.Value, end);
        if (selection.Width < 4 || selection.Height < 4)
        {
            CancelCapture();
            return;
        }

        var scaleX = _screenImage.PixelWidth / Root.ActualWidth;
        var scaleY = _screenImage.PixelHeight / Root.ActualHeight;
        var pixelRect = new Int32Rect(
            Math.Clamp((int)Math.Round(selection.X * scaleX), 0, _screenImage.PixelWidth - 1),
            Math.Clamp((int)Math.Round(selection.Y * scaleY), 0, _screenImage.PixelHeight - 1),
            Math.Max(1, (int)Math.Round(selection.Width * scaleX)),
            Math.Max(1, (int)Math.Round(selection.Height * scaleY)));
        pixelRect.Width = Math.Min(pixelRect.Width, _screenImage.PixelWidth - pixelRect.X);
        pixelRect.Height = Math.Min(pixelRect.Height, _screenImage.PixelHeight - pixelRect.Y);

        var cropped = new CroppedBitmap(_screenImage, pixelRect);
        cropped.Freeze();
        CapturedImage = cropped;
        Close();
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelCapture();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelCapture();
        }
    }

    private void UpdateSelection(Point start, Point end)
    {
        var selection = Normalize(start, end);
        SelectionBorder.Margin = new Thickness(selection.X, selection.Y, 0, 0);
        SelectionBorder.Width = selection.Width;
        SelectionBorder.Height = selection.Height;
    }

    private void CancelCapture()
    {
        CapturedImage = null;
        Close();
    }

    private static Rect Normalize(Point first, Point second)
    {
        return new Rect(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Abs(second.X - first.X),
            Math.Abs(second.Y - first.Y));
    }
}
