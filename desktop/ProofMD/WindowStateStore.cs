using System.Text.Json;

namespace ProofMD;

internal sealed class WindowStateData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
}

internal static class WindowStateStore
{
    private static readonly string SettingsDirectory = UserProfile.DirectoryPath;

    private static readonly string SettingsPath = Path.Combine(
        SettingsDirectory,
        "window-state.json");

    public static WindowStateData? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<WindowStateData>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(Rectangle bounds, bool maximized)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var state = new WindowStateData
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = maximized,
            };

            string json = JsonSerializer.Serialize(state);
            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch
        {
            // Window persistence is optional and must never prevent shutdown.
        }
    }
}
