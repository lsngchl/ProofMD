using System.Text;

namespace ProofMD;

internal static class UnresolvedStateStore
{
    internal const string SidecarExtension = ".unresolved";
    private const string MarkerContents = "status: unresolved\n";

    public static string SidecarPath(string markdownPath)
    {
        return Path.ChangeExtension(Path.GetFullPath(markdownPath), SidecarExtension);
    }

    public static bool IsUnresolved(string markdownPath)
    {
        return File.Exists(SidecarPath(markdownPath));
    }

    public static void SetUnresolved(string markdownPath, bool unresolved)
    {
        string sidecarPath = SidecarPath(markdownPath);
        if (!unresolved)
        {
            File.Delete(sidecarPath);
            return;
        }

        if (File.Exists(sidecarPath)) return;

        string temporaryPath = $"{sidecarPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, MarkerContents, new UTF8Encoding(false));
            try
            {
                File.Move(temporaryPath, sidecarPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(sidecarPath))
            {
                // Another viewer or tool created the same marker first.
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // A later toggle can clean up an abandoned temporary marker.
            }
        }
    }
}
