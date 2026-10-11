using System.Windows;
using BT583Printer.Services;
namespace BT583Printer;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => { LocalStore.Log(args.Exception); MessageBox.Show("Action failed. Check the printer and local files. Details have been saved to the log.", "BT-583 Printer", MessageBoxButton.OK, MessageBoxImage.Warning); args.Handled = true; };
        base.OnStartup(e);
    }
}
