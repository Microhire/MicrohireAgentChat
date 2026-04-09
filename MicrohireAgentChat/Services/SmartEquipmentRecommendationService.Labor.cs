using MicrohireAgentChat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class SmartEquipmentRecommendationService
{
    /// <summary>
    /// Apply audio pairing logic to auto-suggest speakers when:
    /// - Microphones are recommended (speakers needed to hear audio)
    /// - Video/media playback is involved (video with sound needs speakers)
    /// - Video conferencing (Teams/Zoom) needs speakers for remote participants
    /// </summary>
    private async Task ApplyAudioPairingLogicAsync(
        SmartEquipmentRecommendation result,
        EventContext context,
        CancellationToken ct)
    {
        // User explicitly chose not to have speakers via the base AV form — respect that choice.
        if (context.UserDeclinedAudio)
        {
            _logger.LogInformation("Skipping audio pairing: user declined audio in base AV form.");
            return;
        }

        var speakerStyle = ResolveSpeakerStylePreference(context);
        var prefersExternalSpeakers = speakerStyle is "external" or "portable";

        // Check if speakers/audio packages are already in the recommendation.
        // Match on SPEAKER category, known audio package codes, or description keywords.
        // Do NOT match on broad category "WSB" as it also catches vision/projection packages.
        var audioPackageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WSBELSAD", "WSBELAUD", "WBELAUD", "WSBALLAU", "WBFBCSS", "WBSBCSS",
            "ELEVCSS", "ELEVSCSS", "ELEVAVP", "ELEVSAVP", "THRVCSS", "THRVAVP", "WSBFPAUD", "WSBFPAVP"
        };
        var hasSpeakers = result.Items.Any(i =>
            (i.Category ?? "").Trim().Equals("SPEAKER", StringComparison.OrdinalIgnoreCase) ||
            audioPackageCodes.Contains((i.ProductCode ?? "").Trim()) ||
            (i.Description ?? "").ToLower().Contains("speaker") ||
            (i.Description ?? "").ToLower().Contains("ceiling speaker") ||
            (i.Description ?? "").ToLower().Contains("pa system"));

        if (hasSpeakers)
        {
            _logger.LogInformation("Speakers already included in recommendations - skipping auto-add");
            return;
        }

        // Check if microphones are in the recommendation
        var hasMicrophones = result.Items.Any(i =>
            (i.Category ?? "").Trim().Equals("W/MIC", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLower().Contains("microphone") ||
            (i.Description ?? "").ToLower().Contains("mic"));

        // Check if projector/screen is in the recommendation (indicates presentations with potential video)
        var hasProjectorOrScreen = result.Items.Any(i =>
            (i.Category ?? "").Trim().Equals("PROJECTR", StringComparison.OrdinalIgnoreCase) ||
            (i.Category ?? "").Trim().Equals("SCREEN", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLower().Contains("projector") ||
            (i.Description ?? "").ToLower().Contains("screen"));

        // Check if the event type typically needs audio output
        var eventTypeLower = (context.EventType ?? "").ToLower();
        var eventNeedsAudio = eventTypeLower.Contains("video") ||
                             eventTypeLower.Contains("conference") ||
                             eventTypeLower.Contains("teams") ||
                             eventTypeLower.Contains("zoom") ||
                             eventTypeLower.Contains("webinar") ||
                             eventTypeLower.Contains("hybrid") ||
                             eventTypeLower.Contains("broadcast") ||
                             eventTypeLower.Contains("screening") ||
                             eventTypeLower.Contains("movie") ||
                             eventTypeLower.Contains("film");

        // Check equipment requests for audio-related keywords
        var requestsNeedAudio = context.EquipmentRequests.Any(r =>
        {
            var type = (r.EquipmentType ?? "").ToLower();
            return type.Contains("video") ||
                   type.Contains("audio") ||
                   type.Contains("teams") ||
                   type.Contains("zoom") ||
                   type.Contains("camera");
        });

        // Determine if speakers should be auto-added
        bool shouldAddSpeakers = false;
        string addReason = "";

        if (hasMicrophones && context.ExpectedAttendees > 10)
        {
            // Microphones need speakers for the audience to hear
            shouldAddSpeakers = true;
            addReason = "Speakers recommended with microphones so attendees can hear presenters clearly";
        }
        else if (hasProjectorOrScreen && (eventNeedsAudio || requestsNeedAudio))
        {
            // Video presentations typically need audio
            shouldAddSpeakers = true;
            addReason = "Speakers recommended for video/media playback with audio";
        }
        else if (eventNeedsAudio)
        {
            // Event type suggests audio is needed
            shouldAddSpeakers = true;
            addReason = $"Speakers recommended for {context.EventType} event to hear remote participants";
        }

        if (shouldAddSpeakers)
        {
            _logger.LogInformation("Auto-adding speakers: {Reason}", addReason);

            if (!prefersExternalSpeakers)
            {
                // Prefer room-specific WSB audio packages when venue+room known.
                var roomSpeakers = await TryGetRoomSpecificPackagesAsync(
                    context.VenueName, context.RoomName, "audio", 1, context, ct);
                if (roomSpeakers.Count > 0)
                {
                    var speaker = roomSpeakers[0];
                    speaker.RecommendationReason = addReason;
                    result.Items.Add(speaker);
                    _logger.LogInformation("Added room-specific speaker: {Code} - {Desc}",
                        speaker.ProductCode, speaker.Description);
                    return;
                }
            }

            // Fall back to generic speaker recommendations
            var speakerRecommendations = await GetSingleEquipmentTypeAsync(
                "speaker", 1, null, null, context, ct, speakerStyle);

            if (speakerRecommendations.Count > 0)
            {
                var speaker = speakerRecommendations[0];
                speaker.RecommendationReason = addReason;
                result.Items.Add(speaker);
                _logger.LogInformation("Added speaker: {Code} - {Desc}",
                    speaker.ProductCode, speaker.Description);
            }
        }
    }

    private static string? ResolveSpeakerStylePreference(EventContext context)
    {
        var candidates = context.EquipmentRequests
            .Select(r => r.SpeakerStyle)
            .Append(context.SpeakerStylePreference);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = candidate.Trim().ToLowerInvariant();
            if (normalized.Contains("inbuilt") || normalized.Contains("built in") || normalized.Contains("ceiling"))
                return "inbuilt";
            if (normalized.Contains("external") || normalized.Contains("portable") || normalized.Contains("pa"))
                return "external";
        }

        return null;
    }

    /// <summary>
    /// Adds MIXER06 (6ch mixer) when more than 1 microphone is in the room.
    /// Per spec: "Note on microphones once there is more than 1 in this room we need to add a mixer. MIXER06 = 6ch so 6 microphones."
    /// Scales quantity: ceil(micCount / 6) so 7-12 mics get 2 units, etc.
    /// </summary>
    private async Task ApplyMicrophoneMixerLogicAsync(
        SmartEquipmentRecommendation result,
        EventContext context,
        CancellationToken ct)
    {
        var micCount = result.Items
            .Where(i => (i.Category ?? "").Trim().Equals("W/MIC", StringComparison.OrdinalIgnoreCase) ||
                        i.Description.ToLowerInvariant().Contains("microphone") ||
                        i.Description.ToLowerInvariant().Contains("mic "))
            .Sum(i => i.Quantity);

        if (micCount <= 1) return;

        var existingMixer = result.Items.FirstOrDefault(i =>
            (i.ProductCode ?? "").Equals("MIXER06", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLowerInvariant().Contains("mixer"));

        var mixerQty = (int)Math.Ceiling(micCount / 6.0);

        if (existingMixer != null)
        {
            if (existingMixer.Quantity < mixerQty)
            {
                _logger.LogInformation("Scaling existing MIXER06 from {Old} to {New} for {MicCount} microphones",
                    existingMixer.Quantity, mixerQty, micCount);
                existingMixer.Quantity = mixerQty;
                existingMixer.RecommendationReason = $"6-channel mixer required for {micCount} microphones ({mixerQty} unit(s))";
            }
            return;
        }

        var mixerItems = await RecommendProductByCodeAsync(
            "MIXER06", mixerQty,
            $"6-channel mixer required for {micCount} microphones ({mixerQty} unit(s))",
            context, ct);

        if (mixerItems.Count > 0)
        {
            result.Items.AddRange(mixerItems);
            _logger.LogInformation("Added {Qty} MIXER06 for {MicCount} microphones", mixerQty, micCount);
        }
    }

    /// <summary>
    /// Recommends technicians based on event complexity and equipment volume.
    /// Follows Microhire's Technician Allocation guidelines.
    /// </summary>
    private async Task RecommendLaborAsync(
        SmartEquipmentRecommendation result,
        EventContext context,
        CancellationToken ct)
    {
        _logger.LogInformation("Recommending labor for event: {EventType}, {Attendees} attendees", 
            context.EventType, context.ExpectedAttendees);

        // Count microphones from recommendations if not explicitly provided
        int micCount = context.NumberOfMicrophones;
        if (micCount == 0)
        {
            micCount = result.Items
                .Where(i => (i.Category ?? "").Trim().Equals("W/MIC", StringComparison.OrdinalIgnoreCase) || 
                            i.Description.ToLower().Contains("microphone") || 
                            i.Description.ToLower().Contains("mic"))
                .Sum(i => i.Quantity);
        }

        // Prefer hybrid room/package labor rules for Westin scenarios.
        if (await TryApplyWestinLaborRulesAsync(result, context, micCount, ct))
        {
            return;
        }

        bool isLargeEvent = context.ExpectedAttendees > 100 || context.DurationDays > 1;
        bool hasVision = result.Items.Any(i => (i.Category ?? "").Trim().Equals("PROJECTR", StringComparison.OrdinalIgnoreCase) || 
                                              (i.Category ?? "").Trim().Equals("SCREEN", StringComparison.OrdinalIgnoreCase) ||
                                              i.Description.ToLower().Contains("led wall"));
        bool hasAudio = result.Items.Any(i =>
            (i.Category ?? "").Trim().Equals("W/MIC", StringComparison.OrdinalIgnoreCase) ||
            (i.Category ?? "").Trim().Equals("SPEAKER", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLower().Contains("microphone") ||
            (i.Description ?? "").ToLower().Contains("speaker") ||
            (i.Description ?? "").ToLower().Contains("audio") ||
            (i.Description ?? "").ToLower().Contains("mixer"));
        bool hasLighting = result.Items.Any(i => (i.Category ?? "").Trim().Equals("LED", StringComparison.OrdinalIgnoreCase) || 
                                                 i.Description.ToLower().Contains("light"));
        bool isAudioOnly = hasAudio &&
                           !hasVision &&
                           !hasLighting &&
                           !context.NeedsStreaming &&
                           !context.NeedsRecording &&
                           !context.NeedsHeavyStreaming;
        var operationProfile = await BuildOperationProfileAsync(result.Items, context, ct);
        bool complexityOverride = context.IsContentHeavy ||
                                  context.NeedsAdvancedLighting ||
                                  context.NeedsHeavyStreaming ||
                                  (context.NeedsStreaming && context.NeedsRecording) ||
                                  micCount > 0 ||
                                  context.NumberOfPresentations > 1 ||
                                  isLargeEvent;
        bool shouldBypassDefaultLabor = operationProfile.IsSelfOperatedOnly && !complexityOverride;

        // 1. TECHNICIAN BUNDLES & ALLOCATION
        if (micCount >= 1 && micCount <= 4)
        {
            if (context.IsContentHeavy)
            {
                AddLabor(result, "Senior AV Technician", "Bundle: 1-4 Microphones + Content-Heavy", productCode: "SAVTECH", task: "Operate");
            }
            else if (context.IsContentLight || context.ExpectedAttendees <= 50)
            {
                AddLabor(result, "Audio Technician", "Bundle: 1-4 Microphones + Content-Light/Small Event", productCode: "AXTECH", task: "Operate");
            }
            else
            {
                AddLabor(result, "AV Technician", "Standard support for 1-4 microphones", productCode: "AVTECH", task: "Operate");
            }
        }
        else if (micCount > 4 && micCount <= 10)
        {
            if (context.IsContentHeavy)
            {
                AddLabor(result, "Senior Audio Technician", "Bundle: 4-10 Microphones + Content-Heavy (Audio focus)", productCode: "AXTECH", task: "Operate");
                AddLabor(result, "Senior Vision Technician", "Bundle: 4-10 Microphones + Content-Heavy (Vision focus)", productCode: "VXTECH", task: "Operate");
            }
            else
            {
                AddLabor(result, "Senior AV Technician", "Bundle: 4-10 Microphones + Content-Light", productCode: "SAVTECH", task: "Operate");
            }
        }
        else if (micCount > 10)
        {
            AddLabor(result, "Senior Audio Technician", "High microphone count (>10) requires dedicated senior audio support", productCode: "AXTECH", task: "Operate");
        }

        // 2. VISION / CONTENT SPECIFIC
        if (context.IsContentHeavy && !result.LaborItems.Any(l => l.Description.Contains("Vision")))
        {
            AddLabor(result, "Senior Vision Technician", "Content-heavy event with multiple presenters/sources requires senior vision support", productCode: "VXTECH", task: "Operate");
        }
        else if (hasVision && !result.LaborItems.Any())
        {
            // For simple vision setup in small rooms
            AddLabor(result, "AV Technician", "Standard AV support for vision/presentations", productCode: "AVTECH", task: "Operate");
        }

        // 3. LIGHTING SPECIFIC
        if (context.NeedsAdvancedLighting)
        {
            AddLabor(result, "Senior Lighting Technician", "Advanced or heavy effect lighting requires senior lighting specialist", productCode: "LXTECH", task: "Operate");
        }
        else if (context.NeedsLighting && !result.LaborItems.Any())
        {
            // If only lighting is requested, ensure someone is there
            AddLabor(result, "AV Technician", "Standard support for stage wash/basic lighting", productCode: "AVTECH", task: "Operate");
        }

        // 4. STREAMING & RECORDING
        if (context.NeedsHeavyStreaming || (context.NeedsStreaming && context.NeedsRecording))
        {
            AddLabor(result, "Senior Streaming Technician", "Heavy streaming with recording and multiple presenters requires senior specialist", productCode: "VXTECH", task: "Operate");
        }
        else if (context.NeedsStreaming)
        {
            AddLabor(result, "Senior Streaming Technician", "Live streaming/virtual meeting support", productCode: "VXTECH", task: "Operate");
        }

        // 5. FULL PRODUCTION / LARGE SCALE
        if (isLargeEvent && result.LaborItems.Count >= 3)
        {
            AddLabor(result, "Technical Director", "Large scale production with multiple specialists requires a Technical Director for coordination", productCode: "AVTECH", task: "Operate");
        }

        // 6. Enforce operator-required equipment coverage
        if (operationProfile.HasRequired && result.LaborItems.Count == 0)
        {
            AddLabor(result, "AV Technician", "Operator-required equipment selected; qualified technician support is mandatory.", productCode: "AVTECH", task: "Operate");
        }

        // 7. DEFAULT FALLBACK
        if (!shouldBypassDefaultLabor && result.LaborItems.Count == 0 && (result.Items.Count > 0 || context.ExpectedAttendees > 20))
        {
            // Small events still need someone for Test & Connect
            string techType = isAudioOnly
                ? "Audio Technician"
                : (isLargeEvent ? "Senior AV Technician" : "AV Technician");
            var code = isAudioOnly
                ? "AXTECH"
                : (isLargeEvent ? "SAVTECH" : "AVTECH");
            AddLabor(result, techType, "Standard technician allocation for setup and support", productCode: code, task: "Operate");
        }
        else if (shouldBypassDefaultLabor)
        {
            _logger.LogInformation("Skipping default labor allocation because selected equipment is self-operated and no complexity override is present.");
        }

        // AVTECH labour rules: append setup time notes when specific equipment is present
        var hasSwitcher = result.Items.Any(i => (i.ProductCode ?? "").Equals("V1HD", StringComparison.OrdinalIgnoreCase));
        var hasMixer06 = result.Items.Any(i => (i.ProductCode ?? "").Equals("MIXER06", StringComparison.OrdinalIgnoreCase));
        var flipchartCount = result.Items.Sum(i =>
            (i.ProductCode ?? "").Equals("NATFLIPC", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLowerInvariant().Contains("flipchart")
                ? i.Quantity : 0);
        var hasVideoConf = result.Items.Any(i =>
            (i.ProductCode ?? "").Equals("LOG4kCAM", StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? "").ToLowerInvariant().Contains("video conference"));
        var hasLectern = result.Items.Any(i => (i.Description ?? "").ToLowerInvariant().Contains("lectern"));

        // TODO: This has the same early-clear bug as the Westin path (fixed in TryApplyWestinLaborRulesAsync).
        // It clears all accumulated labor and replaces with a single Operate item, losing Setup/T&C/Packdown times.
        // Fix this for non-Westin venues once the Westin flow is confirmed working.
        if (hasSwitcher && hasMixer06)
        {
            result.LaborItems.Clear();
            AddLabor(
                result,
                "AV Technician",
                "MIXER06 with V1HD requires one AV Technician for full event duration (rehearsal, operation, and pack down).",
                productCode: "AVTECH",
                task: "Operate");
        }

        if (result.LaborItems.Count > 0)
        {
            var extraNotes = new List<string>();
            if (hasSwitcher) extraNotes.Add("V1HD switcher: +1 hr setup; Test & Connect 30 mins; Operator from show start to end");
            if (flipchartCount > 0) extraNotes.Add($"Flipchart: +15 mins setup per unit ({flipchartCount} unit(s))");
            if (hasVideoConf) extraNotes.Add("Video conference unit: +15 mins setup, 15 mins T&C, 15 mins pack down");
            if (hasLectern) extraNotes.Add("Lectern: +15 mins setup");

            if (extraNotes.Count > 0)
            {
                var firstLabor = result.LaborItems[0];
                firstLabor.RecommendationReason += ". " + string.Join("; ", extraNotes);
            }
        }

        await Task.CompletedTask;
    }

    private async Task<bool> TryApplyWestinLaborRulesAsync(
        SmartEquipmentRecommendation result,
        EventContext context,
        int micCount,
        CancellationToken ct)
    {
        var venue = context.VenueName ?? string.Empty;
        if (!venue.Contains("Westin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rules = await LoadWestinLaborRulesAsync(ct);
        if (rules?.Rooms == null || rules.Rooms.Count == 0)
        {
            return false;
        }

        var roomNorm = (context.RoomName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(roomNorm))
        {
            return false;
        }

        var roomRule = rules.Rooms.FirstOrDefault(r =>
            r.RoomContains != null && r.RoomContains.Any(token => roomNorm.Contains(token, StringComparison.OrdinalIgnoreCase)));
        if (roomRule == null)
        {
            return false;
        }

        var selectedCodes = result.Items
            .Select(i => (i.ProductCode ?? string.Empty).Trim().ToUpperInvariant())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasMatchingPackage = roomRule.PackageCodes != null &&
                                 roomRule.PackageCodes.Any(code => selectedCodes.Contains(code.Trim().ToUpperInvariant()));
        if (!hasMatchingPackage)
        {
            return false;
        }

        var special = rules.SpecialRules ?? new WestinLaborSpecialRules();
        var switcherCode = (special.SwitcherCode ?? "V1HD").Trim();
        var mixerCode = (special.MixerCode ?? "MIXER06").Trim();
        var flipchartCode = (special.FlipchartCode ?? "NATFLIPC").Trim();
        var videoConferenceCode = (special.VideoConferenceCode ?? "LOG4kCAM").Trim();

        var hasSwitcher = selectedCodes.Contains(switcherCode);
        var hasMixer06 = selectedCodes.Contains(mixerCode);
        var flipchartCount = result.Items.Sum(i =>
            string.Equals((i.ProductCode ?? string.Empty).Trim(), flipchartCode, StringComparison.OrdinalIgnoreCase) ||
            (i.Description ?? string.Empty).Contains("flipchart", StringComparison.OrdinalIgnoreCase)
                ? i.Quantity
                : 0);
        var hasVideoConf = selectedCodes.Contains(videoConferenceCode) ||
                           result.Items.Any(i => (i.Description ?? string.Empty).Contains("video conference", StringComparison.OrdinalIgnoreCase));
        var hasLectern = result.Items.Any(i =>
            (special.LecternCodes ?? new List<string>()).Any(code =>
                string.Equals((i.ProductCode ?? string.Empty).Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase))) ||
            result.Items.Any(i => (i.Description ?? string.Empty).Contains("lectern", StringComparison.OrdinalIgnoreCase));

        // V1HD + MIXER06 combo: use AVTECH for all labour (no specialist escalation).
        var isV1hdMixerCombo = hasSwitcher && hasMixer06;

        var baselineCode = string.IsNullOrWhiteSpace(roomRule.BaselineLaborCode)
            ? "AVTECH"
            : roomRule.BaselineLaborCode.Trim().ToUpperInvariant();
        var baselineDescription = ResolveLaborDescription(baselineCode);

        var baseSetup = roomRule.BaselineSetupMinutes;
        var baseTc = roomRule.BaselineTcMinutes;
        var basePd = roomRule.BaselinePackdownMinutes;

        if (baseSetup > 0)
            AddLabor(result, baselineDescription, "Room package baseline labor: setup.", productCode: baselineCode, task: "Setup", minutes: baseSetup);
        if (baseTc > 0)
            AddLabor(result, baselineDescription, "Room package baseline labor: test and connect.", productCode: baselineCode, task: "Test & Connect", minutes: baseTc);
        if (basePd > 0)
            AddLabor(result, baselineDescription, "Room package baseline labor: pack down.", productCode: baselineCode, task: "Packdown", minutes: basePd);

        // Determine mic operator escalation threshold
        var micThreshold = roomRule.MicrophoneOperatorThreshold <= 0 ? 2 : roomRule.MicrophoneOperatorThreshold;
        var needsMicOperator = micCount > micThreshold;

        if (hasSwitcher && roomRule.SupportsOperatorEscalation)
        {
            // V1HD: +1hr AVTECH Setup
            AddLabor(result, baselineDescription, "V1HD switcher requires additional setup.", productCode: baselineCode, task: "Setup", minutes: 60);

            if (isV1hdMixerCombo)
            {
                // MIXER06 + V1HD: one AVTECH covers the full event — no specialist escalation.
                AddLabor(result, baselineDescription, "MIXER06 with V1HD requires one AV Technician for full event duration.", productCode: baselineCode, task: "Operate");
            }
            else if (needsMicOperator)
            {
                // Both mic operator AND switcher → use AVTECH for Rehearsal + Operate
                AddLabor(result, baselineDescription, "V1HD switcher with microphone operator: AVTECH rehearsal.", productCode: baselineCode, task: "Rehearsal", minutes: 30);
                AddLabor(result, baselineDescription, "V1HD switcher with microphone operator: AVTECH operates from show start to end.", productCode: baselineCode, task: "Operate");
            }
            else
            {
                // V1HD only → VXTECH Rehearsal + Operate
                var visionCode = string.IsNullOrWhiteSpace(roomRule.VisionSpecialistCode) ? "VXTECH" : roomRule.VisionSpecialistCode.Trim().ToUpperInvariant();
                var visionDesc = ResolveLaborDescription(visionCode);
                AddLabor(result, visionDesc, "V1HD switcher requires VXTECH rehearsal.", productCode: visionCode, task: "Rehearsal", minutes: 30);
                AddLabor(result, visionDesc, "V1HD switcher requires specialist operation from show start to end.", productCode: visionCode, task: "Operate");
            }
        }

        if (flipchartCount > 0)
        {
            AddLabor(result, baselineDescription, $"Flipchart setup (+15 mins per unit x {flipchartCount}).", productCode: baselineCode, task: "Setup", minutes: 15 * flipchartCount);
        }

        if (hasVideoConf)
        {
            AddLabor(result, baselineDescription, "Video conference unit requires additional setup.", productCode: baselineCode, task: "Setup", minutes: 30);
            AddLabor(result, baselineDescription, "Video conference unit requires additional test and connect.", productCode: baselineCode, task: "Test & Connect", minutes: 30);
            AddLabor(result, baselineDescription, "Video conference unit requires additional pack down.", productCode: baselineCode, task: "Packdown", minutes: 30);
        }

        if (hasLectern)
        {
            AddLabor(result, baselineDescription, "Lectern/microphone requires additional setup.", productCode: baselineCode, task: "Setup", minutes: 15);
            AddLabor(result, baselineDescription, "Lectern/microphone requires additional pack down.", productCode: baselineCode, task: "Packdown", minutes: 15);
        }

        // Wireless microphones: +15 mins setup & pack down per spec
        if (micCount > 0)
        {
            AddLabor(result, baselineDescription, "Wireless microphone(s) require additional setup.", productCode: baselineCode, task: "Setup", minutes: 15);
            AddLabor(result, baselineDescription, "Wireless microphone(s) require additional pack down.", productCode: baselineCode, task: "Packdown", minutes: 15);
        }

        if (roomRule.SupportsOperatorEscalation)
        {
            // Mic operator escalation — only when switcher is NOT also present (combo handled above)
            if (needsMicOperator && !hasSwitcher)
            {
                var baselineTcForMics = result.LaborItems.FirstOrDefault(l =>
                    string.Equals(l.ProductCode, baselineCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(l.Task, "Test & Connect", StringComparison.OrdinalIgnoreCase));
                if (baselineTcForMics != null)
                    result.LaborItems.Remove(baselineTcForMics);

                var audioCode = string.IsNullOrWhiteSpace(roomRule.AudioSpecialistCode) ? "AXTECH" : roomRule.AudioSpecialistCode.Trim().ToUpperInvariant();
                var audioDesc = ResolveLaborDescription(audioCode);
                AddLabor(result, audioDesc, "More than 2 microphones: AVTECH T&C replaced by AXTECH rehearsal.", productCode: audioCode, task: "Rehearsal", minutes: 30);
                AddLabor(result, audioDesc, "More than 2 microphones require audio operator from show start to end.", productCode: audioCode, task: "Operate");
            }

            var hasOperator = result.LaborItems.Any(l =>
                string.Equals(l.Task, "Operate", StringComparison.OrdinalIgnoreCase));

            if (!hasOperator && (context.IsContentHeavy || context.NumberOfPresentations > 1))
            {
                var visionCode = string.IsNullOrWhiteSpace(roomRule.VisionSpecialistCode) ? "VXTECH" : roomRule.VisionSpecialistCode.Trim().ToUpperInvariant();
                var visionDesc = ResolveLaborDescription(visionCode);
                AddLabor(result, visionDesc, "Multiple presenters or content-heavy event requires operator from show start to end.", productCode: visionCode, task: "Operate");
            }

            if (context.NeedsHeavyStreaming || (context.NeedsStreaming && context.NeedsRecording))
            {
                if (!result.LaborItems.Any(l => l.ProductCode == "VXTECH" && string.Equals(l.Task, "Operate", StringComparison.OrdinalIgnoreCase)))
                {
                    var visionCode = string.IsNullOrWhiteSpace(roomRule.VisionSpecialistCode) ? "VXTECH" : roomRule.VisionSpecialistCode.Trim().ToUpperInvariant();
                    var visionDesc = ResolveLaborDescription(visionCode);
                    AddLabor(result, visionDesc, "Streaming with recording requires specialist operator.", productCode: visionCode, task: "Operate");
                }
            }
            else if (context.NeedsStreaming)
            {
                if (!result.LaborItems.Any(l => string.Equals(l.Task, "Operate", StringComparison.OrdinalIgnoreCase)))
                {
                    AddLabor(result, baselineDescription, "Live streaming requires operator support.", productCode: baselineCode, task: "Operate");
                }
            }
        }

        if (context.NeedsAdvancedLighting)
        {
            AddLabor(result, ResolveLaborDescription("LXTECH"), "Advanced lighting requires specialist operator.", productCode: "LXTECH", task: "Operate");
        }

        return result.LaborItems.Count > 0;
    }

    private async Task<WestinLaborRulesConfig?> LoadWestinLaborRulesAsync(CancellationToken ct)
    {
        var path = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "westin-labor-rules.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<WestinLaborRulesConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load westin-labor-rules.json");
            return null;
        }
    }

    private static string ResolveLaborDescription(string productCode)
    {
        return productCode.Trim().ToUpperInvariant() switch
        {
            "AXTECH" => "Audio Technician",
            "VXTECH" => "Vision Technician",
            "LXTECH" => "Lighting Technician",
            "SAVTECH" => "Senior AV Technician",
            _ => "AV Technician"
        };
    }

    /// <summary>
    /// Applies room-specific suggestions and commentary based on the Microhire UAT feedback.
    /// These suggestions are appended to the RecommendationReason of the equipment items
    /// so they appear in the Isla chat quote summary.
    /// </summary>

    private void AddLabor(
        SmartEquipmentRecommendation result,
        string description,
        string reason,
        string productCode = "AVTECH",
        string task = "Operate",
        int quantity = 1,
        int minutes = 0,
        double hours = 0)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(productCode) ? "AVTECH" : productCode.Trim().ToUpperInvariant();
        var normalizedTask = string.IsNullOrWhiteSpace(task) ? "Operate" : task.Trim();

        var existing = result.LaborItems.FirstOrDefault(l =>
            string.Equals(l.ProductCode, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Task, normalizedTask, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Description, description, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Quantity = Math.Max(existing.Quantity, quantity);
            existing.Hours += hours;
            existing.Minutes += minutes;
            if (!string.IsNullOrWhiteSpace(reason) &&
                !existing.RecommendationReason.Contains(reason, StringComparison.OrdinalIgnoreCase))
            {
                existing.RecommendationReason = string.IsNullOrWhiteSpace(existing.RecommendationReason)
                    ? reason
                    : $"{existing.RecommendationReason}; {reason}";
            }

            return;
        }

        result.LaborItems.Add(new RecommendedLaborItem
        {
            ProductCode = normalizedCode,
            Description = description,
            Task = normalizedTask,
            Quantity = quantity,
            Hours = hours,
            Minutes = minutes,
            RecommendationReason = reason
        });
    }

}
