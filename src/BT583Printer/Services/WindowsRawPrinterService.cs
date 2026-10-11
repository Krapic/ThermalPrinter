using System.ComponentModel;
using System.Runtime.InteropServices;
namespace BT583Printer.Services;
public sealed class WindowsRawPrinterService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        public string Name; public string? Output; public string Type;
    }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint StartDocPrinter(IntPtr h, uint level, ref DocInfo info);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr h, byte[] data, int count, out int written);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr h);
    [DllImport("winspool.drv")] private static extern bool ClosePrinter(IntPtr h);
    [DllImport("winspool.drv")] private static extern bool AbortPrinter(IntPtr h);
    public void Send(string name, byte[] data)
    {
        if (!OpenPrinter(name, out var h, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        bool started = false, completed = false;
        try
        {
            var info = new DocInfo { Name = "BT-583 document", Type = "RAW" };
            if (StartDocPrinter(h, 1, ref info) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            started = true;
            if (!StartPagePrinter(h))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!WritePrinter(h, data, data.Length, out var written) || written != data.Length)
                throw new IOException("Incomplete printer write");
            if (!EndPagePrinter(h) || !EndDocPrinter(h))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            completed = true;
        }
        finally { if (started && !completed) AbortPrinter(h); ClosePrinter(h); }
    }
}
