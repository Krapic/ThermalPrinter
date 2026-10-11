using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BT583Printer.Models;
using ZXing;
using ZXing.Common;
namespace BT583Printer.Rendering;
public sealed class DocumentRenderer
{
    public BitmapSource Render(DocumentElement element, PrinterSettings settings)
    {
        int w = RasterRenderer.CalculateWidth(settings.PrintableWidthMm, settings.Dpi);
        double height = 24;
        FormattedText? text = null;
        BitmapSource? image = null;
        if (element is TextElement t)
        {
            text = new FormattedText(t.Text.Length == 0 ? " " : t.Text.Replace("\r", ""), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily(t.Font), FontStyles.Normal, t.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), t.Size switch
            {
                TextSize.Small => 18,
                TextSize.Large => 40,
                _ => 24
            }, Brushes.Black, 1);
            text.MaxTextWidth = w;
            text.TextAlignment = ToAlignment(t.Alignment);
            if (t.Underline)
                text.SetTextDecorations(TextDecorations.Underline);
            height = Math.Ceiling(text.Height) + 6;
        }
        else if (element is ImageElement img)
        {
            using var stream = new MemoryStream(img.Data);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            image = decoder.Frames[0];
            height = Math.Ceiling(image.PixelHeight * Math.Min(1d, (double)w / image.PixelWidth));
        }
        else if (element is QrCodeElement qr)
        {
            int size = Math.Min(w, qr.Size switch
            {
                TextSize.Small => 120,
                TextSize.Large => 320,
                _ => 200
            });
            image = Code(qr.Content, BarcodeFormat.QR_CODE, size, size);
            height = image.PixelHeight + 16;
        }
        else if (element is BarcodeElement barcode)
        {
            image = Code(barcode.Content, barcode.Format == "EAN13" ? BarcodeFormat.EAN_13 : BarcodeFormat.CODE_128, w, 90);
            if (image.PixelWidth > w)
                throw new ArgumentException("Barcode is too long for the selected paper width.");
            height = 124;
        }
        else if (element is FeedElement feed)
            height = 24 * Math.Clamp(feed.Lines, 0, 100);
        if (height > 60000)
            throw new ArgumentException("Block is too long. Split the text into multiple blocks.");
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, Math.Max(1, height)));
            if (text != null)
                dc.DrawText(text, new Point(0, 0));
            if (image != null)
            {
                double scale = Math.Min(1d, (double)w / image.PixelWidth);
                double iw = image.PixelWidth * scale, ih = image.PixelHeight * scale;
                double x = element.Alignment switch
                {
                    Alignment.Center => (w - iw) / 2,
                    Alignment.Right => w - iw,
                    _ => 0
                };
                dc.DrawImage(image, new Rect(x, element is QrCodeElement ? 8 : 0, iw, ih));
                if (element is BarcodeElement b)
                {
                    var label = new FormattedText(b.Content, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 18, Brushes.Black, 1) { MaxTextWidth = w, TextAlignment = TextAlignment.Center };
                    dc.DrawText(label, new Point(0, 94));
                }
            }
            if (element is SeparatorElement)
                dc.DrawLine(new Pen(Brushes.Black, 2), new Point(0, 12), new Point(w, 12));
        }
        var output = new RenderTargetBitmap(w, Math.Max(1, (int)height), 96, 96, PixelFormats.Pbgra32);
        output.Render(visual);
        output.Freeze();
        var packed = new RasterRenderer().Monochrome(output, settings.Dither);
        var mono = new byte[w * output.PixelHeight];
        int stride = (w + 7) / 8;
        for (int y = 0; y < output.PixelHeight; y++)
            for (int x = 0; x < w; x++)
                mono[y * w + x] = (packed[y * stride + x / 8] & (128 >> (x % 8))) != 0 ? (byte)0 : (byte)255;
        var final = BitmapSource.Create(w, output.PixelHeight, 96, 96, PixelFormats.Gray8, null, mono, w);
        final.Freeze();
        return final;
    }
    public static TextAlignment ToAlignment(Alignment a) => a switch { Alignment.Center => TextAlignment.Center, Alignment.Right => TextAlignment.Right, _ => TextAlignment.Left };
    private static BitmapSource Code(string content, BarcodeFormat format, int width, int height)
    {
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Code content is empty.");
        var matrix = new MultiFormatWriter().encode(content, format, width, height, new Dictionary<EncodeHintType, object> { { EncodeHintType.MARGIN, format == BarcodeFormat.QR_CODE ? 4 : 10 }, { EncodeHintType.CHARACTER_SET, "UTF-8" } });
        int w = matrix.Width, h = matrix.Height;
        var data = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                data[y * w + x] = matrix[x, y] ? (byte)0 : (byte)255;
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, data, w);
        bmp.Freeze();
        return bmp;
    }
}
