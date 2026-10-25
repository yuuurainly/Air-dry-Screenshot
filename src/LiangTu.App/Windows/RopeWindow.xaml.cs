using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LiangTu.App.Infrastructure;
using LiangTu.App.Services;
using LiangTu.App.ViewModels;
using LiangTu.Core;

namespace LiangTu.App.Windows;

public partial class RopeWindow : Window
{
    private const double ContentHeight = 176;
    private const string RopeDragFormat = "LiangTu.RopeItem";
    private readonly ImageLibraryService _library;
    private readonly SettingsService _settings;
    private readonly ClipboardService _clipboard;
    private readonly HotkeyService _hotkeys;
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _holdHintTimer;
    private readonly DispatcherTimer _longPressTimer;
    private readonly HashSet<Guid> _knownRopeImageIds = [];
    private readonly HashSet<Guid> _pendingHangImageIds = [];
    private RopeAutoHideStateMachine _visibility;
    private HwndSource? _source;
    private RopeImageViewModel? _pressedItem;
    private Point _pressPosition;
    private bool _dragging;
    private bool _detaching;
    private bool _longPressTriggered;
    private bool _doubleClickTriggered;
    private int _externalInteractionDepth;
    private int _hoverInteractionDepth;
    private int _animationVersion;
    private bool _windowAnimationRunning;
    private bool _hasLoadedRope;
    private bool _refreshInProgress;
    private bool _refreshRequested;
    private Border? _activeHoldHint;
    private ListBoxItem? _dragContainer;
    private TranslateTransform? _detachTransform;
    private double _visibleTop;
    private double _hiddenTop;
    private HotZoneWindow? _hotZone;

    public RopeWindow(
        ImageLibraryService library,
        SettingsService settings,
        ClipboardService clipboard,
        HotkeyService hotkeys,
        ClipboardMonitor clipboardMonitor)
    {
        _library = library;
        _settings = settings;
        _clipboard = clipboard;
        _hotkeys = hotkeys;
        _clipboardMonitor = clipboardMonitor;
        _visibility = CreateVisibilityState();
        InitializeComponent();
        Height = ContentHeight;
        var monitor = ScreenGeometry.GetMonitorFromPoint(0, 0);
        var monitorTop = monitor.Bounds.Top / monitor.Scale;
        Left = monitor.Bounds.Left / monitor.Scale;
        Top = monitorTop - ContentHeight;
        _visibleTop = monitorTop;
        _hiddenTop = Top;
        Width = monitor.Bounds.Width / monitor.Scale;
        IsHitTestVisible = false;

        _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _hideTimer.Tick += HideTimer_Tick;

        _holdHintTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(220),
        };
        _holdHintTimer.Tick += HoldHintTimer_Tick;

        _longPressTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(420),
        };
        _longPressTimer.Tick += LongPressTimer_Tick;

        SourceInitialized += RopeWindow_SourceInitialized;
        Closed += RopeWindow_Closed;
        Loaded += RopeWindow_Loaded;
        _library.RopeChanged += Library_RopeChanged;
        _clipboardMonitor.ClipboardChanged += ClipboardMonitor_ClipboardChanged;
        _hotkeys.Pressed += Hotkeys_Pressed;
    }

    public event EventHandler<ImageAsset>? EditRequested;

    public event EventHandler<string>? ToastRequested;

    public event EventHandler? RegionScreenshotRequested;

    public event EventHandler? WindowScreenshotRequested;

    public event EventHandler? PinClipboardRequested;

    public event EventHandler? LibraryRequested;

    public event EventHandler<RopeDetachRequest>? DetachRequested;

    public bool IsExpanded => _visibility.IsVisible;

    public void ShowRope()
    {
        if (_visibility.IsVisible)
        {
            return;
        }

        _hideTimer.Stop();
        _visibility.Show(DateTimeOffset.UtcNow);
        PlayDropAnimation();
        _hideTimer.Start();
        _ = RefreshAsync();
    }

    public void HideImmediately()
    {
        _visibility.HideImmediately();
        _hideTimer.Stop();
        StopTopAnimation();
        Top = _hiddenTop;
        IsHitTestVisible = false;
        _windowAnimationRunning = false;
        _hotZone?.SetContentVisible(false);
    }

    public void PauseAutoHide()
    {
        _externalInteractionDepth++;
        if (_externalInteractionDepth == 1)
        {
            _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        }
    }

    public void ResumeAutoHide()
    {
        if (_externalInteractionDepth == 0)
        {
            return;
        }

        _externalInteractionDepth--;
        if (_externalInteractionDepth == 0)
        {
            _visibility.EndInteraction(DateTimeOffset.UtcNow);
            if (_visibility.IsVisible)
            {
                _hideTimer.Start();
            }
        }
    }

    public void ApplySettings()
    {
        var wasVisible = _visibility.IsVisible;
        _hideTimer.Stop();
        _visibility = CreateVisibilityState();
        if (_externalInteractionDepth > 0 || _hoverInteractionDepth > 0)
        {
            _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        }

        var failedHotkeys = _hotkeys.Register(_settings.Current);
        if (failedHotkeys.Count > 0)
        {
            ToastRequested?.Invoke(this, "部分快捷键未能注册，可能已被其他程序占用。");
        }

        if (wasVisible)
        {
            _visibility.Show(DateTimeOffset.UtcNow);
            _hideTimer.Start();
        }
        else
        {
            _visibility.HideImmediately();
            HideImmediately();
        }
    }

    public async Task RefreshAsync()
    {
        if (_refreshInProgress)
        {
            _refreshRequested = true;
            return;
        }

        _refreshInProgress = true;
        try
        {
            var items = await _library.GetRopeItemsAsync();
            var newImageIds = _hasLoadedRope
                ? items
                    .Where(item => !_knownRopeImageIds.Contains(item.Image.Id))
                    .Select(item => item.Image.Id)
                    .ToList()
                : [];
            var viewModels = new List<RopeImageViewModel>(items.Count);
            foreach (var item in items)
            {
                var thumbnail = await _library.GetThumbnailAsync(item.Image);
                viewModels.Add(new RopeImageViewModel(item, thumbnail));
            }

            _knownRopeImageIds.Clear();
            _knownRopeImageIds.UnionWith(items.Select(item => item.Image.Id));
            _hasLoadedRope = true;
            foreach (var imageId in newImageIds)
            {
                _pendingHangImageIds.Add(imageId);
            }

            RopeList.ItemsSource = viewModels;
            CountText.Text = viewModels.Count.ToString();
            EmptyState.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (viewModels.Count > 0)
            {
                RopeList.SelectedIndex = viewModels.Count - 1;
                await Dispatcher.InvokeAsync(
                    () => RopeList.ScrollIntoView(RopeList.SelectedItem),
                    DispatcherPriority.Background);
            }

            await Dispatcher.InvokeAsync(PlayPendingHangAnimations, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"刷新晾衣绳失败：{ex.Message}");
        }
        finally
        {
            _refreshInProgress = false;
            if (_refreshRequested)
            {
                _refreshRequested = false;
                _ = RefreshAsync();
            }
        }
    }

    private RopeAutoHideStateMachine CreateVisibilityState()
    {
        return new RopeAutoHideStateMachine(
            _settings.Current.AutoHide,
            TimeSpan.FromSeconds(_settings.Current.AutoHideSeconds));
    }

    private void RopeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionOnPrimaryMonitor();
        EnsureHotZone();
        ShowRope();
    }

    private void RopeWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WindowProc);
        _hotkeys.Attach(handle);
        var failedHotkeys = _hotkeys.Register(_settings.Current);
        if (failedHotkeys.Count > 0)
        {
            ToastRequested?.Invoke(this, "部分快捷键未能注册，可能已被其他程序占用。");
        }

        _clipboardMonitor.Attach(handle);
    }

    private void RopeWindow_Closed(object? sender, EventArgs e)
    {
        if (_hotZone is not null)
        {
            _hotZone.RevealRequested -= HotZone_RevealRequested;
            _hotZone.Close();
            _hotZone = null;
        }

        _source?.RemoveHook(WindowProc);
        _hotkeys.Pressed -= Hotkeys_Pressed;
        _clipboardMonitor.ClipboardChanged -= ClipboardMonitor_ClipboardChanged;
        _library.RopeChanged -= Library_RopeChanged;
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (_hotkeys.ProcessWindowMessage(message, wParam))
        {
            handled = true;
            return 0;
        }

        if (_clipboardMonitor.ProcessWindowMessage(message))
        {
            handled = true;
            return 0;
        }

        if (message == NativeMethods.WmDisplayChange)
        {
            _ = Dispatcher.InvokeAsync(PositionOnPrimaryMonitor);
        }

        return 0;
    }

    private void Hotkeys_Pressed(object? sender, GlobalHotkey hotkey)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (hotkey)
            {
                case GlobalHotkey.RegionScreenshot:
                    RegionScreenshotRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case GlobalHotkey.WindowScreenshot:
                    WindowScreenshotRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case GlobalHotkey.PinClipboard:
                    PinClipboardRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        });
    }

    private async void ClipboardMonitor_ClipboardChanged(object? sender, EventArgs e)
    {
        if (!_settings.Current.AutoCollect || _clipboard.IsSelfCopy())
        {
            return;
        }

        try
        {
            if (!_clipboard.TryReadImage(out var image) || image is null)
            {
                return;
            }

            await _library.ImportBitmapAsync(image, ImageSourceType.Clipboard);
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"剪贴板收图失败：{ex.Message}");
        }
    }

    private void Library_RopeChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(async () => await RefreshAsync());
    }

    private void HideTimer_Tick(object? sender, EventArgs e)
    {
        if (_visibility.Tick(DateTimeOffset.UtcNow))
        {
            _hideTimer.Stop();
            PlayReturnAnimation();
        }
    }

    private void RopeCard_MouseEnter(object sender, MouseEventArgs e)
    {
        BeginHoverInteraction();
    }

    private void RopeCard_MouseLeave(object sender, MouseEventArgs e)
    {
        EndHoverInteraction();
    }

    private void InteractiveArea_MouseEnter(object sender, MouseEventArgs e)
    {
        BeginHoverInteraction();
    }

    private void InteractiveArea_MouseLeave(object sender, MouseEventArgs e)
    {
        EndHoverInteraction();
    }

    private void BeginHoverInteraction()
    {
        _hoverInteractionDepth++;
        if (_hoverInteractionDepth == 1)
        {
            _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        }
    }

    private void EndHoverInteraction()
    {
        if (_hoverInteractionDepth == 0)
        {
            return;
        }

        _hoverInteractionDepth--;
        if (_hoverInteractionDepth == 0)
        {
            _visibility.EndInteraction(DateTimeOffset.UtcNow);
            if (_visibility.IsVisible)
            {
                _hideTimer.Start();
            }
        }
    }

    private void PositionOnPrimaryMonitor()
    {
        var monitor = ScreenGeometry.GetMonitorFromPoint(0, 0);
        _visibleTop = monitor.Bounds.Top / monitor.Scale;
        _hiddenTop = _visibleTop - ContentHeight - 12;
        Left = monitor.Bounds.Left / monitor.Scale;
        Width = monitor.Bounds.Width / monitor.Scale;
        _hotZone?.PositionOn(monitor);

        StopTopAnimation();
        Top = _visibility.IsVisible ? _visibleTop : _hiddenTop;
    }

    private void EnsureHotZone()
    {
        if (_hotZone is not null)
        {
            return;
        }

        _hotZone = new HotZoneWindow();
        _hotZone.RevealRequested += HotZone_RevealRequested;
        _hotZone.PositionOn(ScreenGeometry.GetMonitorFromPoint(0, 0));
        _hotZone.Show();
    }

    private void HotZone_RevealRequested(object? sender, EventArgs e)
    {
        ShowRope();
    }

    private bool ShouldAnimate => _settings.Current.RopeAnimationEnabled && SystemParameters.ClientAreaAnimation;

    private void PlayDropAnimation()
    {
        StopTopAnimation();
        Top = _hiddenTop;
        IsHitTestVisible = true;
        _windowAnimationRunning = true;
        _hotZone?.SetContentVisible(true);

        if (!ShouldAnimate)
        {
            Top = _visibleTop;
            _windowAnimationRunning = false;
            PlayPendingHangAnimations();
            return;
        }

        var version = ++_animationVersion;
        var topAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(380),
            FillBehavior = FillBehavior.HoldEnd,
        };
        topAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
            _visibleTop + 5,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(292)),
            new KeySpline(0.18, 0.86, 0.26, 1)));
        topAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
            _visibleTop - 1.2,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340)),
            new KeySpline(0.4, 0, 0.55, 1)));
        topAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
            _visibleTop,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)),
            new KeySpline(0.25, 0.7, 0.35, 1)));
        topAnimation.Completed += (_, _) =>
        {
            if (version != _animationVersion)
            {
                return;
            }

            BeginAnimation(TopProperty, null);
            Top = _visibleTop;
            IsHitTestVisible = true;
            _windowAnimationRunning = false;
            PlayPendingHangAnimations();
        };
        BeginAnimation(TopProperty, topAnimation);
    }

    private void PlayReturnAnimation()
    {
        StopTopAnimation();
        IsHitTestVisible = false;
        _windowAnimationRunning = true;

        if (!ShouldAnimate)
        {
            Top = _hiddenTop;
            _hotZone?.SetContentVisible(false);
            _windowAnimationRunning = false;
            return;
        }

        var version = ++_animationVersion;
        var topAnimation = new DoubleAnimation
        {
            To = _hiddenTop,
            Duration = TimeSpan.FromMilliseconds(230),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd,
        };
        topAnimation.Completed += (_, _) =>
        {
            if (version != _animationVersion)
            {
                return;
            }

            BeginAnimation(TopProperty, null);
            Top = _hiddenTop;
            _hotZone?.SetContentVisible(false);
            _windowAnimationRunning = false;
        };
        BeginAnimation(TopProperty, topAnimation);
    }

    private void StopTopAnimation()
    {
        _animationVersion++;
        BeginAnimation(TopProperty, null);
    }

    private void PlayPendingHangAnimations()
    {
        if (!IsLoaded
            || !_visibility.IsVisible
            || _windowAnimationRunning
            || _pendingHangImageIds.Count == 0
            || RopeList.Items.Count == 0)
        {
            return;
        }

        if (!ShouldAnimate)
        {
            _pendingHangImageIds.Clear();
            return;
        }

        RopeList.UpdateLayout();
        var animationIndex = 0;
        foreach (var imageId in _pendingHangImageIds.ToList())
        {
            var item = RopeList.Items
                .OfType<RopeImageViewModel>()
                .FirstOrDefault(candidate => candidate.Id == imageId);
            if (item is null
                || RopeList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)
            {
                if (item is null)
                {
                    _pendingHangImageIds.Remove(imageId);
                }

                continue;
            }

            _pendingHangImageIds.Remove(imageId);
            var delay = TimeSpan.FromMilliseconds(animationIndex++ * 45);
            var transform = new TranslateTransform(0, -34);
            container.RenderTransform = transform;
            container.Opacity = 0;
            container.IsHitTestVisible = false;

            var dropAnimation = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = delay,
                Duration = TimeSpan.FromMilliseconds(380),
                FillBehavior = FillBehavior.HoldEnd,
            };
            dropAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
                3,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)),
                new KeySpline(0.18, 0.86, 0.28, 1)));
            dropAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
                -1,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)),
                new KeySpline(0.42, 0, 0.56, 1)));
            dropAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(
                0,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            dropAnimation.Completed += (_, _) =>
            {
                container.RenderTransform = null;
                container.Opacity = 1;
                container.IsHitTestVisible = true;
            };

            transform.BeginAnimation(TranslateTransform.YProperty, dropAnimation);
            container.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
                {
                    BeginTime = delay,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }
    }

    private void RopeList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _holdHintTimer.Stop();
        _longPressTimer.Stop();
        HideHoldHint();
        _dragging = false;
        _detaching = false;
        _longPressTriggered = false;
        _doubleClickTriggered = e.ClickCount > 1;

        if (FindTaggedAncestor(e.OriginalSource as DependencyObject, "Delete") is not null
            || FindTaggedAncestor(e.OriginalSource as DependencyObject, "Detach") is not null)
        {
            _pressedItem = null;
            return;
        }

        _pressedItem = FindItem(e.OriginalSource as DependencyObject);
        _pressPosition = e.GetPosition(this);

        if (_pressedItem is null)
        {
            return;
        }

        RopeList.SelectedItem = _pressedItem;
        Activate();
        RopeList.Focus();
        if (_doubleClickTriggered)
        {
            _holdHintTimer.Stop();
            _longPressTimer.Stop();
            e.Handled = true;
            return;
        }

        _holdHintTimer.Start();
        _longPressTimer.Start();
    }

    private void RopeList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _holdHintTimer.Stop();
        _longPressTimer.Stop();
        HideHoldHint();

        if (_detaching)
        {
            CompleteDetach();
            e.Handled = true;
            return;
        }

        if (_dragging || _longPressTriggered || _doubleClickTriggered || _pressedItem is null)
        {
            _pressedItem = null;
            return;
        }

        CopyItem(_pressedItem);
        _pressedItem = null;
    }

    private void RopeList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (_detaching)
        {
            UpdateDetachVisual(current);
            return;
        }

        if (_pressedItem is null || _dragging)
        {
            return;
        }

        if (Math.Abs(current.X - _pressPosition.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _pressPosition.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _longPressTimer.Stop();
        _holdHintTimer.Stop();
        HideHoldHint();

        if (_longPressTriggered)
        {
            BeginDetachDrag(current);
            return;
        }

        _dragging = true;
        _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        var container = RopeList.ItemContainerGenerator.ContainerFromItem(_pressedItem) as ListBoxItem;
        if (container is not null)
        {
            container.Opacity = 0.55;
        }

        try
        {
            var data = new DataObject();
            data.SetData(RopeDragFormat, _pressedItem.Item.Id);
            data.SetData(DataFormats.FileDrop, new[] { _pressedItem.Item.Image.FilePath });
            data.SetData(
                "Preferred DropEffect",
                new MemoryStream(BitConverter.GetBytes(1)));
            DragDrop.DoDragDrop(
                RopeList,
                data,
                DragDropEffects.Copy);
        }
        finally
        {
            if (container is not null)
            {
                container.Opacity = 1;
            }

            _visibility.EndInteraction(DateTimeOffset.UtcNow);
            _pressedItem = null;
        }
    }

    private void LongPressTimer_Tick(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        _holdHintTimer.Stop();
        if (_pressedItem is null || _dragging || _detaching)
        {
            return;
        }

        _longPressTriggered = true;
        var container = RopeList.ItemContainerGenerator.ContainerFromItem(_pressedItem) as ListBoxItem;
        var holdText = FindTemplateElement<TextBlock>(container, "HoldHintText");
        if (holdText is not null)
        {
            holdText.Text = "拖动到桌面钉住";
        }
    }

    private void HoldHintTimer_Tick(object? sender, EventArgs e)
    {
        _holdHintTimer.Stop();
        if (_pressedItem is null)
        {
            return;
        }

        var container = RopeList.ItemContainerGenerator.ContainerFromItem(_pressedItem) as ListBoxItem;
        _activeHoldHint = FindTemplateElement<Border>(container, "HoldHint");
        if (_activeHoldHint is null)
        {
            return;
        }

        _activeHoldHint.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void BeginDetachDrag(Point current)
    {
        if (_pressedItem is null)
        {
            return;
        }

        _dragging = true;
        _detaching = true;
        _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        _dragContainer = RopeList.ItemContainerGenerator.ContainerFromItem(_pressedItem) as ListBoxItem;
        if (_dragContainer is not null)
        {
            _detachTransform = new TranslateTransform();
            _dragContainer.RenderTransform = _detachTransform;
            _dragContainer.Opacity = 0.82;
        }

        DetachHint.Visibility = Visibility.Visible;
        RopeList.CaptureMouse();
        UpdateDetachVisual(current);
    }

    private void UpdateDetachVisual(Point current)
    {
        if (_detachTransform is null)
        {
            return;
        }

        _detachTransform.X = current.X - _pressPosition.X;
        _detachTransform.Y = current.Y - _pressPosition.Y;
    }

    private async void CompleteDetach()
    {
        var item = _pressedItem;
        var releasePoint = Mouse.GetPosition(RopeContent);
        RopeList.ReleaseMouseCapture();
        DetachHint.Visibility = Visibility.Collapsed;
        if (_dragContainer is not null)
        {
            _dragContainer.RenderTransform = null;
            _dragContainer.Opacity = 1;
        }

        _dragContainer = null;
        _detachTransform = null;
        _detaching = false;
        _dragging = false;
        _pressedItem = null;
        _visibility.EndInteraction(DateTimeOffset.UtcNow);

        if (item is null)
        {
            return;
        }

        var insideRope = releasePoint.X >= 0
                         && releasePoint.X <= RopeContent.ActualWidth
                         && releasePoint.Y >= 0
                         && releasePoint.Y <= RopeContent.ActualHeight;
        var pulledDown = releasePoint.Y - _pressPosition.Y > 34;
        if (insideRope && !pulledDown)
        {
            var target = FindItem(
                RopeList.InputHitTest(Mouse.GetPosition(RopeList)) as DependencyObject);
            if (target is not null && target.Id != item.Id)
            {
                await _library.MoveRopeItemAsync(
                    item.Item.Id,
                    RopeList.Items.IndexOf(target));
            }

            return;
        }

        var (screenX, screenY) = ScreenGeometry.GetCursorPosition();
        DetachRequested?.Invoke(
            this,
            new RopeDetachRequest(item.Item.Image, screenX, screenY));
    }

    private void RopeList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right) || RopeList.Items.Count == 0)
        {
            return;
        }

        var direction = e.Key == Key.Left ? -1 : 1;
        RopeList.SelectedIndex = Math.Clamp(RopeList.SelectedIndex + direction, 0, RopeList.Items.Count - 1);
        RopeList.ScrollIntoView(RopeList.SelectedItem);
        e.Handled = true;
    }

    private async void ExpandedSurface_Drop(object sender, DragEventArgs e)
    {
        _visibility.BeginInteraction(DateTimeOffset.UtcNow);
        DropHint.Visibility = Visibility.Collapsed;
        try
        {
            if (e.Data.GetDataPresent(RopeDragFormat)
                && e.Data.GetData(RopeDragFormat) is Guid ropeItemId)
            {
                var target = FindItem(e.OriginalSource as DependencyObject);
                var targetIndex = target is null ? RopeList.Items.Count - 1 : RopeList.Items.IndexOf(target);
                await _library.MoveRopeItemAsync(ropeItemId, targetIndex);
                e.Effects = DragDropEffects.Copy;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop)
                && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files.Where(IsSupportedImage))
                {
                    await _library.ImportFileAsync(file);
                }

                e.Effects = DragDropEffects.Copy;
            }
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"导入失败：{ex.Message}");
        }
        finally
        {
            _visibility.EndInteraction(DateTimeOffset.UtcNow);
            e.Handled = true;
        }
    }

    private void ExpandedSurface_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(RopeDragFormat))
        {
            var target = FindItem(e.OriginalSource as DependencyObject)
                         ?? RopeList.Items.OfType<RopeImageViewModel>().LastOrDefault();
            if (target is not null)
            {
                RopeList.SelectedItem = target;
                RopeList.ScrollIntoView(target);
            }

            DropHint.Visibility = Visibility.Collapsed;
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            DropHint.Visibility = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? Visibility.Visible
                : Visibility.Collapsed;
            e.Effects = DragDropEffects.Copy;
        }

        e.Handled = true;
    }

    private void RopeContent_DragLeave(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
    }

    private async void CopyItem(RopeImageViewModel item)
    {
        try
        {
            var image = await _library.LoadFullImageAsync(item.Item.Image);
            _clipboard.CopyImage(image);
            ToastRequested?.Invoke(this, "已复制原图");
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"复制失败：{ex.Message}");
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RopeImageViewModel item)
        {
            return;
        }

        _holdHintTimer.Stop();
        _longPressTimer.Stop();
        HideHoldHint();
        _pressedItem = null;
        try
        {
            await _library.DeleteAssetsAsync([item.Item.Image]);
            ToastRequested?.Invoke(this, "截图已删除");
        }
        catch (Exception ex)
        {
            ToastRequested?.Invoke(this, $"删除失败：{ex.Message}");
        }

        e.Handled = true;
    }

    private void Clothespin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RopeImageViewModel item })
        {
            return;
        }

        var (screenX, screenY) = ScreenGeometry.GetCursorPosition();
        DetachRequested?.Invoke(
            this,
            new RopeDetachRequest(item.Item.Image, screenX, screenY));
        e.Handled = true;
    }

    private void RopeImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RopeImageViewModel item })
        {
            _holdHintTimer.Stop();
            _longPressTimer.Stop();
            HideHoldHint();
            _pressedItem = null;
            EditRequested?.Invoke(this, item.Item.Image);
            e.Handled = true;
        }
    }

    private void BasketButton_Click(object sender, RoutedEventArgs e)
    {
        LibraryRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static FrameworkElement? FindTaggedAncestor(
        DependencyObject? source,
        string tag)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element
                && string.Equals(element.Tag as string, tag, StringComparison.Ordinal))
            {
                return element;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private RopeImageViewModel? FindItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element && element.DataContext is RopeImageViewModel item)
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void HideHoldHint()
    {
        if (_activeHoldHint is null)
        {
            return;
        }

        _activeHoldHint.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(90))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        _activeHoldHint = null;
    }

    private static T? FindTemplateElement<T>(DependencyObject? root, string name)
        where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            var nested = FindTemplateElement<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool IsSupportedImage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff";
    }

}

public sealed record RopeDetachRequest(
    ImageAsset Asset,
    int ScreenX,
    int ScreenY);
