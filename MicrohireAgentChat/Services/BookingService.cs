using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Models.MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MicrohireAgentChat.Services
{
    public sealed class BookingService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly BookingDbContext _db;
        public BookingService(BookingDbContext db) { _db = db; }

        // Single booking by number
        public async Task<BookingModel?> GetByBookingNoAsync(string bookingNo, CancellationToken ct = default)
        {
            var list = await GetListAsync(search: bookingNo, exactBookingNo: true, take: 1, ct);
            return list.FirstOrDefault();
        }

        // List bookings (everything converted to string, never null)
        public async Task<List<BookingModel>> GetListAsync(
            string? search = null,
            bool exactBookingNo = false,
            int take = 100,
            CancellationToken ct = default)
        {
            if (take <= 0) take = 100;
            var s = (search ?? string.Empty).Trim();

            // base query
            var baseQ = _db.TblBookings.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(s))
            {
                if (exactBookingNo)
                {
                    baseQ = baseQ.Where(b => b.booking_no != null && b.booking_no.Trim() == s);
                }
                else
                {
                    baseQ = baseQ.Where(b =>
                        (b.booking_no ?? "").Contains(s) ||
                        (b.showName ?? "").Contains(s));
                }
            }

            // Select RAW values (no CultureInfo/ToString in SQL)
            var rows = await baseQ
                .OrderByDescending(b => b.ShowSDate ?? b.SDate ?? b.order_date)
                .Take(take)
                .Select(b => new
                {
                    // TblBooking raw fields
                    b.showName,
                    b.contact_nameV6,
                    b.OrganizationV6,
                    Address1 = b.delivery_address_exist,
                    Address3 = b.same_address,
                    b.VenueRoom,
                    VenueContact = b.VenueRoom,
                    Salesperson = b.Salesperson,
                    b.setupTimeV61,
                    b.RehearsalTime,
                    b.showStartTime,
                    ShowEndTime = b.ShowEndTime,
                    b.ShowSDate,
                    b.ShowEdate,
                    b.price_quoted,
                    b.hire_price,
                    b.labour,
                    b.insurance_v5,

                    // First TblItemtran (raw)
                    FirstDesc = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.ProductCodeV42 ?? i.CommentDescV42)
                        .FirstOrDefault(),

                    FirstComment = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.CommentDescV42)
                        .FirstOrDefault(),

                    FirstQty = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.TransQty)
                        .FirstOrDefault(),

                    FirstStart = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.FirstDate)
                        .FirstOrDefault(),

                    FirstEnd = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.RetnDate)
                        .FirstOrDefault(),

                    FirstTotal = _db.TblItemtrans
                        .Where(i => i.BookingNoV32 == b.booking_no)
                        .OrderBy(i => i.ID)
                        .Select(i => i.BeforeDiscountAmount)
                        .FirstOrDefault(),

                    // Latest TblCrew (raw)
                    LastTask = _db.TblCrews
                        .Where(c => c.BookingNoV32 == b.booking_no)
                        .OrderByDescending(c => c.ID)
                        .Select(c => c.Task)
                        .FirstOrDefault(),

                    LastHours = _db.TblCrews
                        .Where(c => c.BookingNoV32 == b.booking_no)
                        .OrderByDescending(c => c.ID)
                        .Select(c => c.Hours)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            // Helpers: convert to non-null strings
            static string S(string? v) => v ?? "";
            static string SD(DateTime? v) => v.HasValue ? v.Value.ToString("s", Inv) : "";
            static string SN(decimal? v) => v.HasValue ? v.Value.ToString(Inv) : "";
            static string SO(object? v) => v is null ? "" : Convert.ToString(v, Inv) ?? "";

            // Map to string-only model (no nulls)
            var result = rows.Select(r => new BookingModel
            {
                // TblBooking -> strings
                showName = S(r.showName),
                contact_nameV6 = S(r.contact_nameV6),
                OrganizationV6 = S(r.OrganizationV6),
                Address_l1V6 = S(r.Address1),
                Address_l3V6 = S(r.Address3),
                VenueRoom = S(r.VenueRoom),
                VenueContact = S(r.VenueContact),
                Salesperson_name = S(r.Salesperson),
                setupTimeV61 = S(r.setupTimeV61),
                RehearsalTime = S(r.RehearsalTime),
                showStartTime = S(r.showStartTime),
                showEndTime = S(r.ShowEndTime),

                ShowSDate = SD(r.ShowSDate),
                ShowEDate = SD(r.ShowEdate),
                price_quoted = SN((decimal?)r.price_quoted),
                hire_price = SN((decimal?)r.hire_price),
                labour = SN((decimal?)r.labour),
                insurance_v5 = SN((decimal?)r.insurance_v5),
                Tax2 = "",

                //// First TblItemtran -> strings
                //Description = S(r.FirstDesc),
                //Comment_desc_v42 = S(r.FirstComment),
                //Quantity = SN(r.FirstQty),
                //StartDateTime = SD(r.FirstStart),
                //EndDateTime = SD(r.FirstEnd),
                //Total = SN((decimal?)r.FirstTotal),

                //// Latest TblCrew -> strings
                //Task = SO(r.LastTask),
                //Hours = SO(r.LastHours),

                // Fields present in BookingModel but not sourced here -> empty strings
                Phone1 = "",
                Email = "",
                CellCountryCode = ""
            })
            .ToList();

            return result;
        }
    }
}
