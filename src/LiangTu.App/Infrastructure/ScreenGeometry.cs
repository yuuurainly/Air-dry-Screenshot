namespace LiangTu.App.Infrastructure;

public sealed record PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed record MonitorGeometry(
    nint Handle,
    PixelRect Bounds,
    PixelRect WorkArea,
    double Scale);

public static class ScreenGeometry
{
    public static MonitorGeometry GetMonitorFromPoint(int x, int y)
    {
        var monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.Point { X = x, Y = y },
            NativeMethods.MonitorDefaultToNearest);
        return FromHandle(monitor);
    }

    public static MonitorGeometry GetMonitorFromWindow(nint window)
    {
        var monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MonitorDefaultToNearest);
        return FromHandle(monitor);
    }

    public static MonitorGeometry GetMonitorFromCursor()
    {
        NativeMethods.GetCursorPos(out var point);
        return GetMonitorFromPoint(point.X, point.Y);
    }

    public static (int X, int Y) GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    private static MonitorGeometry FromHandle(nint monitor)
    {
        var info = new NativeMethods.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            throw new InvalidOperationException("无法读取显示器信息。");
        }

        var scale = 1d;
        if (NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
        {
            scale = dpiX / 96d;
        }

        return new MonitorGeometry(
            monitor,
            new PixelRect(
                info.Monitor.Left,
                info.Monitor.Top,
                info.Monitor.Width,
                info.Monitor.Height),
            new PixelRect(
                info.WorkArea.Left,
                info.WorkArea.Top,
                info.WorkArea.Width,
                info.WorkArea.Height),
            scale);
    }
}
