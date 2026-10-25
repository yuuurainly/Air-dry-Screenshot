using System.Windows;
using System.Windows.Threading;
using LiangTu.App.Infrastructure;

namespace LiangTu.App.Windows;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
        Loaded += ToastWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
    }

    private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var monitor = ScreenGeometry.GetMonitorFromCursor();
        Left = (monitor.WorkArea.Right / monitor.Scale) - Width - 8;
        Top = (monitor.WorkArea.Bottom / monitor.Scale) - ActualHeight - 8;
        _timer.Start();
    }
}
