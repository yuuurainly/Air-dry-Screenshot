using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LiangTu.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _ropeItem;
    private readonly Forms.ToolStripMenuItem _autoCollectItem;
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Drawing.Icon _applicationIcon;

    public TrayService(bool ropeVisible, bool autoCollect, bool startWithWindows)
    {
        _ropeItem = new Forms.ToolStripMenuItem(ropeVisible ? "隐藏晾衣绳" : "显示晾衣绳");
        _autoCollectItem = new Forms.ToolStripMenuItem("自动收集图片", null, (_, _) => AutoCollectToggleRequested?.Invoke(this, EventArgs.Empty))
        {
            Checked = autoCollect,
        };
        _startupItem = new Forms.ToolStripMenuItem("开机启动", null, (_, _) => StartupToggleRequested?.Invoke(this, EventArgs.Empty))
        {
            Checked = startWithWindows,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_ropeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_autoCollectItem);
        menu.Items.Add("导入图片", null, (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("打开图片目录", null, (_, _) => OpenFolderRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("打开图片库", null, (_, _) => LibraryRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出晾图", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _ropeItem.Click += (_, _) => ToggleRopeRequested?.Invoke(this, EventArgs.Empty);
        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "晾图",
            Icon = _applicationIcon,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleRopeRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ToggleRopeRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? AutoCollectToggleRequested;

    public event EventHandler? ImportRequested;

    public event EventHandler? OpenFolderRequested;

    public event EventHandler? LibraryRequested;

    public event EventHandler? StartupToggleRequested;

    public event EventHandler? ExitRequested;

    public void SetRopeState(bool visible)
    {
        _ropeItem.Text = visible ? "隐藏晾衣绳" : "显示晾衣绳";
    }

    public void SetAutoCollect(bool enabled)
    {
        _autoCollectItem.Checked = enabled;
    }

    public void SetStartup(bool enabled)
    {
        _startupItem.Checked = enabled;
    }

    public void ShowMessage(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(1800);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
    }

    private static Drawing.Icon LoadApplicationIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/LiangTu;component/Assets/liangtu.ico"));
            if (resource?.Stream is not null)
            {
                using var stream = resource.Stream;
                return new Drawing.Icon(stream, 32, 32);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
        }

        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }
}
