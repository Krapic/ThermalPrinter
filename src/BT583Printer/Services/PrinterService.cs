using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BT583Printer.Models;
using BT583Printer.Rendering;
namespace BT583Printer.Services;
public sealed class PrinterService
{
    public string[] GetPrinters()
    {
        using var server = new LocalPrintServer();
        using var queues = server.GetPrintQueues();
        return queues.Select(q => q.FullName).OrderBy(s => s).ToArray();
    }
    public void Check(PrinterSettings s)
    {
        s.Validate();
        if (s.Transport == Transport.Serial)
            return;
        if (string.IsNullOrWhiteSpace(s.DefaultPrinter))
            throw new InvalidOperationException("Printer not found.");
        using var server = new LocalPrintServer();
        using var queue = server.GetPrintQueue(s.DefaultPrinter);
        queue.Refresh();
        if (queue.IsOffline)
            throw new InvalidOperationException("Printer is offline.");
        if (queue.IsInError || queue.IsOutOfPaper || queue.IsPaperJammed)
            throw new InvalidOperationException("Printer reports an error. Check the paper and cover.");
    }
    public async Task PrintAsync(Document d, PrinterSettings s, int copies)
    {
        if (copies < 1 || copies > 99)
            throw new ArgumentException("Number of copies must be 1–99.");
        Check(s);
        var renderer = new DocumentRenderer();
        if (s.Transport == Transport.WindowsDriver)
        {
            var pages = d.Elements.Select(e => renderer.Render(e, s)).ToList();
            pages.Add(renderer.Render(new FeedElement { Lines = s.FeedLines }, s));
            var paginator = new RollPaginator(pages, s);
            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(s.DefaultPrinter);
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            var ticket = queue.DefaultPrintTicket;
            ticket.PageMediaSize = new PageMediaSize(58 * 96 / 25.4, paginator.PageSize.Height);
            ticket.PageOrientation = PageOrientation.Portrait;
            ticket.PageScalingFactor = 100;
            for (int i = 0; i < copies; i++)
                writer.Write(paginator, ticket);
        }
        else
        {
            var bytes = new EscPosEncoder().Encode(d, s, renderer);
            await Task.Run(() => { for (int i = 0; i < copies; i++) { if (s.Transport == Transport.WindowsRaw) new WindowsRawPrinterService().Send(s.DefaultPrinter, bytes); else new SerialPrinterService().Send(s.ComPort, s.BaudRate, bytes); } });
        }
    }
    private sealed class RollPaginator : DocumentPaginator
    {
        private readonly List<BitmapSource> strips = []; private readonly PrinterSettings settings;
        public RollPaginator(List<BitmapSource> images, PrinterSettings s)
        {
            settings = s;
            foreach (var image in images)
                for (int y = 0; y < image.PixelHeight; y += 1600)
                {
                    var crop = new CroppedBitmap(image, new Int32Rect(0, y, image.PixelWidth, Math.Min(1600, image.PixelHeight - y)));
                    crop.Freeze();
                    strips.Add(crop);
                }
            PageSize = new Size(58 * 96 / 25.4, Math.Max(24, strips.Sum(b => b.PixelHeight) * 96d / s.Dpi));
        }
        public override bool IsPageCountValid => true;
        public override int PageCount => 1;
        public override Size PageSize
        {
            get; set;
        }
        public override IDocumentPaginatorSource Source => null!;
        public override DocumentPage GetPage(int pageNumber)
        {
            if (pageNumber != 0)
                return DocumentPage.Missing;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double y = 0;
                double width = settings.PrintableWidthMm * 96 / 25.4;
                double x = (PageSize.Width - width) / 2;
                foreach (var strip in strips)
                {
                    double height = strip.PixelHeight * 96d / settings.Dpi;
                    dc.DrawImage(strip, new Rect(x, y, width, height));
                    y += height;
                }
            }
            return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
        }
    }
}
