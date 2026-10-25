using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class PinWindow : Window
{
    private readonly DispatcherTimer _saveTimer;
    private readonly DateTimeOffset _createdAt;
    private readonly bool _showReturnToRope;
    private bool _savingEnabled;

    public PinWindow(
        Guid pinId,
        Guid? imageId,
        BitmapSource image,
        PinInstanceState? restoredState = null,
        bool showReturnToRope = false)
    {
        Id = pinId;
        ImageId = imageId;
        _showReturnToRope = showReturnToRope;
        _createdAt = restoredState?.CreatedAt ?? DateTimeOffset.UtcNow;
        InitializeComponent();
        PinImage.Source = image;
        ReturnPinButton.Visibility = showReturnToRope
            ? Visibility.Visible
            : Visibility.Collapsed;

        var width = restoredState?.Width ?? Math.Min(640, Math.Max(260, image.PixelWidth / 1.5));
        var height = restoredState?.Height ?? Math.Min(520, Math.Max(180, image.PixelHeight / 1.5));
        Width = width;
        Height = height;
        Opacity = restoredState?.Opacity ?? 1;
        Topmost = restoredState?.IsTopmost ?? true;

        if (restoredState is not null)
        {
            Left = restoredState.X;
            Top = restoredState.Y;
        }

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        };
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        Loaded += (_, _) =>
        {
            _savingEnabled = true;
            UpdateStatus();
        };
        Closed += (_, _) => _saveTimer.Stop();
    }

    public event EventHandler? PinStateChanged;

    public event EventHandler? ReturnToRopeRequested;

    public Guid Id { get; }

    public Guid? ImageId { get; }

    public PinInstanceState State => new(
        Id,
        ImageId,
        Left,
        Top,
        Width,
        Height,
        Opacity,
        Topmost,
        _createdAt);

    public void UpdateImage(BitmapSource image)
    {
        PinImage.Source = image;
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        TopmostMenuItem.IsChecked = Topmost;
        PinStatusText.Text = _showReturnToRope
            ? $"已取下 · {Math.Round(Opacity * 100)}%"
            : $"贴图 · {Math.Round(Opacity * 100)}%";
    }

    private void PinImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void PinImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        Opacity = Math.Clamp(Opacity + (e.Delta > 0 ? 0.05 : -0.05), 0.2, 1);
        UpdateStatus();
        ScheduleSave();
        e.Handled = true;
    }

    private void OpacityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value })
        {
            Opacity = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            UpdateStatus();
            ScheduleSave();
        }
    }

    private void TopmostMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostMenuItem.IsChecked;
        UpdateStatus();
        ScheduleSave();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ReturnPinButton_Click(object sender, RoutedEventArgs e)
    {
        ReturnToRopeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ScheduleSave()
    {
        if (!_savingEnabled)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
