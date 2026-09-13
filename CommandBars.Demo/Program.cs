using System;
using System.Windows.Forms;

namespace CommandBars.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(Array.Exists(args, arg => arg == "--mdi" || arg == "--mdi-smoke")
            ? new MdiDemoForm(Array.Exists(args, arg => arg == "--mdi-smoke"))
            : new MainForm());
    }
}
