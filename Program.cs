namespace Bot;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}