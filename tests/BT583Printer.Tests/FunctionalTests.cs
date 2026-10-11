using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BT583Printer;
using BT583Printer.Models;
using BT583Printer.Rendering;
using BT583Printer.Services;
using Xunit;
namespace BT583Printer.Tests;
public sealed class FunctionalTests
{
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    [Fact]
    public void TextCommandsRespectFormatting()
    {
        var bytes = new EscPosEncoder().Text(new TextElement { Text = "ABC", Bold = true, Underline = true, Alignment = Alignment.Center, Size = TextSize.Large }, new PrinterSettings());
        Assert.Equal(new byte[] { 27, 97, 1, 27, 69, 1, 27, 45, 1, 27, 77, 0, 29, 33, 17, 27, 116, 18 }, bytes.Take(18));
        Assert.Contains("ABC\n", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.Equal(new byte[] { 27, 69, 0, 27, 45, 0, 29, 33, 0, 27, 97, 0 }, bytes.TakeLast(12));
    }
    [Fact]
    public void CommandsCannotBeInjected()
    {
        Assert.Throws<ArgumentException>(() => new EscPosEncoder().Text(new TextElement { Text = "a\u001bb" }, new()));
    }
    [Fact]
    public void RasterPacksAndChunksWithoutDroppingRows()
    {
        var encoder = new EscPosEncoder();
        byte[] pixels = Enumerable.Repeat((byte)0x80, 129).ToArray();
        var data = encoder.Raster(8, 129, pixels);
        Assert.Equal(3 + 8 + 128 + 8 + 1, data.Length);
        Assert.Equal(new byte[] { 29, 118, 48, 0, 1, 0, 128, 0 }, data.Skip(3).Take(8));
        Assert.Equal(new byte[] { 29, 118, 48, 0, 1, 0, 1, 0, 128 }, data.TakeLast(9));
    }
    [Fact] public void InvalidRasterIsRejected() => Assert.Throws<ArgumentException>(() => new EscPosEncoder().Raster(9, 1, [255]));
    [Theory][InlineData(48, 203, 384)][InlineData(58, 203, 464)][InlineData(48, 300, 567)] public void WidthUsesPhysicalUnits(double mm, int dpi, int expected) => Assert.Equal(expected, RasterRenderer.CalculateWidth(mm, dpi));
    [Fact]
    public void AutomaticAvoidsUnverifiedPrinterCharacterTables()
    {
        var encoder = new EscPosEncoder();
        var s = new PrinterSettings();
        Assert.True(encoder.CanEncode(new()
        {
            Text = "ASCII"
        }, s));
        Assert.False(encoder.CanEncode(new()
        {
            Text = "čćžšđČĆŽŠĐ"
        }, s));
        s.CodePageVerified = true;
        Assert.True(encoder.CanEncode(new()
        {
            Text = "čćžšđČĆŽŠĐ"
        }, s));
        s.Encoding = "CP437";
        Assert.False(encoder.CanEncode(new()
        {
            Text = "čćžšđ"
        }, s));
    }
    [Fact]
    public void AllElementTypesSurviveJson()
    {
        var d = new Document { Name = "Motor Test", Elements = [new TextElement { Text = "čćžšđ", Bold = true, Underline = true, Size = TextSize.Large, Font = "Arial", Alignment = Alignment.Right }, new ImageElement { Data = [1, 2, 3] }, new QrCodeElement { Content = "BT-583 TEST" }, new BarcodeElement { Content = "1234567890128", Format = "EAN13" }, new SeparatorElement(), new FeedElement { Lines = 3 }] };
        var clone = LocalStore.Clone(d);
        Assert.Equal(d.Name, clone.Name);
        Assert.Equal(d.Elements.Select(e => e.GetType()), clone.Elements.Select(e => e.GetType()));
        var text = Assert.IsType<TextElement>(clone.Elements[0]);
        Assert.Equal("čćžšđ", text.Text);
        Assert.True(text.Bold && text.Underline);
        Assert.Equal(Alignment.Right, text.Alignment);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<ImageElement>(clone.Elements[1]).Data);
    }
    [Fact]
    public void SettingsSurviveJson()
    {
        var s = new PrinterSettings { DefaultPrinter = "BT-583", Transport = Transport.Serial, ComPort = "COM8", CodePageCommand = 30, CodePageVerified = true, NativeQr = true, Theme = "Dark", PrintableWidthMm = 46, TextMode = TextMode.Raster };
        var clone = LocalStore.Clone(s);
        Assert.Equal(JsonSerializer.Serialize(s), JsonSerializer.Serialize(clone));
        clone.Validate();
    }
    [Fact]
    public void TemplateSerializationUsesEmbeddedImagesAndUnicode()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var doc = new Document { Name = "Motor", Elements = [new TextElement { Text = "Temperature: 54 °C\nčćžšđ" }, new ImageElement { Data = [42, 43] }] };
            LocalStore.Write(path, doc);
            var loaded = LocalStore.Read(path, new Document());
            Assert.Equal("Motor", loaded.Name);
            Assert.Equal(Assert.IsType<TextElement>(doc.Elements[0]).Text, Assert.IsType<TextElement>(loaded.Elements[0]).Text);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void QrNativeHasLengthAndStorePrintCommands()
    {
        var bytes = new EscPosEncoder().Qr(new()
        {
            Content = "ABC"
        });
        Assert.Contains("ABC", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 29, 40, 107, 6, 0, 49, 80, 48, 65, 66, 67 }) >= 0);
        Assert.True(bytes.AsSpan().IndexOf(new byte[] { 29, 40, 107, 3, 0, 49, 81, 48 }) >= 0);
    }
    [Fact]
    public void DocumentIsInitializedAndHasTrailingFeed()
    {
        Sta(() => { var bytes = new EscPosEncoder().Encode(new Document { Elements = [new TextElement { Text = "A" }] }, new PrinterSettings { FeedLines = 3 }, new()); Assert.Equal(new byte[] { 27, 64 }, bytes.Take(2)); Assert.Equal(new byte[] { 10, 10, 10 }, bytes.TakeLast(3)); });
    }
    [Fact]
    public void MonochromeUsesMostSignificantBitAndWhitePadding()
    {
        Sta(() => { var bitmap = BitmapSource.Create(9, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 0, 255, 0, 255, 255, 255, 255, 255, 0 }, 9); Assert.Equal(new byte[] { 160, 128 }, new RasterRenderer().Monochrome(bitmap, false)); });
    }
    [Fact]
    public void UnicodeRasterAndCodesRenderAtConfiguredWidth()
    {
        Sta(() => { var renderer = new DocumentRenderer(); var settings = new PrinterSettings(); foreach (var element in new DocumentElement[] { new TextElement { Text = "č ć ž š đ Č Ć Ž Š Đ\nTemperature 54 °C" }, new QrCodeElement { Content = "BT-583 TEST" }, new BarcodeElement { Content = "1234567890128", Format = "EAN13" }, new BarcodeElement { Content = "TORP-583" }, new SeparatorElement() }) { var bitmap = renderer.Render(element, settings); Assert.Equal(384, bitmap.PixelWidth); Assert.True(bitmap.PixelHeight > 0); var bits = new RasterRenderer().Monochrome(bitmap, true); Assert.Contains(bits, b => b != 0); } });
    }
    [Fact]
    public void InvalidEanChecksumIsRejected()
    {
        Sta(() => Assert.ThrowsAny<Exception>(() => new DocumentRenderer().Render(new BarcodeElement { Content = "1234567890123", Format = "EAN13" }, new())));
    }
    [Fact]
    public void AutomaticRasterContainsCroatianText()
    {
        Sta(() => { var settings = new PrinterSettings(); var data = new EscPosEncoder().Encode(new Document { Elements = [new TextElement { Text = "čćžšđ" }] }, settings, new()); Assert.True(data.AsSpan().IndexOf(new byte[] { 29, 118, 48, 0 }) >= 0); });
    }
    [Fact] public void InvalidPaperSettingsAreRejected() => Assert.Throws<ArgumentException>(() => new PrinterSettings { PrintableWidthMm = 70 }.Validate());
    [Fact]
    public void WindowCanRenderWithoutPrinting()
    {
        Sta(() =>
        {
            string dataRoot = Path.Combine(Path.GetTempPath(), "BT583-ui-" + Guid.NewGuid());
            try
            {
                new SettingsService(dataRoot).Save(new PrinterSettings { Transport = Transport.Serial, ComPort = "COM3" });
                var window = new MainWindow(dataRoot);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(MainWindow).GetField("document", flags)!.SetValue(window, new Document
                {
                    Name = "Motor Test",
                    Elements = [new TextElement { Text = "TORP MOTORS", Bold = true, Alignment = Alignment.Center, Size = TextSize.Large }, new SeparatorElement(), new TextElement { Text = "Printer test\n\nMotor temperature: 54 °C\nVoltage: 48.7 V\nč ć ž š đ Č Ć Ž Š Đ\n\nThank you!" }, new QrCodeElement { Content = "https://torpmotors.com", Alignment = Alignment.Center, Size = TextSize.Small }]
                });
                typeof(MainWindow).GetMethod("ReloadBlocks", flags)!.Invoke(window, new object[] { 2 });
                typeof(MainWindow).GetMethod("UpdatePreview", flags)!.Invoke(window, null);
                var config = (PrinterSettings)typeof(MainWindow).GetField("settings", flags)!.GetValue(window)!;
                foreach (string theme in new[] { "Light", "Dark" })
                {
                    config.Theme = theme;
                    typeof(MainWindow).GetMethod("ApplyTheme", flags)!.Invoke(window, null);
                    var root = (FrameworkElement)window.Content;
                    root.Measure(new Size(1172, 852));
                    root.Arrange(new Rect(0, 0, 1172, 852));
                    root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(1172, 852, 96, 96, PixelFormats.Pbgra32);
                    var background = new DrawingVisual();
                    using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)window.Resources["Surface"], null, new Rect(0, 0, 1172, 852));
                    bitmap.Render(background);
                    bitmap.Render(root);
                    var png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "ui-" + theme.ToLowerInvariant() + ".png"));
                    png.Save(stream);
                }
                Assert.Equal(Visibility.Collapsed, ((System.Windows.Controls.ComboBox)window.FindName("Printers")).Visibility);
                Assert.Contains("COM3", ((System.Windows.Controls.TextBlock)window.FindName("SerialTargetLabel")).Text);
                var minimumRoot = (FrameworkElement)window.Content;
                minimumRoot.Measure(new Size(972, 752));
                minimumRoot.Arrange(new Rect(0, 0, 972, 752));
                minimumRoot.UpdateLayout();
                Assert.True(((FrameworkElement)window.FindName("Editor")).ActualHeight > 50, "Editor must remain usable at the minimum window size.");
                window.Close();
            }
            finally { if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true); }
        });
    }
    [Fact]
    public void LocalServicesRoundTripAndPruneHistory()
    {
        string root = Path.Combine(Path.GetTempPath(), "BT583-tests-" + Guid.NewGuid());
        try
        {
            var settings = new SettingsService(root);
            settings.Save(new PrinterSettings { DefaultPrinter = "BT-583", TextMode = TextMode.Raster });
            Assert.Equal("BT-583", settings.Load().DefaultPrinter);
            var templates = new TemplateService(root);
            var doc = new Document { Name = "Motor Test", Elements = [new TextElement { Text = "čćžšđ" }] };
            templates.Save(doc);
            Assert.Single(templates.List());
            Assert.Equal("Motor Test", templates.Load(templates.List()[0]).Name);
            var history = new HistoryService(root);
            for (int i = 0; i < 102; i++)
            {
                doc.Name = "Print " + i;
                history.Add(doc, 2);
            }
            var entries = history.Load();
            Assert.Equal(100, entries.Count);
            Assert.Equal("Print 101", entries[0].Document.Name);
            Assert.Equal("Print 2", entries[^1].Document.Name);
            Assert.Equal(2, entries[0].Copies);
            templates.Delete(templates.List()[0]);
            Assert.Empty(templates.List());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void CorruptJsonIsPreserved()
    {
        string root = Path.Combine(Path.GetTempPath(), "BT583-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, "invalid");
            Assert.Throws<IOException>(() => new SettingsService(root).Load());
            Assert.Equal("invalid", File.ReadAllText(path));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void ImageIsScaledAndConvertedToThermalMonochrome()
    {
        Sta(() => { var image = BitmapSource.Create(800, 200, 96, 96, PixelFormats.Gray8, null, Enumerable.Repeat((byte)100, 160000).ToArray(), 800); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = new MemoryStream(); encoder.Save(stream); var rendered = new DocumentRenderer().Render(new ImageElement { Data = stream.ToArray() }, new()); Assert.Equal(384, rendered.PixelWidth); Assert.Equal(96, rendered.PixelHeight); var pixels = new byte[384 * 96]; rendered.CopyPixels(pixels, 384, 0); Assert.All(pixels, p => Assert.True(p == 0 || p == 255)); });
    }
    [Fact]
    public void StudioActionsDuplicateIndependentlyAndClampCopies()
    {
        Sta(() =>
        {
            string root = Path.Combine(Path.GetTempPath(), "BT583-studio-" + Guid.NewGuid());
            try
            {
                var window = new MainWindow(root);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var document = (Document)typeof(MainWindow).GetField("document", flags)!.GetValue(window)!;
                var original = (TextElement)document.Elements[0];
                original.Text = "Motor";
                typeof(MainWindow).GetMethod("DuplicateBlock", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                var duplicate = Assert.IsType<TextElement>(document.Elements[1]);
                Assert.NotSame(original, duplicate);
                duplicate.Text = "Changed";
                Assert.Equal("Motor", original.Text);
                Assert.StartsWith("●", window.Title);
                var copies = (System.Windows.Controls.TextBox)window.FindName("Copies");
                copies.Text = "99";
                typeof(MainWindow).GetMethod("CopiesUp", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                Assert.Equal("99", copies.Text);
                copies.Text = "1";
                typeof(MainWindow).GetMethod("CopiesDown", flags)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                Assert.Equal("1", copies.Text);
                var zoom = (System.Windows.Controls.Slider)window.FindName("Zoom");
                zoom.Value = 125;
                Assert.Equal(1.25, ((ScaleTransform)((FrameworkElement)window.FindName("Paper")).LayoutTransform).ScaleX);
                typeof(MainWindow).GetProperty("dirty", flags)!.SetValue(window, false);
                window.Close();
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
    }
}
