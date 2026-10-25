using System.Windows;
using LiangTu.App.Windows;

namespace LiangTu.App.Services;

public sealed class ToastService
{
    private ToastWindow? _current;

    public void Show(string message)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Show(message));
            return;
        }

        _current?.Close();
        _current = new ToastWindow(message);
        _current.Closed += (_, _) => _current = null;
        _current.Show();
    }
}
