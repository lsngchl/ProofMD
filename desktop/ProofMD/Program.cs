using System.Diagnostics;

namespace ProofMD;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (UserProfile.NeedsWebViewMigration(localAppData))
            {
                Process[] legacyProcesses = Process.GetProcessesByName("LeanMD");
                bool legacyAppRunning = legacyProcesses.Length > 0;
                foreach (Process process in legacyProcesses) process.Dispose();
                if (legacyAppRunning)
                {
                    throw new IOException("Close LeanMD, then reopen ProofMD to transfer your settings.");
                }
            }
            UserProfile.Migrate(localAppData);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Your LeanMD settings could not be transferred. Close LeanMD and try again.\n\n{exception.Message}",
                "ProofMD",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string? markdownPath = args.FirstOrDefault(argument =>
            !string.IsNullOrWhiteSpace(argument) && !argument.StartsWith("--", StringComparison.Ordinal));

        if (markdownPath is not null)
        {
            try
            {
                markdownPath = Path.GetFullPath(markdownPath.Trim('"'));
            }
            catch
            {
                markdownPath = null;
            }
        }

        Application.Run(new MainForm(markdownPath));
    }
}
