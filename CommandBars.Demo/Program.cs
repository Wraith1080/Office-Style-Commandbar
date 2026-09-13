using System;
using System.Windows.Forms;

namespace CommandBars.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (Array.Exists(args, arg => arg == "--mdi-native"))
        {
            Application.Run(new NativeMdiCheckForm());
            return;
        }
        Application.Run(Array.Exists(args, arg => arg == "--mdi" || arg == "--mdi-smoke")
            ? new MdiDemoForm(Array.Exists(args, arg => arg == "--mdi-smoke"))
            : new MainForm());
    }
}
