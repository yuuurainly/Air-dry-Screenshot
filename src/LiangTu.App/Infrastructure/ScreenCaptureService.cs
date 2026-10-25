using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LiangTu.App.Infrastructure;

public static class ScreenCaptureService
{
    public static BitmapSource Capture(PixelRect rectangle)
    {
        if (rectangle.IsEmpty)
        {
            throw new ArgumentException("截图区域为空。", nameof(rectangle));
        }

        var screenDc = NativeMethods.GetDC(0);
        if (screenDc == 0)
        {
            throw new InvalidOperationException("无法访问屏幕设备上下文。");
        }

        nint memoryDc = 0;
        nint bitmap = 0;
        nint previousObject = 0;
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, rectangle.Width, rectangle.Height);
            if (memoryDc == 0 || bitmap == 0)
            {
                throw new InvalidOperationException("无法创建截图缓冲区。");
            }

            previousObject = NativeMethods.SelectObject(memoryDc, bitmap);
            if (!NativeMethods.BitBlt(
                    memoryDc,
                    0,
                    0,
                    rectangle.Width,
                    rectangle.Height,
                    screenDc,
                    rectangle.Left,
                    rectangle.Top,
                    NativeMethods.Srccopy | NativeMethods.CaptureBlt))
            {
                throw new InvalidOperationException("屏幕截图失败。");
            }

            var image = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                0,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            ImageCodec.ComputeContentHash(image);
            return image;
        }
        finally
        {
            if (previousObject != 0 && memoryDc != 0)
            {
                NativeMethods.SelectObject(memoryDc, previousObject);
            }

            if (bitmap != 0)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != 0)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    public static BitmapSource CaptureWindow(nint window)
    {
        var rectangle = GetWindowBounds(window);
        if (rectangle.IsEmpty)
        {
            throw new InvalidOperationException("无法截取该窗口。");
        }

        return Capture(rectangle);
    }

    public static PixelRect GetWindowBounds(nint window)
    {
        var windowRect = new NativeMethods.Rect();
        if (NativeMethods.DwmGetWindowAttribute(
                window,
                NativeMethods.DwmwaExtendedFrameBounds,
                out var frameBounds,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Rect>()) == 0)
        {
            windowRect = frameBounds;
        }
        else if (!NativeMethods.GetWindowRect(window, out windowRect))
        {
            return new PixelRect(0, 0, 0, 0);
        }

        return new PixelRect(windowRect.Left, windowRect.Top, windowRect.Width, windowRect.Height);
    }
}
