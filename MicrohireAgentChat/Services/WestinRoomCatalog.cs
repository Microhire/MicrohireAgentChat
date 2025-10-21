// Services/WestinRoomCatalog.cs
using System.Text;
using System.Text.Json;

public record RoomLayout(string Type, int Capacity, string Image);
public record WestinRoom(int Id, string Name, string Slug, string Level, string Cover, List<RoomLayout> Layouts);

public interface IWestinRoomCatalog
{
    Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default);
}

public sealed class WestinRoomCatalog : IWestinRoomCatalog
{
    private readonly string _jsonPath;
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public WestinRoomCatalog(IWebHostEnvironment env)
        => _jsonPath = Path.Combine(env.WebRootPath, "data", "the_westin_brisbane_rooms.json");

    public async Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(_jsonPath, ct);

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
