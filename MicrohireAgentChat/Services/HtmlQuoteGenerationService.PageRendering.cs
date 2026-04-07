using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;

namespace MicrohireAgentChat.Services;

public partial class HtmlQuoteGenerationService
{
    private string GenerateHtml(QuoteHtmlData data)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Microhire Quote - " + HttpUtility.HtmlEncode(data.EventTitle) + @"</title>
    <style>" + GetStyles() + @"</style>
</head>
<body>
    <div class=""page-container"">");

        // Page 1: Cover page
        sb.AppendLine(GenerateCoverPage(data));

        // Page 2: Overview page
        sb.AppendLine(GenerateOverviewPage(data));

        // Page 3: Equipment page
        sb.AppendLine(GenerateEquipmentPage(data));

        // Page 4: Technical Services (Labour)
        sb.AppendLine(GenerateTechnicalServicesPage(data));

        // Page 5: Budget Summary & Confirmation
        sb.AppendLine(GenerateBudgetSummaryPage(data));

        sb.AppendLine(@"    </div>
</body>
</html>");

        return sb.ToString();
    }

    private string GenerateCoverPage(QuoteHtmlData data)
    {
        return $@"
        <!-- PAGE 1: COVER -->
        <div class=""page cover-page"">
            <div class=""cover-main"">
                <div class=""cover-shape-1""></div>
                <div class=""cover-shape-2""></div>
                
                <div class=""cover-logo"">
                    <div class=""cover-logo-text""><span>M</span>icrohire.</div>
                    <div class=""cover-tagline"">Events That Inspire</div>
                </div>
                
                <div class=""cover-title"">
                    <h1>{HttpUtility.HtmlEncode(data.EventTitle)}</h1>
                </div>
            </div>
            
            <div class=""cover-footer"">
                <div class=""cover-info"">
                    <div class=""cover-date"">{HttpUtility.HtmlEncode(data.EventDate)}</div>
                    <div class=""cover-ref"">{HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                </div>
                <div class=""cover-venue-logo"">
                    <div class=""venue-name"">{HttpUtility.HtmlEncode(data.VenueName.ToUpper())}</div>
                    <div class=""venue-location"">{HttpUtility.HtmlEncode(data.VenueLocation)}</div>
                </div>
            </div>
        </div>";
    }

    private string GenerateOverviewPage(QuoteHtmlData data)
    {
        var eventDateDisplay = string.Equals(data.EventStartDate, data.EventEndDate, StringComparison.Ordinal)
            ? data.EventStartDate
            : $"{data.EventStartDate} to {data.EventEndDate}";

        return $@"
        <!-- PAGE 2: OVERVIEW -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">OVERVIEW</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""contact-block"">
                    <div class=""contact-name"">{HttpUtility.HtmlEncode(data.ContactName)}</div>
                    <div class=""contact-company"">{HttpUtility.HtmlEncode(data.OrganizationName)}</div>
                    <div class=""contact-details"">
                        {HttpUtility.HtmlEncode(data.ContactAddress)}<br>
                        {HttpUtility.HtmlEncode(data.ContactPhone)}
                    </div>
                </div>
                
                <div class=""greeting"">Dear {HttpUtility.HtmlEncode(data.ContactName.Split(' ')[0])},</div>
                
                <p class=""intro-text"">
                    Thank you for the opportunity to present our audio-visual production services for your upcoming event at {HttpUtility.HtmlEncode(data.VenueName)}. We are pleased to provide our proposal in the following pages, based on the information we have received and our recommendations for a seamless and successful event. Our team are committed to achieving your event objectives and welcome the opportunity to discuss your requirements and any budget parameters in further detail. If you have any questions or need further information, please do not hesitate to contact me on the details below. Thank you again and we look forward to speaking to you soon.
                </p>
                
                <div class=""details-table"">
                    <div class=""detail-row"">
                        <div class=""detail-label"">Location</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueName)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Address</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueAddress)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Room</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueRoom)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Date</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(eventDateDisplay)}</div>
                    </div>
                </div>
                
                <div class=""section-divider""></div>
                
                <div class=""manager-section"">
                    <div class=""detail-row"">
                        <div class=""detail-label"">Event Account Manager</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerName)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Mobile Number</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerMobile)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Email</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerEmail)}</div>
                    </div>
                </div>
                
                <p class=""schedule-intro"">Please confirm the following dates and times are accurate:</p>
                
                <div class=""schedule-table"">
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Setup By</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.SetupDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.SetupTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Rehearsal</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.RehearsalDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.RehearsalTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Event Start</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.EventStartDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.EventStartTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Event End</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.EventEndDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.EventEndTime)}</div>
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 2</div>
            </div>
        </div>";
    }

    private string GenerateEquipmentPage(QuoteHtmlData data)
    {
        var equipmentHtml = new StringBuilder();

        // Flat equipment list — all sections rendered without category headers
        foreach (var section in data.EquipmentSections)
        {
            foreach (var item in section.Items)
            {
                var itemClass = item.IsComponent ? "component" : "";
                equipmentHtml.AppendLine($@"
                    <div class=""equipment-row"">
                        <div class=""equipment-item {itemClass}"">{HttpUtility.HtmlEncode(item.Description)}</div>
                        <div class=""equipment-qty"">{(item.Quantity > 0 ? item.Quantity : 1)}</div>
                    </div>");
            }
        }

        // Single equipment total — use the pre-calculated value from BuildQuoteData
        // (which prefers RentalPoint's booking.hire_price) so the Equipment page and
        // Budget Summary always agree.
        decimal equipmentTotal = data.EquipmentTotal;
        equipmentHtml.AppendLine($@"
                <div class=""section-total"">
                    <div class=""section-total-label""></div>
                    <div class=""section-total-amount"">{equipmentTotal:C}</div>
                </div>");

        return $@"
        <!-- PAGE 3: EQUIPMENT & SERVICES -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">EQUIPMENT & SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""equipment-header"">
                    <div class=""equipment-header-desc"">Description</div>
                    <div class=""equipment-header-qty"">Qty.</div>
                </div>

                <div class=""room-header"">
                    <div class=""room-name"">{HttpUtility.HtmlEncode(data.VenueRoom)}</div>
                </div>
                
                {equipmentHtml}
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 2</div>
            </div>
        </div>";
    }

    private string GenerateTechnicalServicesPage(QuoteHtmlData data)
    {
        var labourHtml = new StringBuilder();
        decimal labourTotal = 0;

        // Build labour rows from LaborItems
        if (data.LaborItems.Any())
        {
            foreach (var labor in data.LaborItems)
            {
                labourHtml.AppendLine($@"
                    <tr>
                        <td>{HttpUtility.HtmlEncode(labor.Description)}</td>
                        <td>{HttpUtility.HtmlEncode(labor.Task)}</td>
                        <td>{labor.Quantity}</td>
                        <td>{labor.StartDateTime}</td>
                        <td>{labor.EndDateTime}</td>
                        <td>{labor.Hours}</td>
                        <td>{labor.LineTotal:C}</td>
                    </tr>");
                labourTotal += labor.LineTotal;
            }
        }
        else
        {
            // Default labor rows if none specified
            labourHtml.AppendLine($@"
                <tr>
                    <td>AV Technician</td>
                    <td>Setup</td>
                    <td>1</td>
                    <td>{data.SetupDateShort} {data.SetupTime}</td>
                    <td>{data.EventStartDateShort} {data.EventStartTime}</td>
                    <td>01:30</td>
                    <td>$165.00</td>
                </tr>
                <tr>
                    <td>AV Technician</td>
                    <td>Test & Connect</td>
                    <td>1</td>
                    <td>{data.RehearsalDateShort} {data.RehearsalTime}</td>
                    <td>{data.EventStartDateShort} {data.EventStartTime}</td>
                    <td>01:00</td>
                    <td>$110.00</td>
                </tr>
                <tr>
                    <td>AV Technician</td>
                    <td>Pack Down</td>
                    <td>1</td>
                    <td>{data.EventEndDateShort} {data.EventEndTime}</td>
                    <td>{data.EventEndDateShort} 18:00</td>
                    <td>01:00</td>
                    <td>$110.00</td>
                </tr>");
            labourTotal = 385.00m;
        }

        // Prefer the RentalPoint booking-level labour value if it was set in BuildQuoteData;
        // otherwise use the value computed from crew rows (or the default hardcoded fallback).
        if (data.LabourTotal <= 0)
            data.LabourTotal = labourTotal;

        return $@"
        <!-- PAGE 4: TECHNICAL SERVICES -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">TECHNICAL SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <table class=""labour-table"">
                    <thead>
                        <tr>
                            <th>Description</th>
                            <th>Task</th>
                            <th>Qty</th>
                            <th>Start Date/Time</th>
                            <th>Finish</th>
                            <th>Hrs</th>
                            <th>Total ($)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {labourHtml}
                    </tbody>
                </table>
                
                <div class=""section-total labour-total"">
                    <div class=""section-total-label"">Labour Total</div>
                    <div class=""section-total-amount"">{labourTotal:C}</div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 3</div>
            </div>
        </div>";
    }

    private string GenerateBudgetSummaryPage(QuoteHtmlData data)
    {
        decimal serviceCharge = data.ServiceCharge;
        decimal subTotalExGst = data.EquipmentTotal + data.LabourTotal + serviceCharge;
        // Prefer the pre-calculated totals from BuildQuoteData (which honours
        // RentalPoint's booking.price_quoted). Only fall back to a fresh
        // calculation if those weren't set.
        decimal gst = data.Gst > 0 ? data.Gst : subTotalExGst * 0.10m;
        decimal grandTotal = data.GrandTotal > 0 ? data.GrandTotal : subTotalExGst + gst;

        // Update data totals
        data.Gst = gst;
        data.GrandTotal = grandTotal;

        // Calculate valid until date (30 days from now)
        var validUntil = DateTime.Now.AddDays(30).ToString("ddd d MMM yyyy").ToUpper();

        return $@"
        <!-- PAGE 5: BUDGET SUMMARY -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">BUDGET SUMMARY</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""budget-table"">
                    <div class=""budget-row"">
                        <div class=""budget-label"">Rental Equipment</div>
                        <div class=""budget-value"">{data.EquipmentTotal:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Labour</div>
                        <div class=""budget-value"">{data.LabourTotal:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Service Charge</div>
                        <div class=""budget-value"">{serviceCharge:C}</div>
                    </div>
                    <div class=""budget-row budget-divider"">
                        <div class=""budget-label""></div>
                        <div class=""budget-value"">----------------------</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Sub Total (ex GST)</div>
                        <div class=""budget-value"">{subTotalExGst:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">GST</div>
                        <div class=""budget-value"">{gst:C}</div>
                    </div>
                    <div class=""budget-row budget-divider"">
                        <div class=""budget-label""></div>
                        <div class=""budget-value"">----------------------</div>
                    </div>
                    <div class=""budget-row budget-total"">
                        <div class=""budget-label"">Total</div>
                        <div class=""budget-value"">{grandTotal:C}</div>
                    </div>
                </div>
                
                <div class=""closing-text"">
                    <p>The team at Microhire look forward to working with you to make every aspect of your event a success. To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is <strong>valid until {validUntil}</strong> and our resources are subject to availability at the time of booking.</p>
                    <p>Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.</p>
                    <p>However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.</p>
                    <p>We look forward to working with you on a seamless and successful event.</p>
                </div>
                
                <div class=""manager-signature"">
                    <div class=""manager-name"">{HttpUtility.HtmlEncode(data.AccountManagerName)} <span>Event Staging Manager</span></div>
                    <div class=""manager-company""><strong>Microhire</strong> | {HttpUtility.HtmlEncode(data.VenueName)}</div>
                    <div class=""manager-address"">Microhire @ 111 Mary St, Brisbane City QLD 4000</div>
                    <div class=""manager-contact"">
                        <span class=""label"">M</span> {HttpUtility.HtmlEncode(data.AccountManagerMobile)}<br>
                        <span class=""label"">T</span><br>
                        <span class=""label"">E</span> {HttpUtility.HtmlEncode(data.AccountManagerEmail)}
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 4</div>
            </div>
        </div>

        <!-- PAGE 6: CONFIRMATION OF SERVICES -->
        <div class=""page overview-page confirmation-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">CONFIRMATION OF SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""confirmation-text"">
                    <p>On behalf of {HttpUtility.HtmlEncode(data.OrganizationName)}, I accept this proposal and wish to proceed with the details that are confirmed to be correct.</p>
                    <p>Upon request, any additions or amendments will be updated to this proposal accordingly.</p>
                    <p>We understand that equipment and personnel are not allocated until this document is signed and returned.</p>
                    <p>This proposal and billing details are subject to Microhire's terms and conditions.<br>
                    <a href=""https://www.microhire.com.au/terms-conditions/"" target=""_blank"">https://www.microhire.com.au/terms-conditions/</a></p>
                </div>
                
                <div class=""confirmation-details"">
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Reference Number</div>
                        <div class=""confirmation-value"">{HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                    </div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Total Quotation</div>
                        <div class=""confirmation-value"">{grandTotal:C} inc GST</div>
                    </div>
                </div>

                <div class=""payment-details"">
                    <div class=""payment-title"">Payment Details</div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Account Name</div>
                        <div class=""confirmation-value"">Microhire Pty Ltd</div>
                    </div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">BSB</div>
                        <div class=""confirmation-value"">064-000</div>
                    </div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Account Number</div>
                        <div class=""confirmation-value"">1527 3541</div>
                    </div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Reference</div>
                        <div class=""confirmation-value"">{HttpUtility.HtmlEncode(data.Reference)}</div>
                    </div>
                </div>
                
                <div class=""signature-section"">
                    <div class=""signature-row"">
                        <div class=""signature-field"">
                            <label>Full Name</label>
                            <div class=""signature-line"" style=""border-bottom:1px solid #333;height:30px;line-height:30px;font-size:13px;color:#222;padding-left:4px"">{HttpUtility.HtmlEncode(data.ContactName)}</div>
                        </div>
                        <div class=""signature-field"">
                            <label>Date</label>
                            <div class=""signature-line""></div>
                        </div>
                    </div>
                    <div class=""signature-row"">
                        <div class=""signature-field"">
                            <label>Title / Position</label>
                            <div class=""signature-line""></div>
                        </div>
                        <div class=""signature-field"">
                            <label>Purchase Order (if applicable)</label>
                            <div class=""signature-line""></div>
                        </div>
                    </div>
                    <div class=""signature-row"">
                        <div class=""signature-field full-width"">
                            <label>Signature</label>
                            <div class=""signature-line signature-box""></div>
                        </div>
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 5</div>
            </div>
        </div>";
    }


}
