using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProofMD;

/// <summary>A reading position: the first visible source line and its offset, with a pixel fallback.</summary>
internal readonly record struct DocumentPosition(int? SourceLine, double Offset, double ScrollY)
{
    private const double MaximumCoordinate = 100_000_000;

    [JsonIgnore]
    public bool IsValid =>
        SourceLine is null or >= 1 &&
        IsCoordinate(Offset) &&
        IsCoordinate(ScrollY) &&
        ScrollY >= 0;

    private static bool IsCoordinate(double value) =>
        double.IsFinite(value) && Math.Abs(value) <= MaximumCoordinate;
}

internal readonly record struct DocumentHistoryEntry(string Path, DocumentPosition? Position);

internal sealed record LinkOrder(string? Href, int Order);

/// <summary>Any message the viewer page posts; each type uses a subset of the fields.</summary>
internal sealed class ViewerMessage
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string? Type { get; init; }
    public string? Href { get; init; }
    public string? SourceDocument { get; init; }
    public int? Order { get; init; }
    public DocumentPosition? Position { get; init; }
    public string? Id { get; init; }
    public int? ContextId { get; init; }
    public bool? Unresolved { get; init; }
    public List<LinkOrder>? Links { get; init; }

    public DocumentPosition? ValidPosition => Position is { IsValid: true } position ? position : null;

    public static ViewerMessage? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ViewerMessage>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(object message) => JsonSerializer.Serialize(message, Options);
}
