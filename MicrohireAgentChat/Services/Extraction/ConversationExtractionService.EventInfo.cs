using MicrohireAgentChat.Models;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MicrohireAgentChat.Services.Extraction;

public sealed partial class ConversationExtractionService
{
    public Dictionary<string, string> ExtractExpectedFields(IEnumerable<DisplayMessage> messages)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var (eventDate, venueName, dateMatched, venueMatched) = ExtractVenueAndEventDate(messages);
        if (eventDate.HasValue)
            facts["event_date"] = eventDate.Value.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(venueName))
            facts["venue_name"] = venueName!;
        var roomName = ExtractRoom(messages);
        if (!string.IsNullOrWhiteSpace(roomName))
            facts["venue_room"] = roomName!;

        var contactInfo = ExtractContactInfo(messages);
        if (!string.IsNullOrWhiteSpace(contactInfo.Name))
            facts["contact_name"] = contactInfo.Name!;
        if (!string.IsNullOrWhiteSpace(contactInfo.Email))
            facts["contact_email"] = contactInfo.Email!;
        if (!string.IsNullOrWhiteSpace(contactInfo.PhoneE164))
            facts["contact_phone"] = contactInfo.PhoneE164!;
        if (!string.IsNullOrWhiteSpace(contactInfo.Position))
            facts["contact_position"] = contactInfo.Position!;

        var (org, addr) = ExtractOrganisationFromTranscript(messages);
        if (!string.IsNullOrWhiteSpace(org))
            facts["organization"] = org!;
        if (!string.IsNullOrWhiteSpace(addr))
            facts["organization_address"] = addr!;

        // Extract selected equipment from "Selected equipment: ..." messages
        var equipment = ExtractSelectedEquipment(messages);
        if (equipment.Any())
        {
            // Store as JSON for the ItemPersistenceService
            facts["selected_equipment"] = System.Text.Json.JsonSerializer.Serialize(equipment);
        }

        // Extract times from "Schedule selected: ..." messages
        var times = ExtractScheduleTimes(messages);
        foreach (var kvp in times)
        {
            facts[kvp.Key] = kvp.Value;
        }

        return facts;
    }

    /// <summary>
    /// Extract schedule times from "Schedule selected: ..." or "I've selected this schedule: ..." messages
    /// </summary>
    public Dictionary<string, string> ExtractScheduleTimes(IEnumerable<DisplayMessage> messages)
    {
        var times = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: "Schedule selected: ..." or "I've selected this schedule: ..." (reformatted after time picker submit)
        var schedulePattern = @"(?:Schedule selected|I've selected this schedule):\s*(.+)";
        
        // Individual time patterns within the schedule - enhanced to distinguish semantic meaning
        var timePatterns = new Dictionary<string, string[]>
        {
            ["setup_time"] = new[] {
                @"Setup\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Set[- ]?up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"
            },
            ["rehearsal_time"] = new[] {
                @"Rehearsal\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Rehearsal\s+at\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"
            },
            ["show_start_time"] = new[] {
                @"(?:Event\s+)?Start\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"(?:Event\s+)?Start\s+at\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Event\s+Start\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Start\s+time\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"
            },
            ["show_end_time"] = new[] {
                @"(?:Event\s+)?(?:End|Finish)\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"(?:Event\s+)?(?:End|Finish)\s+at\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Event\s+End\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Finish\s+time\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"
            },
            ["strike_time"] = new[] {
                @"Pack[- ]?Up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Pack\s+Up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)",
                @"Pack\s+down\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"
            }
        };

        foreach (var msg in messages)
        {
            if (!string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.Join("\n", msg.Parts ?? Enumerable.Empty<string>());
            
            var scheduleMatch = Regex.Match(text, schedulePattern, RegexOptions.IgnoreCase);
            if (scheduleMatch.Success)
            {
                var scheduleText = scheduleMatch.Groups[1].Value;
                _logger.LogInformation("Found schedule text: {Schedule}", scheduleText);

                foreach (var kvp in timePatterns)
                {
                    foreach (var pattern in kvp.Value)
                    {
                        var match = Regex.Match(scheduleText, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var timeStr = match.Groups[1].Value.Trim();
                            var normalized = NormalizeTimeToHHmm(timeStr);
                            if (!string.IsNullOrWhiteSpace(normalized))
                            {
                                times[kvp.Key] = normalized;
                                _logger.LogInformation("Extracted {Key}: {Value}", kvp.Key, normalized);
                            }
                            break; // Found a match for this key, move to next
                        }
                    }
                }
            }
        }

        return times;
    }

    /// <summary>
    /// Normalize time string like "7:00 AM" or "4:00 PM" to "HHmm" format
    /// </summary>
    private static string? NormalizeTimeToHHmm(string timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            return null;

        // Try parsing as DateTime to handle AM/PM
        if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("HHmm");
        }

        // Try manual parsing for formats like "7:00 AM" or "7:30AM" or "16:00"
        var match = Regex.Match(timeStr, @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM)?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value);
            var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var ampm = match.Groups[3].Success ? match.Groups[3].Value.ToUpperInvariant() : null;

            // Convert to 24-hour format
            if (ampm == "PM" && hour < 12) hour += 12;
            if (ampm == "AM" && hour == 12) hour = 0;

            return $"{hour:D2}{minute:D2}";
        }

        return null;
    }

    /// <summary>
    /// Extract event type from conversation messages (interviews, meetings, conferences, etc.)
    /// </summary>
    public (string? EventType, string? Match) ExtractEventType(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        // Common event types and their patterns
        var eventTypePatterns = new Dictionary<string, string[]>
        {
            ["interviews"] = new[] {
                @"running\s+interviews",
                @"interview\s+process",
                @"conducting\s+interviews",
                @"holding\s+interviews",
                @"interview\s+session",
                @"interviews\s+for",
                @"\binterviews?\b"
            },
            ["meeting"] = new[] {
                @"(?:running|holding|organizing)\s+a?\s?meeting",
                @"meeting\s+(?:with|for)",
                @"business\s+meeting",
                @"team\s+meeting",
                @"\bmeetings?\b"
            },
            ["conference"] = new[] {
                @"conference\s+(?:call|meeting|session)",
                @"running\s+a\s+conference",
                @"conference\s+event",
                @"\bconferences?\b"
            },
            ["seminar"] = new[] {
                @"seminar\s+(?:session|event)",
                @"running\s+a\s+seminar",
                @"seminar\s+presentation",
                @"\bseminars?\b"
            },
            ["workshop"] = new[] {
                @"workshop\s+(?:session|event)",
                @"running\s+a\s+workshop",
                @"hands-on\s+workshop",
                @"\bworkshops?\b"
            },
            ["presentation"] = new[] {
                @"presentation\s+(?:session|event)",
                @"giving\s+a\s+presentation",
                @"presentation\s+to",
                @"\bpresentations?\b"
            },
            ["training"] = new[] {
                @"training\s+(?:session|program|course)",
                @"conducting\s+training",
                @"training\s+event",
                @"\btraining\b"
            },
            ["ceremony"] = new[] {
                @"ceremony\s+event",
                @"award\s+ceremony",
                @"graduation\s+ceremony",
                @"\bceremon(y|ies)\b"
            },
            ["wedding"] = new[] {
                @"wedding\s+(?:ceremony|reception)",
                @"wedding\s+event",
                @"\bweddings?\b"
            },
            ["party"] = new[] {
                @"party\s+event",
                @"celebration\s+party",
                @"birthday\s+party",
                @"\bpart(y|ies)\b"
            },
            ["webinar"] = new[] {
                @"webinar\s+(?:session|event)",
                @"online\s+webinar",
                @"virtual\s+webinar",
                @"\bwebinars?\b"
            },
            ["interview"] = new[] {
                @"(?:conducting|doing|having)\s+an?\s+interview",
                @"interview\s+(?:with|for)",
                @"\binterview\b"
            },
            ["team offsite"] = new[] {
                @"team\s+offsite",
                @"company\s+offsite",
                @"staff\s+offsite",
                @"\boffsite\b"
            },
            ["town hall"] = new[] {
                @"town\s+hall",
                @"all[-\s]?hands"
            },
            ["gala dinner"] = new[] {
                @"gala\s+dinner",
                @"awards?\s+night"
            }
        };

        var directAnswerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meeting"] = "meeting",
            ["conference"] = "conference",
            ["seminar"] = "seminar",
            ["workshop"] = "workshop",
            ["presentation"] = "presentation",
            ["training"] = "training",
            ["wedding"] = "wedding",
            ["party"] = "party",
            ["webinar"] = "webinar",
            ["interview"] = "interview",
            ["interviews"] = "interviews",
            ["team offsite"] = "team offsite",
            ["offsite"] = "team offsite",
            ["town hall"] = "town hall",
            ["all hands"] = "town hall",
            ["gala dinner"] = "gala dinner"
        };

        // Check user messages first (more reliable than assistant responses)
        foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var rawText = JoinParts(m);
            var text = rawText.ToLowerInvariant();
            var normalized = Regex.Replace(text, @"[^\w\s-]", " ").Trim();
            normalized = Regex.Replace(normalized, @"\s+", " ");
            if (normalized.Length <= 40 && directAnswerMap.TryGetValue(normalized, out var directType))
            {
                return (directType, rawText.Trim());
            }

            foreach (var kvp in eventTypePatterns)
            {
                foreach (var pattern in kvp.Value)
                {
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        return (kvp.Key, Regex.Match(JoinParts(m), pattern, RegexOptions.IgnoreCase).Value);
                    }
                }
            }
        }

        // Also check assistant messages for confirmation
        foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m).ToLowerInvariant();

            // Look for confirmation patterns like "I see you mentioned interviews"
            foreach (var kvp in eventTypePatterns)
            {
                foreach (var pattern in kvp.Value)
                {
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        return (kvp.Key, Regex.Match(JoinParts(m), pattern, RegexOptions.IgnoreCase).Value);
                    }
                }
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Extract all event information from conversation messages
    /// </summary>
    public EventInformation ExtractAllEventInformation(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        var info = new EventInformation();

        // Combine all user messages for comprehensive extraction
        var userText = string.Join(" ", ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .Select(JoinParts));

        if (string.IsNullOrWhiteSpace(userText)) return info;

        userText = userText.ToLowerInvariant();

        // Extract budget
        var budgetMatch = Regex.Match(userText, @"(?:\$|budget\s+(?:of\s+)?)(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\s*(?:dollars?|budget)", RegexOptions.IgnoreCase);
        if (budgetMatch.Success && decimal.TryParse(budgetMatch.Groups[1].Value.Replace(",", ""), out var budget))
        {
            info.Budget = budget;
            info.BudgetMatch = budgetMatch.Value;
        }

        // Extract attendees count
        var attendeesMatch = Regex.Match(userText, @"(\d{1,4})\s+(?:people|attendees|pax|participants|guests)", RegexOptions.IgnoreCase);
        if (attendeesMatch.Success && int.TryParse(attendeesMatch.Groups[1].Value, out var attendees))
        {
            info.Attendees = attendees;
            info.AttendeesMatch = attendeesMatch.Value;
        }

        // Extract setup style
        var setupPatterns = new[] {
            @"(?:setup|style|layout)\s+(?:is|will\s+be|should\s+be)?\s+(?:a\s+)?(?:classroom|theater|boardroom|banquet|u-shape|u\s+shape|reception|cocktail|dinner)",
            @"(?:classroom|theater|boardroom|banquet|u-shape|u\s+shape|reception|cocktail|dinner)\s+(?:setup|style|layout)"
        };

        foreach (var pattern in setupPatterns)
        {
            var match = Regex.Match(userText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var setup = match.Value.ToLower()
                    .Replace("setup", "").Replace("style", "").Replace("layout", "")
                    .Replace("is", "").Replace("will be", "").Replace("should be", "")
                    .Replace("a", "").Trim();

                if (!string.IsNullOrWhiteSpace(setup))
                {
                    info.SetupStyle = setup;
                    info.SetupMatch = match.Value;
                    break;
                }
            }
        }

        // Extract venue/room information
        var venueMatch = Regex.Match(userText, @"(?:at|in)\s+(?:the\s+)?([A-Za-z\s]+?)\s+(?:room|venue|ballroom|hall)", RegexOptions.IgnoreCase);
        if (venueMatch.Success)
        {
            info.Venue = venueMatch.Groups[1].Value.Trim();
            info.VenueMatch = venueMatch.Value;
        }

        // Extract special requests - look for phrases after keywords
        var specialRequestPatterns = new[] {
            @"(?:also|additionally|plus|furthermore| moreover)\s+(.+?)(?:\s+(?:that's|that is|and that's|etc|etc\.|$))",
            @"(?:need|want|require)\s+(.+?)(?:\s+(?:as well|also|too|etc|etc\.|$))",
            @"special\s+requests?\s*(?:include|are|is)?\s*(.+?)(?:\s+(?:that's|that is|$))"
        };

        foreach (var pattern in specialRequestPatterns)
        {
            var match = Regex.Match(userText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var request = match.Groups[1].Value.Trim();
                if (request.Length > 5 && !string.IsNullOrWhiteSpace(request))
                {
                    info.SpecialRequests = request;
                    info.SpecialRequestsMatch = match.Value;
                    break;
                }
            }
        }

        // Extract all dates mentioned (for multi-day events)
        var dateMatches = Regex.Matches(userText, @"(\d{1,2})(st|nd|rd|th)?\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec|january|february|march|april|june|july|august|september|october|november|december)\s+(\d{4})", RegexOptions.IgnoreCase);
        if (dateMatches.Count > 0)
        {
            info.Dates = new List<string>();
            foreach (Match match in dateMatches)
            {
                info.Dates.Add(match.Value);
            }
        }

        return info;
    }

    /// <summary>
    /// Extract multi-day event details from conversation messages
    /// </summary>
    public MultiDayEventDetails? ExtractMultiDayEventDetails(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        var fullText = string.Join(" ", ordered.Select(JoinParts)).ToLowerInvariant();

        // Check if this is a multi-day event
        var dayCountMatch = Regex.Match(fullText, @"(\d+)\s+(?:day|days?)\s+(?:event|conference|meeting|seminar)", RegexOptions.IgnoreCase);
        if (!dayCountMatch.Success)
        {
            // Check for specific day references
            var dayRefs = Regex.Matches(fullText, @"\b(?:first|second|third|fourth|fifth|1st|2nd|3rd|4th|5th|day\s+\d+)\b", RegexOptions.IgnoreCase);
            if (dayRefs.Count < 2) return null; // Need at least 2 day references for multi-day
        }

        var details = new MultiDayEventDetails();

        // Extract start date
        var (eventDate, _) = ExtractEventDate(messages);
        if (eventDate.HasValue)
        {
            details.StartDate = eventDate.Value.DateTime;
        }

        // Determine duration
        var durationMatch = Regex.Match(fullText, @"(\d+)\s+(?:day|days?)\s+(?:event|conference|meeting|seminar)", RegexOptions.IgnoreCase);
        if (durationMatch.Success && int.TryParse(durationMatch.Groups[1].Value, out var days))
        {
            details.DurationDays = days;
        }
        else
        {
            // Count unique day references
            var dayRefs = Regex.Matches(fullText, @"\b(?:first|second|third|fourth|fifth|1st|2nd|3rd|4th|5th)\b", RegexOptions.IgnoreCase);
            details.DurationDays = Math.Max(dayRefs.Count, 1);
        }

        // Parse day-specific information
        var dayPatterns = new Dictionary<string, int>
        {
            ["first"] = 1, ["1st"] = 1, ["day 1"] = 1,
            ["second"] = 2, ["2nd"] = 2, ["day 2"] = 2,
            ["third"] = 3, ["3rd"] = 3, ["day 3"] = 3,
            ["fourth"] = 4, ["4th"] = 4, ["day 4"] = 4,
            ["fifth"] = 5, ["5th"] = 5, ["day 5"] = 5
        };

        foreach (var kvp in dayPatterns)
        {
            var dayNumber = kvp.Value;
            var dayDetails = new DayEventDetails
            {
                DayNumber = dayNumber,
                Date = details.StartDate.AddDays(dayNumber - 1)
            };

            // Extract setup style for this day
            var setupPattern = $@"(?:on|for|during)\s+(?:the\s+)?{Regex.Escape(kvp.Key)}\s+(?:day)?\s*(?:is|will\s+be|should\s+be)?\s*(?:a\s+)?(?:classroom|theater|boardroom|banquet|u-shape|u\s+shape|reception|cocktail|dinner)\s+(?:setup|style|layout)";
            var setupMatch = Regex.Match(fullText, setupPattern, RegexOptions.IgnoreCase);
            if (setupMatch.Success)
            {
                var setupText = setupMatch.Value.ToLower()
                    .Replace("on", "").Replace("for", "").Replace("during", "").Replace("the", "")
                    .Replace(kvp.Key, "").Replace("day", "").Replace("is", "").Replace("will be", "").Replace("should be", "")
                    .Replace("a", "").Replace("setup", "").Replace("style", "").Replace("layout", "").Trim();

                dayDetails.SetupStyle = setupText;
            }

            // Extract time information for this day
            var timePattern = $@"(?:on|for|during)\s+(?:the\s+)?{Regex.Escape(kvp.Key)}\s+(?:day)?\s*(?:starts?|begins?|from)\s+(\d{{1,2}}(?::\d{{2}})?\s*(?:AM|PM)?)";
            var timeMatch = Regex.Match(fullText, timePattern, RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                // Parse time - simplified for now
                dayDetails.StartTime = TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time) ? time : null;
            }

            // Extract special notes for this day
            var notesPattern = $@"(?:on|for|during)\s+(?:the\s+)?{Regex.Escape(kvp.Key)}\s+(?:day)?\s*(.+?)(?:\s+(?:on|for|during|day|$))";
            var notesMatch = Regex.Match(fullText, notesPattern, RegexOptions.IgnoreCase);
            if (notesMatch.Success)
            {
                var notes = notesMatch.Groups[1].Value.Trim();
                if (notes.Length > 10 && !notes.Contains("setup") && !notes.Contains("style"))
                {
                    dayDetails.SpecialNotes = notes;
                }
            }

            // Only add if we have some information for this day
            if (!string.IsNullOrWhiteSpace(dayDetails.SetupStyle) ||
                dayDetails.StartTime.HasValue ||
                !string.IsNullOrWhiteSpace(dayDetails.SpecialNotes))
            {
                details.SetDayDetails(dayNumber, dayDetails);
            }
        }

        // If we found day-specific information, return the details
        return details.Days.Count > 0 ? details : null;
    }

    /// <summary>
    /// Extract selected equipment from "Selected equipment: ProductName (PRODUCT_CODE)" messages
    /// </summary>
    public List<SelectedEquipmentItem> ExtractSelectedEquipment(IEnumerable<DisplayMessage> messages)
    {
        var items = new List<SelectedEquipmentItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: "Selected equipment: Description (PRODUCT_CODE)"
        var pattern = @"Selected equipment:\s*(.+?)\s*\(([A-Z0-9\-_]+)\s*\)";

        foreach (var msg in messages)
        {
            // Only check user messages (equipment selections come from button clicks)
            if (!string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.Join("\n", msg.Parts ?? Enumerable.Empty<string>());

            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                var description = m.Groups[1].Value.Trim();
                var productCode = m.Groups[2].Value.Trim();

                // Skip duplicates (same product code)
                if (seenCodes.Contains(productCode))
                    continue;

                seenCodes.Add(productCode);
                items.Add(new SelectedEquipmentItem
                {
                    ProductCode = productCode,
                    Description = description,
                    Quantity = 1 // Default to 1, can be enhanced to parse quantity
                });
            }
        }

        _logger.LogInformation("Extracted {Count} equipment items from conversation", items.Count);
        return items;
    }

    // ==================== PRIVATE HELPERS ====================


}
