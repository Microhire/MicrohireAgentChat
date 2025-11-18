using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models.MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[ApiController]
[Route("api/booking")]

public sealed class BookingController : ControllerBase
{
    private readonly BookingDbContext _db;

    public BookingController(BookingDbContext db) => _db = db;

    [HttpGet("by-number/{bookingNo}")]
    public async Task<ActionResult<BookingResponse>> GetByBookingNo(string bookingNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo))
            return BadRequest("bookingNo is required.");

        var b = await _db.TblBookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.booking_no == bookingNo, ct);
        if (b is null) return NotFound();

        // ---- Items (from TblItemtran) ----
        // 1) Pull raw rows with group and price
        var groupedRaw = await (
            from i in _db.TblItemtrans.AsNoTracking()
            where i.BookingNoV32 == bookingNo
            join inv in _db.TblInvmas.AsNoTracking()
                 on i.ProductCodeV42 equals inv.product_code into gj
            from inv in gj.DefaultIfEmpty()
            select new
            {
                GroupFld = inv.groupFld ?? "Ungrouped",
                i.CommentDescV42,
                i.TransQty,
                Price = (decimal?)(i.Price ?? 0.0),   // TblItemtran.Price is double? -> cast to decimal?
                i.HeadingNo,
                i.SeqNo,
                i.SubSeqNo
            }
        ).ToListAsync(ct);

        // 2) Group, sort, map lines, and compute TotalPrice
        var items = groupedRaw
            .GroupBy(x => x.GroupFld)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var lines = g
                    .OrderBy(x => x.HeadingNo).ThenBy(x => x.SeqNo).ThenBy(x => x.SubSeqNo)
                    .Select(x => new BookingLineDto(
                        x.CommentDescV42.Trim(),
                        x.TransQty,
                        Money(x.Price)                     // per-line display
                    ))
                    .ToList();

                var total = g.Sum(x => x.Price ?? 0m);     // numeric sum
                return new BookingGroupDto(
                    g.Key,
                    lines,
                    Money(total)                           // formatted group total
                );
            })
            .ToList();


        // ---- Crew (from TblCrew) ----
        // First pull raw crew rows to memory, then format with helpers.
        var crewRaw = await (from c in _db.TblCrews.AsNoTracking()
                             join i in _db.TblInvmas.AsNoTracking()
                                 on c.ProductCodeV42 equals i.product_code into gj
                             from inv in gj.DefaultIfEmpty()
                             where c.BookingNoV32 == bookingNo
                             orderby c.FirstDate, c.DelTimeHour, c.DelTimeMin
                             select new
                             {
                                 c.ProductCodeV42,
                                 ProductDescription = inv.descriptionv6.Trim(), // <-- description from tblInvmas
                                 c.Task,
                                 c.TransQty,
                                 c.FirstDate,
                                 c.RetnDate,
                                 c.DelTimeHour,
                                 c.DelTimeMin,
                                 c.ReturnTimeHour,
                                 c.ReturnTimeMin,
                                 c.Hours,
                                 c.Minutes,
                                 c.Price
                             })
                            .ToListAsync(ct);


        var crew = crewRaw.Select(x =>
        {
            var start = CombineDateTime(x.FirstDate, x.DelTimeHour, x.DelTimeMin);
            var end = CombineDateTime(x.RetnDate, x.ReturnTimeHour, x.ReturnTimeMin);

            // Prefer explicit hours/minutes columns when present; otherwise compute.
            string? hm = (x.Hours is not null && x.Minutes is not null)
                ? $"{x.Hours.Value}:{x.Minutes.Value:00}"   // <- use .Value for formatting
                : DiffHColonMM(start, end);

            return new BookingCrewDto(
                x.ProductDescription.Trim(),                 // <- matches DTO
                x.Task,                          // <- no ToString()
                x.TransQty,
                FmtDateTime(start),
                FmtDateTime(end),
                hm,
                Money(x.Price)
            );
        }).ToList();


        var pdf = new BookingPdfDto(
            ShowName: b.showName,
            VenueName: !string.IsNullOrWhiteSpace(b.EventType) ? b.EventType : "The Westin Brisbane",
            VenueRoom: b.VenueRoom,
            ContactNameV6: b.contact_nameV6,
            OrganizationV6: b.OrganizationV6,
            AddressL1V6: TryGet(b, "Address_l1V6"),
            AddressL3V6: TryGet(b, "Address_l3V6"),
            Phone1: TryGet(b, "Phone1"),
            Email: TryGet(b, "Email"),
            SalespersonName: b.Salesperson ?? TryGet(b, "Salesperson_name"),
            CellCountryCodeAndCell: JoinNonEmpty(TryGet(b, "CellCountryCode"), TryGet(b, "Cell")),
            ShowDateLabel: BuildShowDateLabel(b.ShowSDate, b.ShowEdate),
            SetupTime: FormatHHmm(b.setupTimeV61),
            RehearsalTime: FormatHHmm(b.RehearsalTime),
            ShowStartTime: FormatHHmm(b.showStartTime),
            ShowEndTime: FormatHHmm(!string.IsNullOrWhiteSpace(b.StrikeTime) ? b.StrikeTime : b.ShowEndTime),
            HirePrice: Money(b.hire_price),
            Labour: Money(b.labour),
            InsuranceV5: Money(b.insurance_v5),
            Tax2: Money(b.Tax2),
            PriceQuoted: Money(b.price_quoted),
            Items: items,
            Crew: crew
        );

        return new BookingResponse(pdf);
    }
    private static DateTime? CombineDateTime(DateTime? d, int? hh, int? mm)
    {
        if (d is null || hh is null || mm is null) return null;
        return new DateTime(d.Value.Year, d.Value.Month, d.Value.Day,
                            Math.Clamp(hh.Value, 0, 23),
                            Math.Clamp(mm.Value, 0, 59), 0, DateTimeKind.Unspecified);
    }

    private static string? FmtDateTime(DateTime? dt) =>
        dt is null ? null : dt.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string? DiffHColonMM(DateTime? a, DateTime? b)
    {
        if (a is null || b is null) return null;
        var ts = b.Value - a.Value;
        if (ts.TotalMinutes < 0) return null;
        var hours = (int)ts.TotalHours;
        var mins = ts.Minutes;
        return $"{hours}:{mins:00}";
    }

    // price from double? (TblItemtran.Price) → "N2"
    private static string? Money(double? v) =>
        v is null ? null : ((decimal)v.Value).ToString("N2", CultureInfo.InvariantCulture);

    // -------- helpers (kept local to the controller) --------
    private static string? BuildShowDateLabel(DateTime? s, DateTime? e)
    {
        if (s is null && e is null) return null;
        static string F(DateTime d) => $"{d:dddd dd MMMM yyyy}".Replace(" 0", " ");
        if (s is not null && e is not null) return $"{F(s.Value)} to {F(e.Value)}";
        return s is not null ? F(s.Value) : F(e!.Value);
    }

    // "730","0730","7:30","07:30","1730" -> "07:30"/"17:30"
    private static string? FormatHHmm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 3) digits = "0" + digits;
        if (digits.Length != 4) return raw;
        var hh = Math.Clamp(int.Parse(digits[..2], CultureInfo.InvariantCulture), 0, 23);
        var mm = Math.Clamp(int.Parse(digits[2..], CultureInfo.InvariantCulture), 0, 59);
        return $"{hh:00}:{mm:00}";
    }

    // format decimal? with 2dp
    private static string? Money(decimal? v) =>
        v is null ? null : v.Value.ToString("N2", CultureInfo.InvariantCulture);

    // safe property read (works if your TblBooking currently lacks some optional columns)
    private static string? TryGet(object obj, string propName) =>
        obj.GetType().GetProperty(propName)?.GetValue(obj)?.ToString();

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var vals = parts.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return vals.Length == 0 ? null : string.Join(" ", vals);
    }
}
