using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LiangTu.App.Infrastructure;

namespace LiangTu.App.Windows;

public partial class HotZoneWindow : Window
{
    private bool _contentVisible;

    public HotZoneWindow()
    {
        InitializeComponent();
        SourceInitialized += HotZoneWindow_SourceInitialized;
    }

    public event EventHandler? RevealRequested;

    public void PositionOn(MonitorGeometry monitor)
    {
        Left = monitor.Bounds.Left / monitor.Scale;
        Top = monitor.Bounds.Top / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
    }

    public void SetContentVisible(bool visible)
    {
        _contentVisible = visible;
        HintLine.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                To = visible ? 0 : 0.82,
                Duration = TimeSpan.FromMilliseconds(130),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void HotZoneWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExstyle);
        NativeMethods.SetWindowLong(
            handle,
            NativeMethods.GwlExstyle,
            extendedStyle | NativeMethods.WsExNoactivate | NativeMethods.WsExToolwindow);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_contentVisible)
        {
            AnimateHint(1.28, 1);
            RevealRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_contentVisible)
        {
            AnimateHint(1, 0.82);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RevealRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void AnimateHint(double scale, double opacity)
    {
        HintScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        HintScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        HintLine.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }
}
