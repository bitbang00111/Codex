using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace HandTrackingWpfApp.Utils;

public static class MatBitmapSourceConverter
{
    public static BitmapSource ToBitmapSource(Mat mat)
    {
        var bitmapSource = mat.ToBitmapSource();
        bitmapSource.Freeze();
        return bitmapSource;
    }
}
