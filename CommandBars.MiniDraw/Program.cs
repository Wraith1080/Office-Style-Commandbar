namespace CommandBars.MiniDraw;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke"))
        {
            SmokeChecks.Run();
            return;
        }
        Application.Run(new MainForm());
    }
}
