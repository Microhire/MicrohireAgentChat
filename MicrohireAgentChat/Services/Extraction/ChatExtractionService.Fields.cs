using MicrohireAgentChat.Models;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MicrohireAgentChat.Services.Extraction;

public sealed partial class ChatExtractionService
{
    public Dictionary<string, string> ExtractExpectedFields(IEnumerable<DisplayMessage> messages)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Booking ID"] = null!,
            ["Booking #"] = null!,
            ["Quote Total Amount inc GST"] = null!,
            ["Equipment Cost"] = null!,
            ["Booking Type"] = null!,
            ["Labor Cost"] = null!,
            ["Service Charge"] = null!,
            ["Contact Name"] = null!,
            ["Show Start Time"] = null!,
            ["Show End Time"] = null!,
            ["Booking Status"] = null!,
            ["Room"] = null!,
            ["Show Name"] = null!,
            ["Organization"] = null!,
            ["Sales Person Code"] = null!,
            ["Show Start Date"] = null!,
            ["Show Finishes"] = null!,
            ["Setup Date"] = null!,
            ["Rehearsal Date"] = null!,
            ["Bill To"] = null!,
            ["Event Type"] = null!,
            ["Customer ID"] = null!,
            ["Venue Name"] = null!
        };

        var lines = messages
            .OrderBy(m => m.Timestamp)
            .Select(m => string.Join(" ", m.Parts ?? Enumerable.Empty<string>()))
            .ToList();
        var full = string.Join("\n", lines);

        string? Set(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result[key] = value!.Trim();
            return value;
        }

        static string? FirstOrNull(params string?[] arr) => arr.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        string? FindText(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var pattern = $@"(?:(?:^|\b){Regex.Escape(label)}\s*[:\-]?\s*)([^\r\n,;|]+)";
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        string? FindId(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var m = Regex.Match(text, $@"{Regex.Escape(label)}\s*[:\-]?\s*([A-Za-z0-9\-_/]+)", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        string? FindMoney(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var p = $@"{Regex.Escape(label)}\s*[:\-]?\s*(?:AUD\s*)?(\$?\s*[0-9][0-9,]*\.?[0-9]{{0,2}})(?:\s*(?:AUD|inc\s*GST|GST\s*incl\.?)\b)?";
                var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                if (m.Success) return NormalizeMoney(m.Groups[1].Value);
            }
            var near = string.Join("|", labels.Select(Regex.Escape));
            var nearPat = $@"(?:{near}).{{0,40}}(\$?\s*[0-9][0-9,]*\.?[0-9]{{0,2}})";
            var nearM = Regex.Match(text, nearPat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (nearM.Success) return NormalizeMoney(nearM.Groups[1].Value);
            return null;

            static string NormalizeMoney(string raw)
            {
                var v = raw.Replace(" ", "");
                if (!v.StartsWith("$")) v = "$" + v.TrimStart('$');
                return v;
            }
        }

        string? FindDate(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var pat = $@"{Regex.Escape(label)}\s*[:\-]?\s*([A-Za-z]{{3,9}}\s+\d{{1,2}}(?:st|nd|rd|th)?(?:,?\s+\d{{4}})?|\d{{1,2}}/\d{{1,2}}/\d{{2,4}}|\d{{4}}-\d{{2}}-\d{{2}}|\d{{1,2}}\s+[A-Za-z]{{3,9}}\s+\d{{4}})";
                foreach (Match m in Regex.Matches(text, pat, RegexOptions.IgnoreCase))
                {
                    var token = m.Groups[1].Value;
                    if (TryParseDateToken(token, out var dto))
                        return dto.ToString("dd MMM yyyy");
                }
            }
            return null;
        }

        string? FindTime(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                var pat = $@"{Regex.Escape(label)}\s*[:\-]?\s*([01]?\d|2[0-3]):([0-5]\d)\s*(am|pm)?";
                var m = Regex.Match(text, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var token = $"{m.Groups[1].Value}:{m.Groups[2].Value} {m.Groups[3].Value}".Trim();
                    if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt.ToString("HH:mm");
                    return token;
                }
            }
            foreach (var label in labels)
            {
                var pat2 = $@"{Regex.Escape(label)}\s*[:\-]?\s*([1-9]|1[0-2])\s*(am|pm)";
                var m2 = Regex.Match(text, pat2, RegexOptions.IgnoreCase);
                if (m2.Success)
                {
                    var token = $"{m2.Groups[1].Value}:00 {m2.Groups[2].Value}";
                    if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt.ToString("HH:mm");
                    return token;
                }
            }
            return null;
        }

        static bool TryParseDateToken(string token, out DateTimeOffset dto)
        {
            token = token.Trim();
            token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);

            var cultures = new[]
            {
                CultureInfo.GetCultureInfo("en-AU"),
                CultureInfo.GetCultureInfo("en-GB"),
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.InvariantCulture
            };

            if (Regex.IsMatch(token, @"^\d{4}-\d{2}-\d{2}$") &&
                DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var iso))
            { dto = new DateTimeOffset(iso); return true; }

            if (Regex.IsMatch(token, @"^\d{1,2}/\d{1,2}/\d{2,4}$"))
            {
                var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yy", "d/M/yy" };
                foreach (var f in fmts)
                    if (DateTime.TryParseExact(token, f, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
                    { dto = new DateTimeOffset(d); return true; }
            }

            foreach (var c in cultures)
                if (DateTime.TryParse(token, c, DateTimeStyles.AssumeLocal, out var dt))
                { dto = new DateTimeOffset(dt); return true; }

            dto = default;
            return false;
        }

        Set("Booking ID", FirstOrNull(FindId(full, "booking id", "id")));
        Set("Booking #", FirstOrNull(FindId(full, "booking #", "booking no", "booking number", "booking ref", "reference")));
        Set("Quote Total Amount inc GST", FindMoney(full, "total inc gst", "quote total", "grand total", "total amount", "total incl gst", "total including gst"));
        Set("Equipment Cost", FindMoney(full, "equipment cost", "equipment total", "gear total", "av total"));
        Set("Booking Type", FindText(full, "booking type", "type", "event type"));
        Set("Labor Cost", FindMoney(full, "labour cost", "labor cost", "crew total", "labour total", "labor total"));
        Set("Service Charge", FindMoney(full, "service charge", "surcharge", "fees", "handling"));
        Set("Contact Name", FirstOrNull(FindText(full, "contact name", "client name", "customer name", "name")));
        Set("Show Start Time", FirstOrNull(FindTime(full, "show start time", "start time", "event start", "doors at")));
        Set("Show End Time", FirstOrNull(FindTime(full, "show end time", "end time", "event end", "finish time", "finishes at")));
        Set("Booking Status", FirstOrNull(FindText(full, "booking status", "status")));
        Set("Room", FirstOrNull(FindText(full, "room", "venue room", "ballroom", "space")));
        Set("Show Name", FirstOrNull(FindText(full, "show name", "event name", "name of event", "show")));
        Set("Organization", FirstOrNull(FindText(full, "organization", "organisation", "company")));
        Set("Sales Person Code", FirstOrNull(FindText(full, "sales person code", "sales code", "rep code")));
        Set("Show Start Date", FirstOrNull(FindDate(full, "show start date", "event date", "start date", "on")));
        Set("Show Finishes", FirstOrNull(FindDate(full, "show finishes", "finish date", "end date", "to", "until")));
        Set("Setup Date", FirstOrNull(FindDate(full, "setup date", "set date", "bump in date", "move-in date")));
        Set("Rehearsal Date", FirstOrNull(FindDate(full, "rehearsal date", "reh date")));
        Set("Bill To", FirstOrNull(FindText(full, "bill to", "billing name", "invoice to")));
        Set("Event Type", FirstOrNull(FindText(full, "event type", "type of event")));
        Set("Customer ID", FirstOrNull(FindId(full, "customer id", "client id", "cust id")));
        Set("Venue Name", FirstOrNull(FindText(full, "venue name", "venue", "hotel", "location")));

        return result;
    }

    public (DateTimeOffset? EventDate, string? VenueName, string? DateMatched, string? VenueMatched)
        ExtractVenueAndEventDate(IEnumerable<DisplayMessage> messages)
    {
        var items = messages
            .OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => new { line = line.Trim(), role = m.Role }))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList();

        var userLines = items.Where(x => x.role.Equals("user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line).ToList();
        var asstLines = items.Where(x => !x.role.Equals("user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line).ToList();
        var fullText = string.Join("\n", items.Select(x => x.line));

        var yearInContext = Regex.Matches(fullText, @"\b(20\d{2})\b")
                                 .OfType<Match>()
                                 .Select(m => int.Parse(m.Groups[1].Value))
                                 .Cast<int?>()
                                 .FirstOrDefault();

        var date = FindEventDate(userLines, yearInContext, out var dateMatched)
                   ?? FindEventDate(asstLines, yearInContext, out dateMatched);

        var venue = FindVenue(userLines, out var venueMatched)
                    ?? FindVenue(asstLines, out venueMatched);

        return (date, venue, dateMatched, venueMatched);

        static DateTimeOffset? FindEventDate(IEnumerable<string> src, int? yearHint, out string? matched)
        {
            matched = null;

            foreach (var line in src)
            {
                var m = Regex.Match(line, @"\b(event\s*date|date|on)\s*[:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
                if (m.Success && TryParseDateToken(m.Groups[2].Value.Trim(), yearHint, out var dto))
                {
                    matched = m.Groups[2].Value.Trim();
                    return dto;
                }
            }

            foreach (var line in src)
            {
                foreach (Match mm in Regex.Matches(line,
                    @"(\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}|[A-Za-z]{3,9}\s+\d{1,2}(?:st|nd|rd|th)?(?:,?\s+\d{4})?|\d{1,2}\s+[A-Za-z]{3,9}(?:\s+\d{4})?)",
                    RegexOptions.IgnoreCase))
                {
                    var token = mm.Value.Trim();
                    if (TryParseDateToken(token, yearHint, out var dto))
                    {
                        matched = token;
                        return dto;
                    }
                }
            }

            return null;
        }

        static bool TryParseDateToken(string token, int? yearHint, out DateTimeOffset dto)
        {
            dto = default;
            if (string.IsNullOrWhiteSpace(token)) return false;

            token = token.Trim();
            token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);
            var hasExplicitYear = Regex.IsMatch(token, @"\b\d{4}\b");

            if (Regex.IsMatch(token, @"^\d{4}-\d{2}-\d{2}$") &&
                DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var iso))
            {
                dto = new DateTimeOffset(iso);
            }
            else
            {
                var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yy", "M/d/yy" };
                foreach (var f in fmts)
                {
                    if (DateTime.TryParseExact(token, f, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d1))
                    {
                        dto = new DateTimeOffset(d1);
                        break;
                    }
                }
            }

            if (dto == default)
            {
                foreach (var c in new[] { "en-AU", "en-GB", "en-US" })
                {
                    if (DateTime.TryParse(token, CultureInfo.GetCultureInfo(c), DateTimeStyles.AssumeLocal, out var d2))
                    {
                        dto = new DateTimeOffset(d2);
                        break;
                    }
                }
            }

            if (dto == default)
            {
                var m = Regex.Match(token, @"^(?<day>\d{1,2})\s+(?<mon>[A-Za-z]{3,9})(?:\s+(?<yr>\d{4}))?$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var day = int.Parse(m.Groups["day"].Value);
                    if (!DateTime.TryParseExact(m.Groups["mon"].Value.Substring(0, 3), "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monDt))
                        return false;
                    var mon = monDt.Month;
                    var yr = m.Groups["yr"].Success ? int.Parse(m.Groups["yr"].Value) : InferYearFor(day, mon, yearHint);
                    if (yr > 0 && day >= 1 && day <= DateTime.DaysInMonth(yr, mon))
                    {
                        dto = new DateTimeOffset(new DateTime(yr, mon, day));
                    }
                }
            }

            if (dto == default)
                return false;

            if (!hasExplicitYear)
            {
                var targetYear = yearHint ?? InferYearFor(dto.Day, dto.Month, yearHint);
                var adjustedDay = Math.Min(dto.Day, DateTime.DaysInMonth(targetYear, dto.Month));
                dto = new DateTimeOffset(new DateTime(targetYear, dto.Month, adjustedDay), dto.Offset);
            }

            return true;
        }

        static int InferYearFor(int day, int month, int? yearHint)
        {
            if (yearHint.HasValue) return yearHint.Value;
            var now = DateTime.Now;
            var yr = now.Year;
            var candidate = new DateTime(yr, month, Math.Min(day, DateTime.DaysInMonth(yr, month)));
            if (candidate.Date < now.Date) yr++;
            return yr;
        }

        static string? FindVenue(IEnumerable<string> src, out string? matched)
        {
            matched = null;

            var venueKeywords = new[] {
                "westin","brisbane","hotel","resort","ballroom","hall","centre","center",
                "convention","banquet","club","theatre","theater","auditorium","room","suite",
                "terrace","lawn"
            };

            var genericEvent = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "gala","dinner","gala dinner","conference","meeting","event","reception",
                "party","wedding","concert","seminar","workshop","banquet"
            };

            static string Clean(string s) => s.Trim().TrimEnd('.', ',', ';', '?', '!');
            bool HasVenueKeyword(string s) => venueKeywords.Any(k => s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

            bool LooksGeneric(string s)
            {
                if (genericEvent.Contains(s)) return true;
                var words = s.Split(new[] { ' ', '-', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return words.Length > 0 && words.All(w => genericEvent.Contains(w));
            }

            foreach (var line in src)
            {
                var m = Regex.Match(line, @"\b(venue\s*name|venue|location|hotel)\s*[:\-]?\s*(.+)$",
                                    RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var cand = Clean(m.Groups[2].Value);
                    if (!LooksGeneric(cand))
                    {
                        matched = cand;
                        return cand;
                    }
                }
            }

            foreach (var line in src)
            {
                var m = Regex.Match(line, @"\bat\s+([A-Z][A-Za-z0-9&\-\.\s']{2,})",
                                    RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var cand = Clean(m.Groups[1].Value);
                    if (!LooksGeneric(cand) && HasVenueKeyword(cand))
                    {
                        matched = cand;
                        return cand;
                    }
                }
            }

            foreach (var line in src)
            {
                if (line.Length <= 80)
                {
                    var cand = Clean(line);
                    if (!LooksGeneric(cand) && HasVenueKeyword(cand))
                    {
                        matched = cand;
                        return cand;
                    }
                }
            }

            return null;
        }
    }



}
