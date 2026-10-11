using System.Text.Json.Serialization;
namespace BT583Printer.Models;
public enum Alignment
{
    Left, Center, Right
}
public enum TextSize
{
    Small, Medium, Large
}
public enum TextMode
{
    EscPos, Raster, Automatic
}
public enum Transport
{
    WindowsDriver, WindowsRaw, Serial
}
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(QrCodeElement), "qr")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(SeparatorElement), "separator")]
[JsonDerivedType(typeof(FeedElement), "feed")]
public abstract class DocumentElement
{
    public Alignment Alignment
    {
        get; set;
    }
}
public sealed class TextElement : DocumentElement
{
    public string Text { get; set; } = ""; public bool Bold
    {
        get; set;
    }
    public bool Underline
    {
        get; set;
    }
    public TextSize Size { get; set; } = TextSize.Medium; public string Font { get; set; } = "Consolas";
}
public sealed class ImageElement : DocumentElement
{
    public byte[] Data { get; set; } = [];
}
public sealed class QrCodeElement : DocumentElement
{
    public string Content { get; set; } = ""; public TextSize Size { get; set; } = TextSize.Medium;
}
public sealed class BarcodeElement : DocumentElement
{
    public string Content { get; set; } = ""; public string Format { get; set; } = "CODE128";
}
public sealed class SeparatorElement : DocumentElement
{
}
public sealed class FeedElement : DocumentElement
{
    public int Lines { get; set; } = 1;
}
public sealed class Document
{
    public string Name { get; set; } = "Untitled"; public List<DocumentElement> Elements { get; set; } = [new TextElement()];
}
public sealed class PrinterSettings
{
    public string DefaultPrinter { get; set; } = "";
    public Transport Transport { get; set; } = Transport.WindowsDriver;
    public double PaperWidthMm { get; set; } = 58;
    public double PrintableWidthMm { get; set; } = 48;
    public int Dpi { get; set; } = 203;
    public TextMode TextMode { get; set; } = TextMode.Automatic;
    public string Encoding { get; set; } = "CP852";
    public byte CodePageCommand { get; set; } = 18;
    public bool CodePageVerified
    {
        get; set;
    }
    public bool NativeQr
    {
        get; set;
    }
    public bool Dither { get; set; } = true;
    public string Font { get; set; } = "Consolas";
    public int FeedLines { get; set; } = 3;
    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public string Theme { get; set; } = "System";
    public void Validate()
    {
        if (!double.IsFinite(PrintableWidthMm) || !Enum.IsDefined(TextMode) || !Enum.IsDefined(Transport) || PrintableWidthMm < 20 || PrintableWidthMm > 58 || PaperWidthMm != 58 || Dpi < 100 || Dpi > 600 || FeedLines < 0 || FeedLines > 5 || BaudRate < 1200 || BaudRate > 921600)
            throw new ArgumentException("Invalid width, resolution, feed, or COM baud rate settings.");
    }
}
public sealed class HistoryEntry
{
    public DateTime Date { get; set; } = DateTime.Now; public Document Document { get; set; } = new(); public int Copies { get; set; } = 1;
}
