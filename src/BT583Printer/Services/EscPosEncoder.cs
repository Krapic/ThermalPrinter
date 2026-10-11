using System.Text;
using BT583Printer.Models;
using BT583Printer.Rendering;
namespace BT583Printer.Services;
public sealed class EscPosEncoder
{
    public static Encoding GetEncoding(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(name switch
        {
            "CP437" => 437,
            "CP850" => 850,
            "CP852" => 852,
            "Windows-1250" => 1250,
            "UTF-8" => 65001,
            _ => throw new ArgumentException("Unknown encoding.")
        }, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
    public bool CanEncode(TextElement t, PrinterSettings s)
    {
        if (t.Text.Any(c => c > 127) && !s.CodePageVerified)
            return false;
        try
        {
            GetEncoding(s.Encoding).GetBytes(t.Text);
            return true;
        }
        catch (EncoderFallbackException) { return false; }
    }
    public byte[] Text(TextElement t, PrinterSettings s)
    {
        // Never allow user text to inject device commands.
        if (t.Text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
            throw new ArgumentException("Text contains a disallowed control character.");
        using var m = new MemoryStream();
        m.Write(new byte[] { 27, 97, (byte)t.Alignment, 27, 69, (byte)(t.Bold ? 1 : 0), 27, 45, (byte)(t.Underline ? 1 : 0), 27, 77, (byte)(t.Size == TextSize.Small ? 1 : 0), 29, 33, (byte)(t.Size == TextSize.Large ? 17 : 0), 27, 116, s.CodePageCommand });
        m.Write(GetEncoding(s.Encoding).GetBytes(t.Text.Replace("\r", "").Replace("\t", "    ") + "\n"));
        m.Write(new byte[] { 27, 69, 0, 27, 45, 0, 29, 33, 0, 27, 97, 0 });
        return m.ToArray();
    }
    public byte[] Raster(int width, int height, byte[] bits)
    {
        int stride = (width + 7) / 8;
        if (width < 1 || height < 1 || stride * height != bits.Length)
            throw new ArgumentException("Invalid raster.");
        using var m = new MemoryStream();
        m.Write(new byte[] { 27, 97, 0 });
        for (int y = 0; y < height; y += 128)
        {
            int rows = Math.Min(128, height - y);
            m.Write(new byte[] { 29, 118, 48, 0, (byte)stride, (byte)(stride >> 8), (byte)rows, (byte)(rows >> 8) });
            m.Write(bits, y * stride, rows * stride);
        }
        return m.ToArray();
    }
    public byte[] Qr(QrCodeElement q)
    {
        var data = Encoding.UTF8.GetBytes(q.Content);
        if (data.Length > 2953)
            throw new ArgumentException("QR content is too long.");
        using var m = new MemoryStream();
        m.Write(new byte[] { 27, 97, (byte)q.Alignment });
        void Command(byte[] body)
        {
            int n = body.Length;
            m.Write(new byte[] { 29, 40, 107, (byte)n, (byte)(n >> 8) });
            m.Write(body);
        }
        Command([49, 65, 50, 0]);
        Command([49, 67, (byte)(q.Size == TextSize.Small ? 3 : q.Size == TextSize.Large ? 7 : 5)]);
        Command([49, 69, 48]);
        Command(new byte[] { 49, 80, 48 }.Concat(data).ToArray());
        Command([49, 81, 48]);
        m.Write(new byte[] { 10, 27, 97, 0 });
        return m.ToArray();
    }
    public byte[] Encode(Document document, PrinterSettings s, DocumentRenderer renderer)
    {
        s.Validate();
        using var m = new MemoryStream();
        m.Write(new byte[] { 27, 64 });
        var raster = new RasterRenderer();
        foreach (var e in document.Elements)
        {
            if (e is FeedElement feed)
            {
                m.Write(Enumerable.Repeat((byte)10, Math.Clamp(feed.Lines, 0, 100)).ToArray());
                continue;
            }
            byte[] data;
            if (e is TextElement t && s.TextMode != TextMode.Raster && (s.TextMode == TextMode.EscPos || CanEncode(t, s)))
                data = Text(t, s);
            else if (e is QrCodeElement q && s.NativeQr && s.TextMode != TextMode.Raster)
                data = Qr(q);
            else
            {
                var bitmap = renderer.Render(e, s);
                data = Raster(bitmap.PixelWidth, bitmap.PixelHeight, raster.Monochrome(bitmap, s.Dither));
            }
            m.Write(data);
        }
        m.Write(Enumerable.Repeat((byte)10, s.FeedLines).ToArray());
        return m.ToArray();
    }
}
