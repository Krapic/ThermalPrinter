using System.Text.Json;
using BT583Printer.Models;
namespace BT583Printer.Services;
public static class LocalStore
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BT583Printer");
    public static JsonSerializerOptions Options { get; } = new() { WriteIndented = true };
    public static T Read<T>(string path, T fallback)
    {
        if (!File.Exists(path))
            return fallback;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback;
        }
        catch (Exception e) { Log(e); throw new IOException("Local file could not be read. It has been preserved for recovery: " + path, e); }
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
    public static void Log(Exception e)
    {
        try
        {
            var dir = Path.Combine(Root, "Logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, DateTime.Today.ToString("yyyy-MM-dd") + ".log"), DateTime.Now + " " + e + Environment.NewLine);
        }
        catch {/* Logging must never crash the UI. */}
    }
}
public sealed class SettingsService
{
    private readonly string path; public SettingsService(string? root = null)
    {
        path = Path.Combine(root ?? LocalStore.Root, "settings.json");
    }
    public PrinterSettings Load() => LocalStore.Read(path, new PrinterSettings()); public void Save(PrinterSettings s)
    {
        s.Validate();
        LocalStore.Write(path, s);
    }
}
public sealed class TemplateService
{
    private readonly string dir; public TemplateService(string? root = null)
    {
        dir = Path.Combine(root ?? LocalStore.Root, "Templates");
    }
    public string[] List()
    {
        Directory.CreateDirectory(dir);
        return Directory.GetFiles(dir, "*.json").OrderBy(p => p).ToArray();
    }
    public Document Load(string path) => LocalStore.Read(path, new Document());
    public void Save(Document d)
    {
        var name = d.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.'))
            throw new ArgumentException("Template name is invalid.");
        LocalStore.Write(Path.Combine(dir, name + ".json"), d);
    }
    public void Delete(string path) => File.Delete(path);
}
public sealed class HistoryService
{
    private readonly string path; public HistoryService(string? root = null)
    {
        path = Path.Combine(root ?? LocalStore.Root, "history.json");
    }
    public List<HistoryEntry> Load() => LocalStore.Read(path, new List<HistoryEntry>());
    public void Add(Document d, int copies)
    {
        var entries = Load();
        entries.Insert(0, new HistoryEntry { Document = LocalStore.Clone(d), Copies = copies });
        LocalStore.Write(path, entries.Take(100).ToList());
    }
}
