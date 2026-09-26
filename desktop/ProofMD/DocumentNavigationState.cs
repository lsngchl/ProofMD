using System.Text.Json;

namespace ProofMD;

internal readonly record struct DocumentPosition(
    int? SourceLine,
    double Offset,
    double ScrollY)
{
    private const double MaximumCoordinate = 100_000_000;

    public static bool TryRead(
        JsonElement message,
        string propertyName,
        out DocumentPosition position)
    {
        position = default;
        if (!message.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("scrollY", out JsonElement scrollYElement) ||
            !scrollYElement.TryGetDouble(out double scrollY) ||
            !IsValidCoordinate(scrollY) ||
            scrollY < 0)
        {
            return false;
        }

        int? sourceLine = null;
        if (element.TryGetProperty("sourceLine", out JsonElement sourceLineElement) &&
            sourceLineElement.ValueKind != JsonValueKind.Null)
        {
            if (!sourceLineElement.TryGetInt32(out int parsedSourceLine) ||
                parsedSourceLine < 1)
            {
                return false;
            }
            sourceLine = parsedSourceLine;
        }

        double offset = 0;
        if (element.TryGetProperty("offset", out JsonElement offsetElement) &&
            (!offsetElement.TryGetDouble(out offset) || !IsValidCoordinate(offset)))
        {
            return false;
        }

        position = new DocumentPosition(sourceLine, offset, scrollY);
        return true;
    }

    private static bool IsValidCoordinate(double value)
    {
        return double.IsFinite(value) && Math.Abs(value) <= MaximumCoordinate;
    }
}

internal readonly record struct DocumentHistoryEntry(
    string Path,
    DocumentPosition? Position);
