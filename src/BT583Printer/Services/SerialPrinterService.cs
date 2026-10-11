using System.Diagnostics;
using System.IO.Ports;
namespace BT583Printer.Services;
public sealed class SerialPrinterService
{
    public void Send(string port, int baud, byte[] bytes)
    {
        using var serial = new SerialPort(port, baud, Parity.None, 8, StopBits.One) { WriteTimeout = 15000, ReadTimeout = 2000, Handshake = Handshake.None };
        serial.Open();
        for (int offset = 0; offset < bytes.Length; offset += 4096)
            serial.Write(bytes, offset, Math.Min(4096, bytes.Length - offset));
        var timeout = Stopwatch.StartNew();
        while (serial.BytesToWrite > 0)
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(30))
                throw new TimeoutException("COM transmission did not complete.");
            Thread.Sleep(20);
        }
        // Allow the UART/virtual Bluetooth driver to flush its final hardware buffer.
        Thread.Sleep(300);
    }
}
