using System.Text.Json;

public static class IslaBlocks
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed record RoomCard(int Id, string Name, string Slug, string Cover, string? Level, int? MaxCap);
    public sealed record LayoutDto(string Type, int Capacity, string Image);
    public sealed record RoomImagesDto(string Room, string Cover, List<LayoutDto> Layouts);

    private static string Norm(string s) => (s ?? string.Empty)
        .Replace('\u2019', '\'').Replace('\u2018', '\'')
        .Replace('\u201C', '"').Replace('\u201D', '"')
        .Trim();

    private static string GalleryBlock(object payload)
        => $"[[ISLA_GALLERY]]{JsonSerializer.Serialize(payload, _json)}[[/ISLA_GALLERY]]";

    private static string Absolute(string? path, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        path = path.Replace("\\", "/").Trim();

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return path;

        if (path.StartsWith("~/", StringComparison.Ordinal)) path = path.Substring(1);
        if (!string.IsNullOrWhiteSpace(baseUrl))
            return $"{baseUrl!.TrimEnd('/')}/{path.TrimStart('/')}";

        return path.StartsWith("/") ? path : "/" + path;
    }

    public static string BuildRoomsGalleryBlock(
        IEnumerable<RoomCard> rooms,
        string? baseUrl = null,
        int? max = null,
        string headerRoomList = "Choose a room")
    {
        var items = rooms
            .Where(r => !string.IsNullOrWhiteSpace(r.Cover))
            .Select(r => new
            {
                kind = "room",
                label = (r.MaxCap is int c && c > 0) ? $"{Norm(r.Name)} (up to {c})" : Norm(r.Name),
                url = Absolute(r.Cover, baseUrl),
                select = new { room = string.IsNullOrWhiteSpace(r.Slug) ? Norm(r.Name) : Norm(r.Slug) }
            })
            .ToList();

        if (max is int m && m > 0) items = items.Take(m).ToList();

        var payload = new { room = headerRoomList, items };
        return GalleryBlock(payload);
    }

    public static string BuildLayoutsGalleryBlock(
        RoomImagesDto data,
        string? baseUrl = null,
        int? max = null,
        bool includeCover = true)
    {
        var items = new List<object>();

        if (includeCover && !string.IsNullOrWhiteSpace(data.Cover))
        {
            items.Add(new
            {
                kind = "cover",
                label = "Room view",
                url = Absolute(data.Cover, baseUrl),
                select = new { }
            });
        }

        foreach (var l in data.Layouts ?? new())
        {
            var label = l.Capacity > 0 ? $"{l.Type} ({l.Capacity})" : l.Type;
            items.Add(new
            {
                kind = "layout",
                label,
                url = Absolute(l.Image, baseUrl),
                select = new { layout = l.Type }
            });
        }

        if (max is int m && m > 0) items = items.Take(m).ToList();

        var payload = new { room = Norm(data.Room), items };
        return GalleryBlock(payload);
    }

    // ---- Single, correct time-picker builder ----
    public sealed record TimePickerConfig(
        string Title,
        string? DateIso = null,
        string? DefaultStart = "09:00",
        string? DefaultEnd = "10:00",
        int StepMinutes = 30
    );

    public static string BuildTimePickerBlock(TimePickerConfig cfg)
    {
        var payload = new
        {
            ui = new
            {
                type = "timepicker",
                title = cfg.Title,
                date = cfg.DateIso,
                defaultStart = cfg.DefaultStart,
                defaultEnd = cfg.DefaultEnd,
                stepMinutes = cfg.StepMinutes
            }
        };
        return JsonSerializer.Serialize(payload, _json);
    }

    // ---- Equipment picker/gallery builder ----
    public sealed record EquipmentItem(
        string ProductCode,
        string Description,
        string? Category,
        string? ImageUrl
    );

    public static string BuildEquipmentGalleryBlock(
        IEnumerable<EquipmentItem> items,
        string title = "Choose equipment",
        string? baseUrl = null,
        int? max = null)
    {
        var galleryItems = items
            .Where(e => !string.IsNullOrWhiteSpace(e.Description))
            .Select(e => new
            {
                kind = "equipment",
                label = Norm(e.Description),
                url = string.IsNullOrWhiteSpace(e.ImageUrl) ? "" : Absolute(e.ImageUrl, baseUrl),
                select = new { product_code = e.ProductCode, description = e.Description }
            })
            .ToList();

        if (max is int m && m > 0) galleryItems = galleryItems.Take(m).ToList();

        var payload = new { room = title, items = galleryItems };
        return GalleryBlock(payload);
    }
}
