namespace ProofMD;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(ParseMarkdownPath(args)));
    }

    private static string? ParseMarkdownPath(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0])) return null;

        try
        {
            return Path.GetFullPath(args[0]);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
