using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using BT583Printer.Models;
using BT583Printer.Rendering;
using BT583Printer.Services;
namespace BT583Printer;
public partial class MainWindow : Window
{
    private Document document = new(); private PrinterSettings settings = new(); private readonly SettingsService settingsService; private readonly TemplateService templates; private readonly HistoryService history; private readonly PrinterService printer = new(); private readonly DocumentRenderer renderer = new();
    private bool syncing, busy, isDirty;
    private bool dirty { get => isDirty; set { isDirty = value; UpdateDocumentState(); } } private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DocumentElement? Selected => Blocks.SelectedIndex >= 0 && Blocks.SelectedIndex < document.Elements.Count ? document.Elements[Blocks.SelectedIndex] : null;
    private sealed record HistoryRow(HistoryEntry Entry)
    {
        public string Label => $"{Entry.Date:dd.MM.yyyy. HH:mm:ss}   {Entry.Document.Name}   ({Entry.Copies} copies)\n{string.Join(" • ", Entry.Document.Elements.Select(Describe)).Replace("\n", " ")}";
    }
    public MainWindow() : this(null) { }
    public MainWindow(string? dataRoot)
    {
        settingsService = new SettingsService(dataRoot);
        templates = new TemplateService(dataRoot);
        history = new HistoryService(dataRoot);
        InitializeComponent();
        syncing = true;
        try
        {
            settings = settingsService.Load();
        }
        catch (Exception e) { Error(e); }
        SizeChoice.ItemsSource = Enum.GetNames<TextSize>();
        AlignChoice.ItemsSource = Enum.GetNames<Alignment>();
        FontChoice.ItemsSource = new[] { "Arial", "Calibri", "Consolas", "Segoe UI", "Times New Roman" };
        Feed.ItemsSource = Enumerable.Range(0, 6);
        Feed.SelectedItem = settings.FeedLines;
        RenderImage.IsChecked = settings.TextMode == TextMode.Raster;
        syncing = false;
        timer.Tick += (_, _) => { timer.Stop(); UpdatePreview(); };
        Refresh(this, new RoutedEventArgs());
        ReloadBlocks(0);
        LoadHistory();
        ApplyTheme();
        Bind(Key.D, ModifierKeys.Control, () => DuplicateBlock(this, new()));
        Bind(Key.N, ModifierKeys.Control, () => NewClick(this, new()));
        Bind(Key.S, ModifierKeys.Control, () => SaveClick(this, new()));
        Bind(Key.O, ModifierKeys.Control, () => LoadClick(this, new()));
        Bind(Key.P, ModifierKeys.Control, () => PrintClick(this, new()));
        Bind(Key.P, ModifierKeys.Control | ModifierKeys.Shift, () => TestClick(this, new()));
        Closing += (_, e) => { if (busy) { e.Cancel = true; return; } if (!Discard()) e.Cancel = true; };
    }
    private void Bind(Key key, ModifierKeys modifiers, Action action)
    {
        var command = new RoutedCommand();
        InputBindings.Add(new KeyBinding(command, key, modifiers));
        CommandBindings.Add(new CommandBinding(command, (_, _) => { if (!busy) action(); }));
    }
    private static string Describe(DocumentElement e) => e switch { TextElement t => "Text · " + (string.IsNullOrEmpty(t.Text) ? "(empty block)" : t.Text.Split('\n')[0]), QrCodeElement q => "QR · " + q.Content, BarcodeElement b => b.Format + " · " + b.Content, ImageElement => "Image", SeparatorElement => "──── Line", FeedElement f => $"Feed · {f.Lines}", _ => "Block" };
    private void ReloadBlocks(int selected)
    {
        syncing = true;
        Blocks.ItemsSource = new ObservableCollection<string>(document.Elements.Select(Describe));
        Blocks.SelectedIndex = document.Elements.Count == 0 ? -1 : Math.Clamp(selected, 0, document.Elements.Count - 1);
        syncing = false;
        SyncEditor();
        SchedulePreview();
    }
    private void BlockSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!syncing)
            SyncEditor();
    }
    private void SyncEditor()
    {
        syncing = true;
        Editor.IsEnabled = Selected is TextElement;
        Editor.Text = Selected is TextElement t ? t.Text : "This block has no text editor. Use Edit Code to edit QR codes and barcodes.";
        bool isText = Selected is TextElement;
        BoldButton.IsEnabled = UnderlineButton.IsEnabled = NormalButton.IsEnabled = SizeChoice.IsEnabled = FontChoice.IsEnabled = isText;
        AlignChoice.IsEnabled = RemoveButton.IsEnabled = DuplicateButton.IsEnabled = Selected != null;
        EditCodeButton.IsEnabled = Selected is QrCodeElement or BarcodeElement;
        BoldButton.SetResourceReference(BackgroundProperty, Selected is TextElement { Bold: true } ? "AccentSoft" : "ControlFill");
        UnderlineButton.SetResourceReference(BackgroundProperty, Selected is TextElement { Underline: true } ? "AccentSoft" : "ControlFill");
        UpdateDocumentState();
        if (Selected is TextElement text)
        {
            SizeChoice.SelectedItem = text.Size.ToString();
            FontChoice.SelectedItem = text.Font;
        }
        AlignChoice.SelectedItem = (Selected?.Alignment ?? Alignment.Left).ToString();
        syncing = false;
    }
    private void EditorChanged(object sender, TextChangedEventArgs e)
    {
        if (syncing || Selected is not TextElement text)
            return;
        text.Text = Editor.Text;
        int index = Blocks.SelectedIndex;
        if (Blocks.ItemsSource is ObservableCollection<string> labels && index >= 0)
        {
            syncing = true;
            labels[index] = Describe(text);
            Blocks.SelectedIndex = index;
            syncing = false;
        }
        dirty = true;
        SchedulePreview();
    }
    private void SchedulePreview()
    {
        timer.Stop();
        timer.Start();
    }
    private void UpdatePreview()
    {
        try
        {
            Preview.Children.Clear();
            foreach (var element in document.Elements)
            {
                var bitmap = renderer.Render(element, settings);
                var image = new Image { Source = bitmap, Width = 254, Height = bitmap.PixelHeight * 254d / bitmap.PixelWidth, Stretch = Stretch.Fill };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                int index = document.Elements.IndexOf(element);
                image.Cursor = Cursors.Hand; image.ToolTip = "Select this block in the editor";
                image.MouseLeftButtonDown += (_, _) => { Blocks.SelectedIndex = index; Blocks.ScrollIntoView(Blocks.SelectedItem); Editor.Focus(); };
                Preview.Children.Add(image);
            }
            Preview.Children.Add(new Border { Height = settings.FeedLines * 16 });
            PreviewInfo.Text = $"{settings.PrintableWidthMm:0.#} mm printable width · {document.Elements.Count} blocks · click an element to edit";
        }
        catch (Exception e) { LocalStore.Log(e); Preview.Children.Add(new TextBlock { Text = "Preview: " + e.Message, Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap }); }
    }
    private void UpdateDocumentState()
    {
        if (DocumentInfo == null) return;
        string name = document.Name == "Untitled" ? "New document" : document.Name;
        Title = (dirty ? "● " : "") + name + " — BT-583 Studio";
        DocumentInfo.Text = name + (dirty ? " · Unsaved" : "") + " · " + document.Elements.Count + " blocks";
        DocumentInfo.ToolTip = DocumentInfo.Text;
    }
    private void DuplicateBlock(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return;
        Insert(LocalStore.Clone<DocumentElement>(Selected));
    }
    private void CopiesDown(object sender, RoutedEventArgs e) => ChangeCopies(-1);
    private void CopiesUp(object sender, RoutedEventArgs e) => ChangeCopies(1);
    private void ChangeCopies(int delta)
    {
        int value = int.TryParse(Copies.Text, out int parsed) ? parsed : 1;
        Copies.Text = Math.Clamp(value + delta, 1, 99).ToString();
    }
    private void FeedChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || Feed.SelectedItem is not int value) return;
        settings.FeedLines = value;
        try { settingsService.Save(settings); } catch (Exception ex) { Error(ex); }
        SchedulePreview();
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Paper == null || ZoomLabel == null) return;
        Paper.LayoutTransform = new ScaleTransform(e.NewValue / 100, e.NewValue / 100);
        ZoomLabel.Text = e.NewValue.ToString("0") + " %";
    }
    private void FormatClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not TextElement t)
            return;
        switch ((sender as Button)?.Tag)
        {
            case "Bold":
                t.Bold = !t.Bold;
                break;
            case "Underline":
                t.Underline = !t.Underline;
                break;
            case "Normal":
                t.Bold = false;
                t.Underline = false;
                t.Size = TextSize.Medium;
                break;
        }
        dirty = true;
        SyncEditor();
        SchedulePreview();
    }
    private void StyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || Selected == null)
            return;
        if (AlignChoice.SelectedItem is string a)
            Selected.Alignment = Enum.Parse<Alignment>(a);
        if (Selected is TextElement t)
        {
            if (SizeChoice.SelectedItem is string size)
                t.Size = Enum.Parse<TextSize>(size);
            if (FontChoice.SelectedItem is string font)
                t.Font = font;
        }
        dirty = true;
        SchedulePreview();
    }
    private void Insert(DocumentElement element)
    {
        int index = Blocks.SelectedIndex < 0 ? document.Elements.Count : Blocks.SelectedIndex + 1;
        document.Elements.Insert(index, element);
        dirty = true;
        ReloadBlocks(index);
    }
    private void AddText(object sender, RoutedEventArgs e) => Insert(new TextElement { Font = settings.Font });
    private void AddLine(object sender, RoutedEventArgs e) => Insert(new SeparatorElement());
    private void AddQr(object sender, RoutedEventArgs e) => CodeDialog(null, false);
    private void AddBarcode(object sender, RoutedEventArgs e) => CodeDialog(null, true);
    private void EditCode(object sender, RoutedEventArgs e)
    {
        if (Selected is QrCodeElement or BarcodeElement)
            CodeDialog(Selected, Selected is BarcodeElement);
    }
    private void CodeDialog(DocumentElement? existing, bool barcode)
    {
        var dialog = new FormDialog(this, barcode ? "Barcode" : "QR Code");
        var content = dialog.Text("Content", existing switch
        {
            QrCodeElement q => q.Content,
            BarcodeElement b => b.Content,
            _ => ""
        });
        var choice = dialog.Choice(barcode ? "Format" : "Size", barcode ? new[] { "CODE128", "EAN13" } : Enum.GetNames<TextSize>(), existing switch
        {
            BarcodeElement b => b.Format,
            QrCodeElement q => q.Size.ToString(),
            _ => barcode ? "CODE128" : "Medium"
        });
        if (!dialog.Accept("Insert"))
            return;
        try
        {
            DocumentElement element = barcode ? new BarcodeElement { Content = content.Text, Format = (string)choice.SelectedItem, Alignment = Alignment.Center } : new QrCodeElement { Content = content.Text, Size = Enum.Parse<TextSize>((string)choice.SelectedItem), Alignment = Alignment.Center };
            renderer.Render(element, settings);
            if (existing == null)
                Insert(element);
            else
            {
                int index = document.Elements.IndexOf(existing);
                document.Elements[index] = element;
                dirty = true;
                ReloadBlocks(index);
            }
        }
        catch (Exception ex) { Error(ex); }
    }
    private void AddImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 20 * 1024 * 1024)
                throw new ArgumentException("Image is too large (maximum 20 MB).");
            var element = new ImageElement { Data = File.ReadAllBytes(dialog.FileName), Alignment = Alignment.Center };
            renderer.Render(element, settings);
            Insert(element);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void Move(int delta)
    {
        int index = Blocks.SelectedIndex, next = index + delta;
        if (index < 0 || next < 0 || next >= document.Elements.Count)
            return;
        (document.Elements[index], document.Elements[next]) = (document.Elements[next], document.Elements[index]);
        dirty = true;
        ReloadBlocks(next);
    }
    private void MoveUp(object s, RoutedEventArgs e) => Move(-1); private void MoveDown(object s, RoutedEventArgs e) => Move(1);
    private void RemoveBlock(object s, RoutedEventArgs e)
    {
        int index = Blocks.SelectedIndex;
        if (index < 0)
            return;
        document.Elements.RemoveAt(index);
        dirty = true;
        ReloadBlocks(index);
    }
    private bool Discard() => !dirty || MessageBox.Show(this, "Discard unsaved document changes?", "BT-583", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void NewClick(object sender, RoutedEventArgs e)
    {
        if (!Discard())
            return;
        document = new Document();
        dirty = false;
        ReloadBlocks(0);
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new FormDialog(this, "Save Template");
        var name = dialog.Text("Name", document.Name);
        if (!dialog.Accept())
            return;
        try
        {
            var copy = LocalStore.Clone(document);
            copy.Name = name.Text.Trim();
            if (templates.List().Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), copy.Name, StringComparison.OrdinalIgnoreCase)) && MessageBox.Show(this, "Replace the existing template?", "Save", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            templates.Save(copy);
            document.Name = copy.Name;
            dirty = false;
            Status.Text = "Template saved locally.";
        }
        catch (Exception ex) { Error(ex); }
    }
    private string? ChooseTemplate(string title)
    {
        var paths = templates.List();
        if (paths.Length == 0)
        {
            MessageBox.Show(this, "No saved templates.");
            return null;
        }
        var dialog = new FormDialog(this, title);
        var choice = dialog.Choice("Template", paths.Select(Path.GetFileNameWithoutExtension)!, Path.GetFileNameWithoutExtension(paths[0]));
        return dialog.Accept("Select") ? paths[choice.SelectedIndex] : null;
    }
    private void LoadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ChooseTemplate("Load Template");
            if (path == null || !Discard())
                return;
            document = templates.Load(path);
            dirty = false;
            ReloadBlocks(0);
            Tabs.SelectedIndex = 0;
        }
        catch (Exception ex) { Error(ex); }
    }
    private void DeleteTemplateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ChooseTemplate("Delete Template");
            if (path != null && MessageBox.Show(this, "Delete template " + Path.GetFileNameWithoutExtension(path) + "?", "Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                templates.Delete(path);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void LoadHistory()
    {
        try
        {
            HistoryList.ItemsSource = history.Load().Select(h => new HistoryRow(h)).ToArray();
        }
        catch (Exception e) { Error(e); }
    }
    private void HistoryOpen(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryRow row || !Discard())
            return;
        document = LocalStore.Clone(row.Entry.Document);
        dirty = false;
        ReloadBlocks(0);
        Tabs.SelectedIndex = 0;
    }
    private async void PrintAgain(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryRow row)
            await PrintDocument(row.Entry.Document);
    }
    private void Refresh(object sender, RoutedEventArgs e)
    {
        try
        {
            syncing = true;
            var names = printer.GetPrinters();
            Printers.ItemsSource = names;
            Printers.SelectedItem = names.Contains(settings.DefaultPrinter) ? settings.DefaultPrinter : names.FirstOrDefault();
            syncing = false;
            if (Printers.SelectedItem is string name)
            {
                settings.DefaultPrinter = name;
                settingsService.Save(settings);
            }
            UpdateConnection();
        }
        catch (Exception ex) { syncing = false; Error(ex); }
    }
    private void PrinterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || Printers.SelectedItem is not string name)
            return;
        settings.DefaultPrinter = name;
        try
        {
            settingsService.Save(settings);
        }
        catch (Exception ex) { Error(ex); }
    }
    private void RasterChanged(object sender, RoutedEventArgs e)
    {
        if (syncing)
            return;
        settings.TextMode = RenderImage.IsChecked == true ? TextMode.Raster : TextMode.Automatic;
        try
        {
            settingsService.Save(settings);
        }
        catch (Exception ex) { Error(ex); }
        SchedulePreview();
    }
    private async void PrintClick(object sender, RoutedEventArgs e) => await PrintDocument(document);
    private async void TestClick(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        await PrintDocument(new Document { Name = "BT-583 TEST", Elements = [new TextElement { Text = "BT-583 TEST", Bold = true, Alignment = Alignment.Center }, new TextElement { Text = $"Printer connection OK\nDate: {now:dd.MM.yyyy.}\nTime: {now:HH:mm:ss}\nABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789\nč ć ž š đ Č Ć Ž Š Đ" }, new SeparatorElement(), new QrCodeElement { Content = "BT-583 TEST", Alignment = Alignment.Center }] });
    }
    private async Task PrintDocument(Document d)
    {
        if (busy)
            return;
        try
        {
            if (!int.TryParse(Copies.Text, out var copies) || copies < 1 || copies > 99)
                throw new ArgumentException("Copies must be a number from 1 to 99.");
            settings.FeedLines = Feed.SelectedItem is int feed ? feed : 3;
            settingsService.Save(settings);
            var snapshot = LocalStore.Clone(d);
            var config = LocalStore.Clone(settings);
            busy = true;
            PrintButton.IsEnabled = TestButton.IsEnabled = false;
            Status.Text = "Sending document to printer…";
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => { try { printer.PrintAsync(snapshot, config, copies).GetAwaiter().GetResult(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await completion.Task;
            Status.Text = "Sent to printer · Ready for the next print.";
            try
            {
                history.Add(snapshot, copies);
                LoadHistory();
            }
            catch (Exception ex) { LocalStore.Log(ex); MessageBox.Show(this, "The print job was submitted, but history could not be saved. Do not print again because of this message."); }
        }
        catch (Exception ex) { Error(ex); }
        finally { busy = false; PrintButton.IsEnabled = TestButton.IsEnabled = true; }
    }
    private void Error(Exception e)
    {
        LocalStore.Log(e);
        Status.Text = "Action failed · Check the displayed message.";
        string message = e is ArgumentException or InvalidOperationException ? e.Message : e is System.IO.IOException && e.Message.StartsWith("Local file") ? e.Message : settings.Transport == Transport.Serial ? "Bluetooth/COM port is unavailable or transmission failed." : "Could not send data to the printer or access local data. Check the selected printer and connection.";
        MessageBox.Show(this, message, "BT-583 Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void ApplyTheme()
    {
        bool dark = settings.Theme == "Dark";
        if (settings.Theme == "System")
        {
            try
            {
                dark = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0;
            }
            catch { }
        }
        void Brush(string key, string light, string night) => Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        Brush("Surface", "#F3F5FA", "#111827");
        Brush("Card", "#FFFFFF", "#1B2535");
        Brush("ControlFill", "#F5F7FB", "#222F42");
        Brush("Ink", "#17233A", "#E8EDF5");
        Brush("Muted", "#64748B", "#A4B2C8");
        Brush("Stroke", "#DCE3EE", "#344155");
        Brush("Hover", "#E7EDF8", "#2D3D54");
        Brush("AccentSoft", "#DBEAFE", "#254572");
        Brush("PreviewFill", "#E9EEF5", "#121C2B");
    }
    private void UpdateConnection()
    {
        bool serial = settings.Transport == Transport.Serial;
        Printers.Visibility = serial ? Visibility.Collapsed : Visibility.Visible;
        SerialTarget.Visibility = serial ? Visibility.Visible : Visibility.Collapsed;
        SerialTargetLabel.Text = $"BT-583  ·  {settings.ComPort}  ·  {settings.BaudRate} baud";
        Connection.Text = settings.Transport switch
        {
            Transport.Serial => "Bluetooth / COM",
            Transport.WindowsRaw => "RAW ESC/POS",
            _ => "Windows driver"
        };
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        if (busy)
            return;
        var dialog = new FormDialog(this, "Settings");
        var transport = dialog.Choice("Connection", Enum.GetNames<Transport>(), settings.Transport.ToString());
        dialog.Note("WindowsDriver: standard Windows printing. WindowsRaw / Serial: direct ESC/POS. Paper: 58 mm. Verify native command support on your firmware.");
        var width = dialog.Text("Printable width (mm, 20–58)", settings.PrintableWidthMm.ToString(CultureInfo.InvariantCulture));
        var dpi = dialog.Text("Printer DPI", settings.Dpi.ToString());
        var mode = dialog.Choice("Text printing mode", Enum.GetNames<TextMode>(), settings.TextMode.ToString());
        var font = dialog.Choice("Default raster font", new[] { "Arial", "Calibri", "Consolas", "Segoe UI", "Times New Roman" }, settings.Font);
        var encoding = dialog.Choice("Encoding", new[] { "CP437", "CP850", "CP852", "Windows-1250", "UTF-8" }, settings.Encoding);
        var page = dialog.Text("ESC t code page ID (0–255, printer-specific)", settings.CodePageCommand.ToString());
        var verified = dialog.Check("Code page / UTF-8 support verified on the printer", settings.CodePageVerified);
        var qr = dialog.Check("Printer supports native ESC/POS QR", settings.NativeQr);
        var dither = dialog.Check("Floyd–Steinberg dithering", settings.Dither);
        var port = dialog.Text("Bluetooth / serial COM port", settings.ComPort);
        var baud = dialog.Text("Baud rate (8N1, no handshake)", settings.BaudRate.ToString());
        var theme = dialog.Choice("Theme", new[] { "Light", "Dark", "System" }, settings.Theme);
        if (!dialog.Accept())
            return;
        try
        {
            var updated = LocalStore.Clone(settings);
            updated.Transport = Enum.Parse<Transport>((string)transport.SelectedItem);
            updated.PrintableWidthMm = double.Parse(width.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
            updated.Dpi = int.Parse(dpi.Text);
            updated.TextMode = Enum.Parse<TextMode>((string)mode.SelectedItem);
            updated.Font = (string)font.SelectedItem;
            updated.Encoding = (string)encoding.SelectedItem;
            updated.CodePageCommand = byte.Parse(page.Text);
            updated.CodePageVerified = verified.IsChecked == true;
            updated.NativeQr = qr.IsChecked == true;
            updated.Dither = dither.IsChecked == true;
            updated.ComPort = port.Text.Trim();
            updated.BaudRate = int.Parse(baud.Text);
            updated.Theme = (string)theme.SelectedItem;
            updated.Validate();
            settingsService.Save(updated);
            settings = updated;
            syncing = true;
            RenderImage.IsChecked = settings.TextMode == TextMode.Raster;
            syncing = false;
            UpdateConnection();
            ApplyTheme();
            SchedulePreview();
        }
        catch (Exception ex) { Error(new ArgumentException("Check numeric settings. " + ex.Message)); }
    }
}
