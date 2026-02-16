using OpenCvSharp;
using System;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace HandTrackingWpfApp.Utils;

public static class MatBitmapSourceConverter
{
    public static BitmapSource ToBitmapSource(Mat mat)
    {
        if (mat == null || mat.Empty())
            throw new ArgumentNullException(nameof(mat));

        PixelFormat pixelFormat;
        int bytesPerPixel;

        switch (mat.Type().Depth)
        {
            case MatType.CV_8U:
                switch (mat.Channels())
                {
                    case 1:
                        pixelFormat = PixelFormats.Gray8;
                        bytesPerPixel = 1;
                        break;
                    case 3:
                        pixelFormat = PixelFormats.Bgr24;
                        bytesPerPixel = 3;
                        break;
                    case 4:
                        pixelFormat = PixelFormats.Bgra32;
                        bytesPerPixel = 4;
                        break;
                    default:
                        throw new NotSupportedException("Unsupported channel count: " + mat.Channels());
                }
                break;
            default:
                throw new NotSupportedException("Unsupported Mat depth: " + mat.Type().Depth);
        }

        int width = mat.Width;
        int height = mat.Height;
        int stride = width * bytesPerPixel;

        // If Mat is not continuous, clone to make it so
        Mat continuousMat = mat.IsContinuous() ? mat : mat.Clone();

        BitmapSource bitmapSource = BitmapSource.Create(
            width,
            height,
            96, // dpiX
            96, // dpiY
            pixelFormat,
            null,
            continuousMat.Data,
            height * stride,
            stride
        );
        bitmapSource.Freeze();
        return bitmapSource;
    }
}
