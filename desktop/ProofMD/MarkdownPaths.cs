namespace ProofMD;

internal static class MarkdownPaths
{
    public static bool IsMarkdown(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreEqual(string? first, string? second)
    {
        return first is not null && second is not null &&
            first.Equals(second, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWithin(string directory, string path)
    {
        string relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) &&
            relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves a relative Markdown link written in <paramref name="sourcePath"/> to the
    /// on-disk path of its target, or returns null for anything else.
    /// </summary>
    public static string? ResolveLink(string sourcePath, string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;

        try
        {
            int suffixStart = href.IndexOfAny(['?', '#']);
            string relativePath = Uri.UnescapeDataString(suffixStart >= 0 ? href[..suffixStart] : href)
                .Replace('/', Path.DirectorySeparatorChar);
            string? sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                !IsMarkdown(relativePath) ||
                sourceDirectory is null)
            {
                return null;
            }

            return Canonicalize(Path.GetFullPath(Path.Combine(sourceDirectory, relativePath)));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns <paramref name="fullPath"/> with each existing component in its on-disk casing,
    /// so that one document always has one identity however a link spells it.
    /// </summary>
    public static string Canonicalize(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root)) return fullPath;

        string current = root;
        foreach (string component in fullPath[root.Length..].Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            string? match = null;
            try
            {
                match = Directory.EnumerateFileSystemEntries(current, component).FirstOrDefault();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Keep the remaining components as written when a folder cannot be listed.
            }

            current = match ?? Path.Combine(current, component);
        }

        return current;
    }
}
