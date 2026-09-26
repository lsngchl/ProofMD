namespace ProofMD;

internal static class UserProfile
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProofMD");

    public static bool NeedsWebViewMigration(string localAppData)
    {
        return !Directory.Exists(Path.Combine(localAppData, "ProofMD", "WebView2")) &&
            Directory.Exists(Path.Combine(localAppData, "LeanMD", "WebView2"));
    }

    public static void Migrate(string localAppData)
    {
        string currentDirectory = Path.Combine(localAppData, "ProofMD");
        string legacyDirectory = Path.Combine(localAppData, "LeanMD");
        string currentWindowState = Path.Combine(currentDirectory, "window-state.json");
        string legacyWindowState = Path.Combine(legacyDirectory, "window-state.json");

        if (!File.Exists(currentWindowState) && File.Exists(legacyWindowState))
        {
            Directory.CreateDirectory(currentDirectory);
            try
            {
                File.Copy(legacyWindowState, currentWindowState, overwrite: false);
            }
            catch (IOException) when (File.Exists(currentWindowState))
            {
                // Another ProofMD window completed the same migration.
            }
        }

        if (!NeedsWebViewMigration(localAppData)) return;

        Directory.CreateDirectory(currentDirectory);
        try
        {
            Directory.Move(
                Path.Combine(legacyDirectory, "WebView2"),
                Path.Combine(currentDirectory, "WebView2"));
        }
        catch (IOException) when (!NeedsWebViewMigration(localAppData))
        {
            // Another ProofMD window completed the same migration.
        }
    }
}
