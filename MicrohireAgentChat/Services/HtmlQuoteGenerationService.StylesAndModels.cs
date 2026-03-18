using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace MicrohireAgentChat.Services;

public partial class HtmlQuoteGenerationService
{
    private string GetStyles()
    {
        return @"
        :root {
            --microhire-red: #E81E25;
            --microhire-red-dark: #C91A20;
            --microhire-red-darker: #9E1D20;
            --microhire-accent: #FF3262;
            --text-dark: #1a1a1a;
            --text-gray: #4a4a4a;
            --text-light: #6b6b6b;
            --bg-light: #f5f5f5;
            --white: #ffffff;
        }

        * { margin: 0; padding: 0; box-sizing: border-box; }

        body {
            font-family: 'Lato', sans-serif;
            background: #2a2a2a;
            color: var(--text-dark);
            line-height: 1.6;
        }

        .page-container { max-width: 850px; margin: 0 auto; padding: 20px; }

        .page {
            background: var(--white);
            width: 100%;
            min-height: 1100px;
            margin-bottom: 40px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
            position: relative;
            overflow: hidden;
        }

        /* Cover Page */
        .cover-page { background: var(--microhire-red); display: flex; flex-direction: column; }
        .cover-main { flex: 1; min-height: 900px; position: relative; padding: 40px; display: flex; flex-direction: column; }
        .cover-shape-1 { position: absolute; top: 0; left: 0; width: 50%; height: 70%; background: var(--microhire-red-dark); clip-path: polygon(0 0, 100% 0, 50% 100%, 0 100%); }
        .cover-shape-2 { position: absolute; top: 20%; right: 0; width: 55%; height: 80%; background: var(--microhire-red-darker); clip-path: polygon(40% 0, 100% 0, 100% 100%, 0 100%); }
        .cover-logo { position: relative; z-index: 10; text-align: right; margin-bottom: auto; }
        .cover-logo-text { font-family: 'Montserrat', sans-serif; font-weight: 800; font-size: 48px; color: var(--white); letter-spacing: 2px; }
        .cover-logo-text span { color: var(--white); font-weight: 300; }
        .cover-tagline { font-family: 'Montserrat', sans-serif; font-size: 11px; color: var(--white); letter-spacing: 4px; text-transform: uppercase; opacity: 0.9; }
        .cover-title { position: relative; z-index: 10; margin-top: auto; padding-bottom: 40px; }
        .cover-title h1 { font-family: 'Montserrat', sans-serif; font-weight: 300; font-size: clamp(32px, 5vw, 52px); color: var(--white); line-height: 1.2; }
        .cover-footer { background: var(--white); padding: 30px 40px; display: flex; justify-content: space-between; align-items: flex-end; }
        .cover-date { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 18px; color: var(--microhire-red); }
        .cover-ref { font-family: 'Montserrat', sans-serif; font-weight: 500; font-size: 14px; color: var(--microhire-red); margin-top: 5px; }
        .cover-venue-logo { text-align: right; }
        .venue-name { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 20px; color: var(--text-dark); letter-spacing: 2px; }
        .venue-location { font-family: 'Montserrat', sans-serif; font-size: 11px; color: var(--text-gray); letter-spacing: 3px; text-transform: uppercase; }

        /* Overview Page */
        .overview-page { padding: 0; display: flex; flex-direction: column; }
        .page-header { padding: 35px 45px 25px; border-bottom: 3px solid var(--microhire-red); }
        .page-section-title { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 14px; color: var(--microhire-red-darker); letter-spacing: 1px; margin-bottom: 8px; }
        .page-event-title { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: clamp(18px, 3vw, 24px); color: var(--microhire-red); display: flex; align-items: center; gap: 12px; }
        .page-event-title::before { content: ''; width: 0; height: 0; border-top: 10px solid transparent; border-bottom: 10px solid transparent; border-left: 16px solid var(--microhire-red); }
        .page-content { flex: 1; padding: 30px 45px; display: flex; flex-direction: column; }
        .contact-block { margin-bottom: 25px; }
        .contact-name { font-weight: 700; font-size: 15px; color: var(--text-dark); }
        .contact-company { font-weight: 400; font-size: 14px; color: var(--text-dark); }
        .contact-details { font-size: 13px; color: var(--text-gray); margin-top: 4px; }
        .greeting { font-size: 14px; color: var(--text-dark); margin: 20px 0 15px; }
        .intro-text { font-size: 13px; color: var(--text-gray); line-height: 1.7; text-align: justify; margin-bottom: 25px; }
        .details-table { margin-bottom: 25px; }
        .detail-row { display: flex; padding: 8px 0; border-bottom: 1px solid #eee; }
        .detail-row:last-child { border-bottom: none; }
        .detail-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; letter-spacing: 0.5px; width: 180px; flex-shrink: 0; }
        .detail-value { font-size: 13px; color: var(--text-dark); }
        .section-divider { height: 1px; background: linear-gradient(90deg, var(--microhire-red) 0%, transparent 100%); margin: 20px 0; opacity: 0.3; }
        .manager-section { margin-bottom: 25px; }
        .manager-section .detail-row { border-bottom: none; padding: 5px 0; }
        .schedule-intro { font-size: 12px; font-style: italic; color: var(--text-gray); margin-bottom: 15px; }
        .schedule-table { width: 100%; }
        .schedule-row { display: flex; padding: 6px 0; }
        .schedule-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; width: 130px; flex-shrink: 0; }
        .schedule-date { font-size: 13px; color: var(--text-dark); width: 220px; }
        .schedule-time { font-size: 13px; color: var(--text-gray); }
        .page-footer { padding: 20px 45px; display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #eee; margin-top: auto; }
        .footer-logo { display: flex; align-items: center; gap: 8px; }
        .footer-logo-icon { width: 40px; height: 40px; background: var(--microhire-red); border-radius: 50%; display: flex; align-items: center; justify-content: center; position: relative; }
        .footer-logo-icon::after { content: 'M'; color: white; font-family: 'Montserrat', sans-serif; font-weight: 800; font-size: 16px; }
        .footer-ref { font-size: 11px; color: var(--text-light); }
        .footer-page-info { font-size: 11px; color: var(--text-light); }

        /* Equipment Page */
        .equipment-table { width: 100%; margin-bottom: 20px; }
        .equipment-header { display: flex; padding: 12px 0; border-bottom: 2px solid var(--microhire-red); margin-bottom: 15px; }
        .equipment-header-desc { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 12px; color: var(--microhire-red); flex: 1; }
        .equipment-header-qty { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 12px; color: var(--microhire-red); width: 80px; text-align: center; }
        .room-header { margin-bottom: 20px; }
        .room-name { font-weight: 700; font-size: 14px; color: var(--text-dark); }
        .room-times { font-style: italic; font-size: 12px; color: var(--text-gray); margin-top: 4px; }
        .equipment-category-header { font-style: italic; font-weight: 700; font-size: 14px; color: var(--text-dark); margin: 20px 0 10px; }
        .equipment-section { margin-bottom: 20px; }
        .equipment-category { font-style: italic; font-weight: 600; font-size: 13px; color: var(--text-dark); margin-bottom: 8px; }
        .equipment-subcategory { font-style: italic; text-decoration: underline; font-size: 13px; color: var(--text-dark); margin-bottom: 8px; }
        .equipment-group-title { font-style: italic; font-size: 12px; color: var(--text-gray); margin-bottom: 5px; }
        .equipment-row { display: flex; padding: 4px 0; align-items: center; }
        .equipment-item { flex: 1; font-size: 13px; color: var(--text-dark); }
        .equipment-item.main { font-weight: 600; }
        .equipment-item.component { margin-left: 15px; font-style: italic; color: var(--text-gray); }
        .equipment-qty { width: 80px; text-align: center; font-size: 13px; color: var(--text-dark); }
        .equipment-note { font-style: italic; font-size: 13px; color: var(--text-dark); margin: 15px 0 15px 15px; }
        .equipment-brief { margin: 15px 0 20px 0; }
        .equipment-brief-label { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 12px; color: var(--text-dark); text-transform: uppercase; margin-bottom: 6px; }
        .equipment-brief-text { font-size: 13px; color: var(--text-gray); line-height: 1.6; }
        .equipment-notes { margin: 25px 0 15px 0; padding-top: 15px; border-top: 1px solid #eee; }
        .equipment-notes-label { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 12px; color: var(--text-dark); text-transform: uppercase; margin-bottom: 8px; }
        .equipment-notes-text { font-size: 12px; color: var(--text-gray); line-height: 1.6; margin-bottom: 8px; }
        .section-total { display: flex; justify-content: flex-end; padding: 15px 0; margin-top: 10px; border-top: 1px solid #eee; }
        .section-total-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 13px; color: var(--text-dark); margin-right: 30px; }
        .section-total-amount { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 14px; color: var(--text-dark); }

        /* Labour/Technical Services Page */
        .labour-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
        .labour-table th { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; text-align: left; padding: 10px 8px; border-bottom: 2px solid var(--microhire-red); }
        .labour-table td { font-size: 12px; color: var(--text-dark); padding: 8px; border-bottom: 1px solid #eee; }
        .labour-total { margin-top: 20px; }

        /* Budget Summary Page */
        .budget-table { max-width: 350px; margin-bottom: 30px; }
        .budget-row { display: flex; justify-content: space-between; padding: 6px 0; }
        .budget-label { font-size: 13px; color: var(--text-dark); }
        .budget-value { font-size: 13px; color: var(--text-dark); text-align: right; }
        .budget-divider .budget-value { color: var(--text-gray); }
        .budget-total { font-weight: 700; }
        .budget-total .budget-label, .budget-total .budget-value { font-size: 14px; }

        .closing-text { font-size: 12px; color: var(--text-gray); line-height: 1.7; margin-bottom: 30px; }
        .closing-text p { margin-bottom: 12px; }
        .closing-text strong { color: var(--text-dark); }

        .manager-signature { margin-top: 30px; }
        .manager-name { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 16px; color: var(--text-dark); margin-bottom: 5px; }
        .manager-name span { font-weight: 400; font-size: 13px; margin-left: 10px; }
        .manager-company { font-size: 13px; color: var(--text-dark); margin-bottom: 3px; }
        .manager-address { font-size: 12px; color: var(--text-gray); margin-bottom: 10px; }
        .manager-contact { font-size: 12px; color: var(--text-dark); line-height: 1.8; }
        .manager-contact .label { color: var(--microhire-red); font-weight: 600; margin-right: 10px; }

        /* Confirmation Page */
        .confirmation-page .confirmation-text { font-size: 12px; color: var(--text-gray); line-height: 1.7; margin-bottom: 30px; }
        .confirmation-page .confirmation-text p { margin-bottom: 12px; }
        .confirmation-page .confirmation-text a { color: var(--microhire-red); }
        .confirmation-details { margin-bottom: 30px; }
        .payment-details { margin-bottom: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0; }
        .payment-title { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 12px; color: var(--microhire-red); text-transform: uppercase; margin-bottom: 15px; }
        .confirmation-row { display: flex; margin-bottom: 10px; }
        .confirmation-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; width: 150px; }
        .confirmation-value { font-size: 13px; color: var(--text-dark); }
        
        .signature-section { margin-top: 30px; }
        .signature-row { display: flex; gap: 30px; margin-bottom: 25px; }
        .signature-field { flex: 1; }
        .signature-field.full-width { flex: 1 1 100%; }
        .signature-field label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--text-gray); text-transform: uppercase; display: block; margin-bottom: 8px; }
        .signature-line { border-bottom: 1px solid var(--text-dark); height: 30px; }
        .signature-box { height: 80px; border: 1px solid var(--text-dark); border-radius: 4px; }

        @media print {
            body { background: white; }
            .page-container { padding: 0; }
            .page { box-shadow: none; margin-bottom: 0; page-break-after: always; }
            .page:last-child { page-break-after: avoid; }
        }

        @media (max-width: 600px) {
            .page-container { padding: 10px; }
            .cover-main { padding: 25px; }
            .cover-footer { padding: 20px 25px; }
            .page-header, .page-content, .page-footer { padding-left: 25px; padding-right: 25px; }
            .detail-label, .schedule-label { width: 120px; }
        }";
    }

    private decimal GetUnitPrice(TblItemtran item, TblInvmas? invItem, TblRatetbl? rateItem)
    {
        // UnitRate is the per-unit price in Rental Point; Price is UnitRate × Qty (total line price).
        // Always use UnitRate first to avoid double-counting when multiplied by qty in the caller.
        if (item.UnitRate.HasValue && item.UnitRate.Value > 0)
            return (decimal)item.UnitRate.Value;
        if (rateItem?.rate_1st_day.HasValue == true && rateItem.rate_1st_day.Value > 0)
            return (decimal)rateItem.rate_1st_day.Value;
        if (invItem?.retail_price.HasValue == true && invItem.retail_price.Value > 0)
            return (decimal)invItem.retail_price.Value;
        // Price is the total line price; divide by qty as a last resort to recover the unit price.
        if (item.Price.HasValue && item.Price.Value > 0)
            return (decimal)(item.Price.Value / Math.Max((double)(item.TransQty ?? 1), 1));
        return 0;
    }

    private string GetBestDescription(TblItemtran item, TblInvmas? invItem)
    {
        if (!string.IsNullOrWhiteSpace(item.CommentDescV42))
            return item.CommentDescV42.Trim();
        if (invItem != null)
        {
            if (!string.IsNullOrWhiteSpace(invItem.PrintedDesc))
                return invItem.PrintedDesc.Trim();
            if (!string.IsNullOrWhiteSpace(invItem.descriptionv6))
                return invItem.descriptionv6.Trim();
        }
        return item.ProductCodeV42?.Trim() ?? "Equipment Item";
    }

    private string? FormatTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;

        // Time is stored as HHmm (e.g., "0900")
        if (time.Length == 4 && int.TryParse(time, out _))
        {
            return $"{time.Substring(0, 2)}:{time.Substring(2, 2)}";
        }

        return time;
    }

    /// <summary>
    /// Strips projector-area suffixes that may be present on legacy stored room names.
    /// New bookings will never have these, but this guards against pre-existing DB data.
    /// </summary>
    private static string? StripProjectorAreaSuffix(string? venueRoom)
    {
        if (string.IsNullOrWhiteSpace(venueRoom)) return venueRoom;
        var s = venueRoom.Trim();
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s+[A-F](?:/[A-F])*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*\(Proj(?:ector)?\s+[A-F](?:/[A-F])*\)$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }
}

// Supporting data models
public class QuoteHtmlData
{
    public string EventTitle { get; set; } = "";
    public string Reference { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string EventDateFull { get; set; } = "";
    
    public string ContactName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string ContactAddress { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    
    public string VenueName { get; set; } = "";
    public string VenueAddress { get; set; } = "";
    public string VenueRoom { get; set; } = "";
    public string VenueLocation { get; set; } = "";
    
    public string AccountManagerName { get; set; } = "";
    public string AccountManagerMobile { get; set; } = "";
    public string AccountManagerEmail { get; set; } = "";
    
    public string SetupDate { get; set; } = "";
    public string SetupDateShort { get; set; } = "";
    public string SetupTime { get; set; } = "";
    public string RehearsalDate { get; set; } = "";
    public string RehearsalDateShort { get; set; } = "";
    public string RehearsalTime { get; set; } = "";
    public string EventStartDate { get; set; } = "";
    public string EventStartDateShort { get; set; } = "";
    public string EventStartTime { get; set; } = "";
    public string EventEndDate { get; set; } = "";
    public string EventEndDateShort { get; set; } = "";
    public string EventEndTime { get; set; } = "";
    
    public List<QuoteEquipmentSection> EquipmentSections { get; set; } = new();
    public List<QuoteLaborItem> LaborItems { get; set; } = new();
    public decimal EquipmentTotal { get; set; }
    public decimal Transport { get; set; }
    public decimal LabourTotal { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal Gst { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Event brief/description (used in short form).</summary>
    public string Brief { get; set; } = "";
}

public class QuoteLaborItem
{
    public string Description { get; set; } = "";
    public string Task { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string StartDateTime { get; set; } = "";
    public string EndDateTime { get; set; } = "";
    public string Hours { get; set; } = "";
    public decimal LineTotal { get; set; }
}

public class QuoteEquipmentSection
{
    public string Category { get; set; } = "";
    public string? SubCategory { get; set; }
    public List<QuoteEquipmentItem> Items { get; set; } = new();
}

public class QuoteEquipmentItem
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsMainItem { get; set; }
    public bool IsComponent { get; set; }
}
