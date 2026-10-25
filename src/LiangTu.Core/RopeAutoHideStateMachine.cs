namespace LiangTu.Core;

public sealed class RopeAutoHideStateMachine
{
    private int _interactionDepth;
    private DateTimeOffset? _deadline;

    public RopeAutoHideStateMachine(bool autoHideEnabled = true, TimeSpan? autoHideDelay = null)
    {
        AutoHideEnabled = autoHideEnabled;
        AutoHideDelay = autoHideDelay ?? TimeSpan.FromMinutes(2);
        if (AutoHideDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(autoHideDelay));
        }

    }

    public bool AutoHideEnabled { get; private set; }

    public TimeSpan AutoHideDelay { get; }

    public bool IsVisible { get; private set; }

    public bool IsInteractionActive => _interactionDepth > 0;

    public void Show(DateTimeOffset now)
    {
        IsVisible = true;
        _deadline = AutoHideEnabled && _interactionDepth == 0 ? now + AutoHideDelay : null;
    }

    public void HideImmediately()
    {
        IsVisible = false;
        _deadline = null;
    }

    public void BeginInteraction(DateTimeOffset now)
    {
        _deadline = null;
        _interactionDepth++;
    }

    public void EndInteraction(DateTimeOffset now)
    {
        if (_interactionDepth == 0)
        {
            return;
        }

        _interactionDepth--;
        if (_interactionDepth == 0 && IsVisible && AutoHideEnabled)
        {
            _deadline = now + AutoHideDelay;
        }
    }

    public bool Tick(DateTimeOffset now)
    {
        if (!IsVisible || !AutoHideEnabled || _interactionDepth > 0 || _deadline is null || now < _deadline)
        {
            return false;
        }

        HideImmediately();
        return true;
    }

    public void SetAutoHideEnabled(bool enabled, DateTimeOffset now)
    {
        AutoHideEnabled = enabled;
        if (!enabled)
        {
            _deadline = null;
            return;
        }

        if (IsVisible && _interactionDepth == 0)
        {
            _deadline = now + AutoHideDelay;
        }
    }
}
