using System.Text.Json;
using MicrohireAgentChat.Data;
using System.Text.Json.Nodes;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class ItemPersistenceServiceTests
{
    [Fact]
    public async Task UpsertSelectedEquipmentAsync_SelectedPackage_InsertsParentAndComponentsWithOrdering()
    {
        await using var db = CreateDbFromAppSettings();
        var bookingNo = NewBookingNo("TPKG");
        await CreateTestBookingAsync(db, bookingNo);
        var packageCode = await PickAnyPackageCodeAsync(db);

        var service = new ItemPersistenceService(db, NullLogger<ItemPersistenceService>.Instance);
        var payload = JsonSerializer.Serialize(new[]
        {
            new SelectedEquipmentItem
            {
                ProductCode = packageCode,
                Description = packageCode,
                Quantity = 1,
                IsPackage = true
            }
        });

        try
        {
            await service.UpsertSelectedEquipmentAsync(bookingNo, payload, CancellationToken.None);

            var rows = await db.TblItemtrans
                .Where(x => x.BookingNoV32 == bookingNo)
                .OrderBy(x => x.SeqNo)
                .ThenBy(x => x.SubSeqNo)
                .ToListAsync();

            Assert.True(rows.Count >= 1);
            Assert.All(rows, r => Assert.Equal((byte)2, r.TransTypeV41.GetValueOrDefault()));
            var pkg = rows.First();
            Assert.Equal((byte)1, pkg.ItemType);
            Assert.Equal(packageCode, (pkg.ProductCodeV42 ?? "").Trim(), ignoreCase: true);
            Assert.Equal(0, pkg.SubSeqNo);

            var components = rows.Skip(1).ToList();
            Assert.All(components, r => Assert.Equal((byte)2, r.ItemType));
            Assert.All(components, r => Assert.Equal(packageCode, (r.ParentCode ?? "").Trim(), ignoreCase: true));
            Assert.All(components, r => Assert.Equal(pkg.SeqNo, r.SeqNo));
            if (components.Count > 0)
                Assert.Contains(components, c => c.SubSeqNo.GetValueOrDefault() > 0);
        }
        finally
        {
            await CleanupBookingAsync(db, bookingNo);
        }
    }

    [Fact]
    public async Task UpsertSelectedEquipmentAsync_SelectedComponentWithRate_PromotesToParentPackage()
    {
        await using var db = CreateDbFromAppSettings();
        var bookingNo = NewBookingNo("TCMP");
        await CreateTestBookingAsync(db, bookingNo);
        var (parentCode, componentCode) = await PickComponentWithPricedParentAsync(db);

        var service = new ItemPersistenceService(db, NullLogger<ItemPersistenceService>.Instance);
        var payload = JsonSerializer.Serialize(new[]
        {
            new SelectedEquipmentItem
            {
                ProductCode = componentCode,
                Description = componentCode,
                Quantity = 1
            }
        });

        try
        {
            await service.UpsertSelectedEquipmentAsync(bookingNo, payload, CancellationToken.None);

            var rows = await db.TblItemtrans
                .Where(x => x.BookingNoV32 == bookingNo)
                .ToListAsync();

            Assert.All(rows, r => Assert.Equal((byte)2, r.TransTypeV41.GetValueOrDefault()));
            Assert.Contains(rows, r => (r.ProductCodeV42 ?? "").Trim().Equals(parentCode, StringComparison.OrdinalIgnoreCase) && r.ItemType == 1);
            Assert.Contains(rows, r => (r.ProductCodeV42 ?? "").Trim().Equals(componentCode, StringComparison.OrdinalIgnoreCase) && r.ItemType == 2 && (r.ParentCode ?? "").Trim().Equals(parentCode, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rows, r => (r.ProductCodeV42 ?? "").Trim().Equals(componentCode, StringComparison.OrdinalIgnoreCase) && r.ItemType == 0);
        }
        finally
        {
            await CleanupBookingAsync(db, bookingNo);
        }
    }

    [Fact]
    public async Task UpsertSelectedEquipmentAsync_StandaloneItem_RemainsNormalItem()
    {
        await using var db = CreateDbFromAppSettings();
        var bookingNo = NewBookingNo("TSTD");
        await CreateTestBookingAsync(db, bookingNo);
        var standaloneCode = await PickStandaloneCodeAsync(db);

        var service = new ItemPersistenceService(db, NullLogger<ItemPersistenceService>.Instance);
        var payload = JsonSerializer.Serialize(new[]
        {
            new SelectedEquipmentItem
            {
                ProductCode = standaloneCode,
                Description = standaloneCode,
                Quantity = 2
            }
        });

        try
        {
            await service.UpsertSelectedEquipmentAsync(bookingNo, payload, CancellationToken.None);

            var rows = await db.TblItemtrans
                .Where(x => x.BookingNoV32 == bookingNo)
                .ToListAsync();

            var row = Assert.Single(rows);
            Assert.Equal((byte)2, row.TransTypeV41.GetValueOrDefault());
            Assert.Equal((byte)0, row.ItemType);
            Assert.Equal(standaloneCode, (row.ProductCodeV42 ?? "").Trim(), ignoreCase: true);
            Assert.Null(row.ParentCode);
        }
        finally
        {
            await CleanupBookingAsync(db, bookingNo);
        }
    }

    private static BookingDbContext CreateDbFromAppSettings()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MicrohireAgentChat", "appsettings.json");
        var json = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject();
        var cs = json?["ConnectionStrings"]?["BookingsDb"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(cs), "BookingsDb connection string was not found in appsettings.json");

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlServer(cs)
            .Options;
        return new BookingDbContext(options);
    }

    private static async Task CreateTestBookingAsync(BookingDbContext db, string bookingNo)
    {
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO tblbookings
(
    booking_no, booking_type_v32, status,
    dDate, rDate, SDate, order_date, EntryDate,
    ProjectManager, dtExpected_ReturnDate, vcExpected_ReturnTime, vcTruckOutTime, vcTruckInTime,
    CustID, VenueID, LateChargesApplied, shortagesAreTransfered, DeprepOn, DeliveryDateOn, PickupDateOn,
    TaxabPCT, UntaxPCT, Tax1PCT, Tax2PCT, crew_cnt, rTargetMargin, rProfitMargin, SyncType, HasQT, HasDAT
)
VALUES
(
    {bookingNo}, 0, 0,
    {now}, {now.AddDays(1)}, {now}, {now}, {now},
    {""}, {now}, {""}, {""}, {""},
    {1m}, {0}, {false}, {false}, {false}, {false}, {false},
    {0d}, {0d}, {0d}, {0d}, {0}, {0d}, {0d}, {0}, {false}, {false}
)");
    }

    private static async Task CleanupBookingAsync(BookingDbContext db, string bookingNo)
    {
        var items = await db.TblItemtrans.Where(i => i.BookingNoV32 == bookingNo).ToListAsync();
        if (items.Count > 0)
            db.TblItemtrans.RemoveRange(items);

        var booking = await db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo);
        if (booking != null)
            db.TblBookings.Remove(booking);

        await db.SaveChangesAsync();
    }

    private static async Task<string> PickStandaloneCodeAsync(BookingDbContext db)
    {
        var parentCodes = await db.VwProdsComponents
            .Select(v => v.ParentCode.Trim())
            .Distinct()
            .ToListAsync();
        var componentCodes = await db.VwProdsComponents
            .Select(v => v.ProductCode.Trim())
            .Distinct()
            .ToListAsync();

        var blacklist = new HashSet<string>(parentCodes.Concat(componentCodes), StringComparer.OrdinalIgnoreCase);
        var rateCodes = await db.TblRatetbls
            .Where(r => r.TableNo == 0 && r.rate_1st_day != null && r.rate_1st_day > 0 && r.product_code != null)
            .Select(r => r.product_code!.Trim())
            .Distinct()
            .ToListAsync();

        var standalone = rateCodes.FirstOrDefault(code => !blacklist.Contains(code));
        Assert.False(string.IsNullOrWhiteSpace(standalone), "Could not find a standalone product code in live DB.");
        return standalone!;
    }

    private static string NewBookingNo(string prefix)
    {
        // Keep under varchar length used by RP booking_no.
        return $"{prefix}{Guid.NewGuid():N}"[..18].ToUpperInvariant();
    }

    private static async Task<string> PickAnyPackageCodeAsync(BookingDbContext db)
    {
        var packageCode = await db.VwProdsComponents
            .Where(v => (v.VariablePart == null || v.VariablePart == 0) && v.ParentCode != null)
            .Select(v => v.ParentCode.Trim())
            .Distinct()
            .FirstOrDefaultAsync();

        Assert.False(string.IsNullOrWhiteSpace(packageCode), "Could not find any package code from vwProdsComponents.");
        return packageCode!;
    }

    private static async Task<(string ParentCode, string ComponentCode)> PickComponentWithPricedParentAsync(BookingDbContext db)
    {
        var parentRates = await db.TblRatetbls
            .Where(r => r.TableNo == 0 && r.product_code != null && r.rate_1st_day != null && r.rate_1st_day > 0)
            .Select(r => r.product_code!.Trim())
            .Distinct()
            .ToListAsync();

        var parentSet = new HashSet<string>(parentRates, StringComparer.OrdinalIgnoreCase);
        var pairs = await db.VwProdsComponents
            .Where(v => v.ParentCode != null && v.ProductCode != null)
            .Select(v => new { ParentCode = v.ParentCode.Trim(), ComponentCode = v.ProductCode.Trim() })
            .ToListAsync();

        var componentRates = await db.TblRatetbls
            .Where(r => r.TableNo == 0 && r.product_code != null && r.rate_1st_day != null && r.rate_1st_day > 0)
            .Select(r => r.product_code!.Trim())
            .Distinct()
            .ToListAsync();
        var componentRateSet = new HashSet<string>(componentRates, StringComparer.OrdinalIgnoreCase);

        var best = pairs.FirstOrDefault(p => parentSet.Contains(p.ParentCode) && componentRateSet.Contains(p.ComponentCode));
        if (best != null)
            return (best.ParentCode, best.ComponentCode);

        var fallback = pairs.FirstOrDefault(p => parentSet.Contains(p.ParentCode));
        Assert.True(fallback != null, "Could not find a package/component pair in live DB.");
        return (fallback!.ParentCode, fallback.ComponentCode);
    }
}
