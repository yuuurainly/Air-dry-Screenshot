namespace LiangTu.App.Infrastructure;

public sealed class ClipboardMonitor : IDisposable
{
    private nint _window;

    public event EventHandler? ClipboardChanged;

    public void Attach(nint window)
    {
        Detach();
        _window = window;
        NativeMethods.AddClipboardFormatListener(window);
    }

    public bool ProcessWindowMessage(int message)
    {
        if (message != NativeMethods.WmClipboardUpdate)
        {
            return false;
        }

        ClipboardChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Dispose()
    {
        Detach();
    }

    private void Detach()
    {
        if (_window != 0)
        {
            NativeMethods.RemoveClipboardFormatListener(_window);
            _window = 0;
        }
    }
}
