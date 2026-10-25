using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LiangTu.App.Infrastructure;

namespace LiangTu.App.Windows;

public partial class WindowCaptureWindow : Window
{
    private readonly MonitorGeometry _monitor;
    private readonly BitmapSource _screenImage;
    private nint _targetWindow;

    public WindowCaptureWindow(MonitorGeometry monitor, BitmapSource screenImage)
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

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var cursor = ScreenGeometry.GetCursorPosition();
        _targetWindow = FindWindowAtPoint(cursor.X, cursor.Y);
        if (_targetWindow == 0)
        {
            HighlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var bounds = ScreenCaptureService.GetWindowBounds(_targetWindow);
        var left = (bounds.Left - _monitor.Bounds.Left) / _monitor.Scale;
        var top = (bounds.Top - _monitor.Bounds.Top) / _monitor.Scale;
        var width = bounds.Width / _monitor.Scale;
        var height = bounds.Height / _monitor.Scale;
        HighlightBorder.Margin = new Thickness(left, top, 0, 0);
        HighlightBorder.Width = width;
        HighlightBorder.Height = height;
        HighlightBorder.Visibility = Visibility.Visible;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_targetWindow == 0)
        {
            CancelCapture();
            return;
        }

        var bounds = ScreenCaptureService.GetWindowBounds(_targetWindow);
        var left = Math.Max(bounds.Left, _monitor.Bounds.Left);
        var top = Math.Max(bounds.Top, _monitor.Bounds.Top);
        var right = Math.Min(bounds.Right, _monitor.Bounds.Right);
        var bottom = Math.Min(bounds.Bottom, _monitor.Bounds.Bottom);
        if (right - left < 4 || bottom - top < 4)
        {
            CancelCapture();
            return;
        }

        var rectangle = new Int32Rect(
            left - _monitor.Bounds.Left,
            top - _monitor.Bounds.Top,
            right - left,
            bottom - top);
        try
        {
            var cropped = new CroppedBitmap(_screenImage, rectangle);
            cropped.Freeze();
            CapturedImage = cropped;
        }
        catch (ArgumentException)
        {
            CapturedImage = null;
        }

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

    private void CancelCapture()
    {
        CapturedImage = null;
        Close();
    }

    private static nint FindWindowAtPoint(int x, int y)
    {
        nint result = 0;
        var ownProcessId = Environment.ProcessId;
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
            {
                return true;
            }

            var extendedStyle = NativeMethods.GetWindowLong(window, NativeMethods.GwlExstyle);
            if ((extendedStyle & NativeMethods.WsExToolwindow) != 0)
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processId == ownProcessId)
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(window, out var rectangle)
                || rectangle.Width < 24
                || rectangle.Height < 24
                || x < rectangle.Left
                || x >= rectangle.Right
                || y < rectangle.Top
                || y >= rectangle.Bottom)
            {
                return true;
            }

            var length = NativeMethods.GetWindowTextLength(window);
            if (length == 0)
            {
                return true;
            }

            result = window;
            return false;
        }, 0);

        return result;
    }
}
