using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BT583Printer.Models;
using ZXing;
using ZXing.Common;
namespace BT583Printer.Rendering;
public sealed class RasterRenderer
{
    public static int CalculateWidth(double mm, int dpi) => Math.Clamp((int)Math.Round(mm * dpi / 25.4), 1, 4096);
    public byte[] Monochrome(BitmapSource source, bool dither)
    {
        var gray = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        int w = gray.PixelWidth, h = gray.PixelHeight;
        var pixels = new byte[w * h];
        gray.CopyPixels(pixels, w, 0);
        var work = pixels.Select(x => (double)x).ToArray();
        int stride = (w + 7) / 8;
        var bits = new byte[stride * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                double old = work[i];
                bool black = old < 128;
                if (black)
                    bits[y * stride + x / 8] |= (byte)(128 >> (x % 8));
                if (!dither)
                    continue;
                double error = old - (black ? 0 : 255);
                void Add(int xx, int yy, double weight)
                {
                    if (xx >= 0 && xx < w && yy < h)
                        work[yy * w + xx] += error * weight;
                }
                Add(x + 1, y, 7d / 16);
                Add(x - 1, y + 1, 3d / 16);
                Add(x, y + 1, 5d / 16);
                Add(x + 1, y + 1, 1d / 16);
            }
        return bits;
    }
}
