using System.Windows;
using System.Windows.Media.Imaging;

namespace LiangTu.App.Infrastructure;

public sealed class ClipboardService
{
    private const string SelfCopyFormat = "LiangTu.SelfCopy";

    public bool TryReadImage(out BitmapSource? image)
    {
        image = null;
        if (!System.Windows.Clipboard.ContainsImage())
        {
            return false;
        }

        var clipboardImage = System.Windows.Clipboard.GetImage();
        if (clipboardImage is null)
        {
            return false;
        }

        clipboardImage.Freeze();
        image = clipboardImage;
        return true;
    }

    public bool IsSelfCopy()
    {
        try
        {
            return System.Windows.Clipboard.ContainsData(SelfCopyFormat);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    public void CopyImage(BitmapSource image)
    {
        if (image.CanFreeze)
        {
            image.Freeze();
        }

        var data = new DataObject();
        data.SetImage(image);
        data.SetData(SelfCopyFormat, "1");
        System.Windows.Clipboard.SetDataObject(data, copy: true);
    }
}
