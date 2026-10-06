using System.Runtime.InteropServices;

namespace CrashFeel;

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--selftest")
        {
            // Plan bounds only: no window, no DirectInput, no force.
            AttachConsole(-1);
            string? error = Candidates.SelfTest();
            Console.WriteLine(error == null ? "selftest passed: " + Candidates.All.Length + " candidates" : "selftest FAILED: " + error);
            return error == null ? 0 : 1;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
