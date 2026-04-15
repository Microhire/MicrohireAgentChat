using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

public sealed partial class BookingPersistenceService
{
    /// <summary>
    /// Saves the full conversation transcript to tblbooknote for a booking.
    /// Persists to both:
    /// - legacy transcript bucket (NoteType=1), and
    /// - Rental Point visible notes bucket (NoteType=0 with line_no set).
    /// </summary>
    public async Task SaveFullTranscriptToBooknoteAsync(
        string bookingNo,
        IEnumerable<DisplayMessage> messages,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo) || messages == null)
            return;

        var transcript = BuildTranscriptForBooknote(messages);
        if (string.IsNullOrWhiteSpace(transcript))
            return;

        await UpsertLegacyTranscriptNoteAsync(bookingNo, transcript, ct);
        await UpsertVisibleTranscriptNoteAsync(bookingNo, transcript, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertLegacyTranscriptNoteAsync(
        string bookingNo,
        string transcript,
        CancellationToken ct)
    {
        var existing = await _db.TblBooknotes
            .FirstOrDefaultAsync(b =>
                b.BookingNo == bookingNo &&
                b.NoteType == 1,
                ct);

        if (existing == null)
        {
            _db.TblBooknotes.Add(new TblBooknote
            {
                BookingNo = bookingNo,
                NoteType = 1,
                TextLine = transcript,
                OperatorId = _rpDefaults.OperatorId
            });
            return;
        }

        existing.TextLine = transcript;
        if (existing.OperatorId == null)
            existing.OperatorId = _rpDefaults.OperatorId;
    }

    private async Task UpsertVisibleTranscriptNoteAsync(
        string bookingNo,
        string transcript,
        CancellationToken ct)
    {
        var existingVisible = await _db.TblBooknotes
            .Where(b =>
                b.BookingNo == bookingNo &&
                b.NoteType == 0 &&
                b.OperatorId == _rpDefaults.OperatorId &&
                b.TextLine != null &&
                (b.TextLine.StartsWith("Agent:") || b.TextLine.StartsWith("User:")))
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (existingVisible != null)
        {
            existingVisible.TextLine = transcript;
            if (existingVisible.LineNo == null)
                existingVisible.LineNo = await GetNextVisibleNoteLineNoAsync(bookingNo, ct);
            return;
        }

        _db.TblBooknotes.Add(new TblBooknote
        {
            BookingNo = bookingNo,
            NoteType = 0,
            LineNo = await GetNextVisibleNoteLineNoAsync(bookingNo, ct),
            TextLine = transcript,
            OperatorId = _rpDefaults.OperatorId
        });
    }

    private async Task<byte> GetNextVisibleNoteLineNoAsync(string bookingNo, CancellationToken ct)
    {
        var maxLineNo = await _db.TblBooknotes
            .Where(b => b.BookingNo == bookingNo && b.NoteType == 0 && b.LineNo != null)
            .MaxAsync(b => b.LineNo, ct);

        if (!maxLineNo.HasValue)
            return 0;

        return maxLineNo.Value >= byte.MaxValue
            ? byte.MaxValue
            : (byte)(maxLineNo.Value + 1);
    }

    private static string BuildTranscriptForBooknote(IEnumerable<DisplayMessage> messages)
    {
        var sb = new StringBuilder();

        foreach (var m in messages)
        {
            var role = (m.Role ?? "").Trim().ToLowerInvariant();
            string label = role switch
            {
                "assistant" => "Agent",
                "user" => "User",
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(role)
            };

            var body = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
            if (string.IsNullOrWhiteSpace(body))
                continue;

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.Append(label);
            sb.Append(": ");
            sb.Append(body);
        }

        return sb.ToString();
    }

    public async Task<string> GenerateNextBookingNoAsync(string customerCode, CancellationToken ct)
    {
        var prefix = (customerCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            throw new InvalidOperationException("Customer code is required to generate booking numbers.");
        const int suffixWidth = 5;

        var existing = await _db.TblBookings
            .AsNoTracking()
            .Where(b => b.booking_no != null && b.booking_no.StartsWith(prefix))
            .Select(b => b.booking_no!)
            .ToListAsync(ct);

        int maxSeq = 0;
        foreach (var bk in existing)
        {
            var suffix = bk.Length > prefix.Length ? bk.Substring(prefix.Length) : "";
            var digits = new string(suffix.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var n))
                if (n > maxSeq) maxSeq = n;
        }

        var nextSeq = maxSeq + 1;
        return $"{prefix}{nextSeq.ToString().PadLeft(suffixWidth, '0')}";
    }

    /// <summary>
    /// Creates a booking on-the-fly when generate_quote is called but no booking exists.
    /// Uses session data for times/equipment and defaults for other fields.
    /// Accepts optional messages for date extraction fallback when session has no event date.
    /// </summary>
    public async Task<string> CreateBookingOnTheFlyAsync(
        ISession? session,
        IEnumerable<DisplayMessage>? fallbackMessages,
        CancellationToken ct)
    {
        if (session == null) 
        {
            _logger.LogError("[QUOTE GEN] CreateBookingOnTheFlyAsync failed - session is null");
            return "";
        }
        
        try
        {
            var startTime = session.GetString("Draft:StartTime");
            var endTime = session.GetString("Draft:EndTime");
            var setupTime = session.GetString("Draft:SetupTime");
            var eventDateStr = session.GetString("Draft:EventDate");
            var equipmentJson = session.GetString("Draft:SelectedEquipment");
            
            _logger.LogInformation("[QUOTE GEN] CreateBookingOnTheFlyAsync starting. Session data: " +
                "StartTime={StartTime}, EndTime={EndTime}, SetupTime={SetupTime}, EventDate={EventDate}, " +
                "HasEquipment={HasEquipment}",
                startTime ?? "(null)", endTime ?? "(null)", setupTime ?? "(null)", eventDateStr ?? "(null)",
                !string.IsNullOrEmpty(equipmentJson));
            
            var bookingNo = string.Empty;

            var contactName = session.GetString("Draft:ContactName");
            var contactEmail = session.GetString("Draft:ContactEmail");
            var contactPhone = session.GetString("Draft:ContactPhone");
            var organisation = session.GetString("Draft:Organisation");
            var organisationAddress = session.GetString("Draft:OrganisationAddress");

            decimal? contactId = null;
            decimal? orgId = null;
            string? custCode = null;
            string? orgName = null;

            var res = await _contactResolution.ResolveAsync(
                contactName,
                contactEmail,
                contactPhone,
                contactPosition: null,
                organisation,
                organisationAddress,
                ct,
                leadAuthoritative: false);

            contactId = res.contactId;
            orgId = res.orgId;
            custCode = res.customerCode;
            orgName = organisation;

            if (orgId.HasValue)
            {
                var authoritativeCode = await _orgService.GetCustomerCodeByIdAsync(orgId.Value, ct);
                if (!string.IsNullOrWhiteSpace(authoritativeCode))
                    custCode = authoritativeCode;
            }
            else
            {
                orgId = await GetDefaultCustomerIdAsync(ct);
                if (orgId.HasValue)
                {
                    var defaultCustomerCode = await _orgService.GetCustomerCodeByIdAsync(orgId.Value, ct);
                    if (!string.IsNullOrWhiteSpace(defaultCustomerCode))
                        custCode = defaultCustomerCode;
                }
            }

            var bookingCustomerCode = (custCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bookingCustomerCode))
            {
                _logger.LogError(
                    "[QUOTE GEN] Unable to generate booking number because customer code is missing. OrgId={OrgId}, ContactId={ContactId}",
                    orgId,
                    contactId);
                return "";
            }

            if (!orgId.HasValue)
            {
                _logger.LogError(
                    "[QUOTE GEN] CustID could not be resolved. CustCode={CustCode}, ContactId={ContactId}",
                    bookingCustomerCode, contactId);
                return "";
            }

            bookingNo = await GenerateNextBookingNoAsync(bookingCustomerCode, ct);
            _logger.LogInformation(
                "[QUOTE GEN] Creating booking {BookingNo} on-the-fly for customer code {CustomerCode}, OrgId={OrgId}",
                bookingNo,
                bookingCustomerCode,
                orgId);

            if (!bookingNo.StartsWith(bookingCustomerCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "[QUOTE GEN] MISMATCH: Booking number {BookingNo} does not start with customer code {CustCode}. OrgId={OrgId}",
                    bookingNo, bookingCustomerCode, orgId);
            }

            var attendeesStr = session.GetString("Draft:ExpectedAttendees") ?? session.GetString("Ack:Attendees");
            int? sessionAttendees = null;
            if (!string.IsNullOrWhiteSpace(attendeesStr) && int.TryParse(attendeesStr.Trim(), out var parsedAttendees) && parsedAttendees > 0)
                sessionAttendees = parsedAttendees;

            var booking = new TblBooking
            {
                booking_no = bookingNo,
                EntryDate = DateTime.Now,
                booking_type_v32 = 2,
                status = 0, // must be non-null or RentalPoint's Status tab shows "Quote" regardless of BookingProgressStatus
                BookingProgressStatus = 1,
                From_locn = 20,
                return_to_locn = 20,
                Trans_to_locn = 20,
                CustCode = Trunc(bookingCustomerCode, CustomerCodeMaxLength),
                CustID = orgId.Value,
                ContactID = contactId,
                contact_nameV6 = Trunc(contactName ?? "Contact", 35),
                expAttendees = sessionAttendees,
                OrganizationV6 = Trunc(orgName ?? organisation, 50),
                Salesperson = Trunc(_rpDefaults.Salesperson, SalespersonMaxLength),
                VenueID = await ResolveVenueIdAsync(session.GetString("Draft:VenueName"), ct) ?? 20,
                VenueRoom = Trunc(MergeProjectorAreasIntoVenueRoom(
                    session.GetString("Draft:RoomName") ?? session.GetString("Draft:VenueRoom") ?? "Conference Room",
                    session.GetString("Draft:ProjectorAreas"),
                    session.GetString("Draft:ProjectorArea"),
                    SessionRequiresProjectorPlacement(session)), VenueRoomMaxLength)
            };
            
            var rehearsalTime = session.GetString("Draft:RehearsalTime");
            
            DateTime eventDate;
            if (!string.IsNullOrEmpty(eventDateStr) && DateTime.TryParse(eventDateStr, out var parsedDate))
            {
                eventDate = parsedDate;
                _logger.LogInformation("[QUOTE GEN] Using event date from session Draft:EventDate: {EventDate}", eventDate.ToString("yyyy-MM-dd"));
            }
            else
            {
                if (fallbackMessages != null)
                {
                    var (dateDto, _) = _chatExtraction.ExtractEventDate(fallbackMessages);
                    if (dateDto.HasValue)
                    {
                        eventDate = dateDto.Value.Date;
                        _logger.LogWarning("[QUOTE GEN] Draft:EventDate not found in session, extracted from conversation: {EventDate}.", 
                            eventDate.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        eventDate = DateTime.Today.AddDays(30);
                        _logger.LogError("[QUOTE GEN] CRITICAL: No event date found in session or conversation. Using fallback date {FallbackDate}. Booking: {BookingNo}", 
                            eventDate.ToString("yyyy-MM-dd"), bookingNo);
                    }
                }
                else
                {
                    eventDate = DateTime.Today.AddDays(30);
                    _logger.LogError("[QUOTE GEN] CRITICAL: No event date in session and no messages for fallback. Using {FallbackDate}. Booking: {BookingNo}", 
                        eventDate.ToString("yyyy-MM-dd"), bookingNo);
                }
            }
            
            booking.dDate = eventDate;
            booking.rDate = eventDate;
            booking.SetDate = eventDate;
            booking.ShowSDate = eventDate;
            booking.ShowEdate = eventDate;
            booking.SDate = eventDate;
            booking.RehDate = eventDate;
            
            if (!string.IsNullOrEmpty(startTime)) booking.showStartTime = PadTimeHHmm(startTime);
            if (!string.IsNullOrEmpty(endTime)) booking.ShowEndTime = PadTimeHHmm(endTime);
            if (!string.IsNullOrEmpty(setupTime)) booking.setupTimeV61 = PadTimeHHmm(setupTime);
            if (!string.IsNullOrEmpty(rehearsalTime)) booking.RehearsalTime = PadTimeHHmm(rehearsalTime);
            
            _db.TblBookings.Add(booking);
            await _db.SaveChangesAsync(ct);
            
            _logger.LogInformation("Booking {BookingNo} created with ID {BookingId}", bookingNo, booking.ID);
            
            if (!string.IsNullOrEmpty(equipmentJson))
            {
                try
                {
                    await _itemService.UpsertSelectedEquipmentAsync(bookingNo, equipmentJson, ct);
                    _logger.LogInformation("Equipment items added to booking {BookingNo} via ItemPersistenceService", bookingNo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add equipment to booking {BookingNo}", bookingNo);
                }
            }
            
            var laborJson = session.GetString("Draft:SelectedLabor");
            if (!string.IsNullOrEmpty(laborJson))
            {
                try
                {
                    var laborItems = JsonSerializer.Deserialize<List<SelectedLaborItem>>(laborJson);
                    if (laborItems != null && laborItems.Count > 0)
                    {
                        _logger.LogInformation("Adding {Count} labor items to booking {BookingNo} via CrewPersistenceService", laborItems.Count, bookingNo);
                        // Prefer structured labor payload for CrewPersistenceService; keep summary for backward compatibility.
                        var facts = new Dictionary<string, string>
                        {
                            ["selected_labor"] = laborJson,
                            ["event_date"] = eventDate.ToString("yyyy-MM-dd")
                        };
                        var laborSummary = string.Join(
                            "\n",
                            laborItems.Select(i =>
                            {
                                var durationHours = i.Hours + (i.Minutes / 60.0);
                                if (durationHours <= 0) durationHours = 1;
                                return $"{i.Quantity}x {i.Task} @ {durationHours:0.##} hours";
                            }));
                        facts["labor_summary"] = laborSummary;
                        if (!string.IsNullOrEmpty(startTime)) facts["show_start_time"] = PadTimeHHmm(startTime);
                        if (!string.IsNullOrEmpty(endTime)) facts["show_end_time"] = PadTimeHHmm(endTime);
                        if (!string.IsNullOrEmpty(setupTime)) facts["setup_time"] = PadTimeHHmm(setupTime);
                        await _crewService.InsertCrewRowsAsync(bookingNo, facts, ct);
                        _logger.LogInformation("Labor items added to booking {BookingNo}", bookingNo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add labor to booking {BookingNo}", bookingNo);
                }
            }
            
            return bookingNo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create booking on-the-fly");
            return "";
        }
    }
}
