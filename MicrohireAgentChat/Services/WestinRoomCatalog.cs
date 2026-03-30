// Services/WestinRoomCatalog.cs
using System.Text;
using System.Text.Json;

public record RoomLayout(string Type, int Capacity, string Image);
public record WestinRoom(int Id, string Name, string Slug, string Level, string Cover, List<RoomLayout> Layouts);

public interface IWestinRoomCatalog
{
    Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default);

    /// <summary>
    /// The seven quotable Westin Brisbane rooms from the_westin_brisbane_rooms.json (lines 2–9), in display order.
    /// </summary>
    IReadOnlyList<(string Slug, string Name)> GetVenueConfirmRoomOptions();
}

public sealed class WestinRoomCatalog : IWestinRoomCatalog
{
    public const string VenueName = "Westin Brisbane";
    public const string VenueAddress = "111 Mary Street, Brisbane City, Queensland 4000, Australia";

    private readonly string _jsonPath;
    private readonly object _parseCacheLock = new();
    private List<WestinRoom>? _cachedAllRooms;
    private DateTime _cachedFileWriteUtc;
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> _quotableRoomSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "westin-ballroom",
        "westin-ballroom-1",
        "westin-ballroom-2",
        "elevate",
        "elevate-1",
        "elevate-2",
        "thrive-boardroom"
    };

    /// <summary>Supported venue-confirm / sales-portal rooms, same order as the_westin_brisbane_rooms.json lines 2–9.</summary>
    private static readonly string[] VenueConfirmSlugsInOrder =
    {
        "westin-ballroom",
        "westin-ballroom-1",
        "westin-ballroom-2",
        "elevate",
        "elevate-1",
        "elevate-2",
        "thrive-boardroom"
    };

    public WestinRoomCatalog(IWebHostEnvironment env)
        => _jsonPath = Path.Combine(env.WebRootPath, "data", "the_westin_brisbane_rooms.json");

    public IReadOnlyList<(string Slug, string Name)> GetVenueConfirmRoomOptions()
    {
        var allRooms = GetOrParseAllRooms();
        var bySlug = allRooms
            .Where(r => !string.IsNullOrWhiteSpace(r.Slug))
            .ToDictionary(r => r.Slug, r => r, StringComparer.OrdinalIgnoreCase);
        var list = new List<(string Slug, string Name)>();
        foreach (var slug in VenueConfirmSlugsInOrder)
        {
            if (bySlug.TryGetValue(slug, out var room))
                list.Add((room.Slug, room.Name));
        }

        return list;
    }

    /// <summary>Maps session/lead room text to a venue-confirm slug (exact name, or sales-portal &quot;Thrive&quot;).</summary>
    public static string? MatchDraftRoomNameToSlug(string? draftRoom, IReadOnlyList<(string Slug, string Name)> rooms)
    {
        var r = (draftRoom ?? "").Trim();
        if (string.IsNullOrEmpty(r)) return null;
        if (string.Equals(r, "Thrive", StringComparison.OrdinalIgnoreCase))
            return "thrive-boardroom";
        foreach (var (slug, name) in rooms)
        {
            if (string.Equals(r, name, StringComparison.OrdinalIgnoreCase))
                return slug;
        }

        return null;
    }

    public async Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default)
    {
        var allRooms = GetOrParseAllRooms();
        return await Task.FromResult(allRooms
            .Where(r => !string.IsNullOrWhiteSpace(r.Slug) && _quotableRoomSlugs.Contains(r.Slug))
            .ToList());
    }

    /// <summary>
    /// Parses JSON once per process until the file changes (mtime), avoiding repeated disk I/O on hot paths.
    /// </summary>
    private List<WestinRoom> GetOrParseAllRooms()
    {
        DateTime writeUtc;
        try
        {
            writeUtc = File.GetLastWriteTimeUtc(_jsonPath);
        }
        catch
        {
            return ParseRoomsFromDisk();
        }

        lock (_parseCacheLock)
        {
            if (_cachedAllRooms != null && _cachedFileWriteUtc == writeUtc)
                return _cachedAllRooms;

            var parsed = ParseRoomsFromDisk();
            _cachedAllRooms = parsed;
            _cachedFileWriteUtc = writeUtc;
            return parsed;
        }
    }

    private List<WestinRoom> ParseRoomsFromDisk()
    {
        var bytes = File.ReadAllBytes(_jsonPath);

        // Non-throwing UTF-8 decode
        var utf8Lenient = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        var textUtf8 = utf8Lenient.GetString(bytes);

        string text;
        if (textUtf8.IndexOf('\uFFFD') >= 0) // replacement char seen => likely not valid UTF-8
        {
            // Safe 1252 fallback (never throws)
            var win1252 = Encoding.GetEncoding(
                1252,
                EncoderFallback.ReplacementFallback,
                DecoderFallback.ReplacementFallback
            );
            text = win1252.GetString(bytes);
        }
        else
        {
            text = textUtf8;
        }

        // Normalize curly quotes so name/slug comparisons succeed
        text = text
            .Replace('\u2019', '\'').Replace('\u2018', '\'')
            .Replace('\u201C', '"').Replace('\u201D', '"');

        return JsonSerializer.Deserialize<List<WestinRoom>>(text, _opts) ?? new();
    }
}
