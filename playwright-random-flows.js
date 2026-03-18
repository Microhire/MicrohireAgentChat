const { chromium } = require("playwright");
const fs = require("fs");
const path = require("path");

const DEFAULT_SCHEDULE = {
  setup: "07:00",
  rehearsal: "09:30",
  start: "10:00",
  end: "16:00",
  packup: "17:00",
};

function parseArgs(argv) {
  const args = {
    runs: 4,
    headless: false,
    baseUrl: "http://localhost:5216",
    maxTurns: 24,
    outDir: "playwright-random-results",
    assistantTimeoutMs: 45000,
    profile: "mixed",
    parallel: 1,
  };

  for (let i = 0; i < argv.length; i++) {
    const part = argv[i];
    const next = argv[i + 1];
    if (part === "--runs" && next) args.runs = Number(next);
    if (part === "--base-url" && next) args.baseUrl = next;
    if (part === "--max-turns" && next) args.maxTurns = Number(next);
    if (part === "--out-dir" && next) args.outDir = next;
    if (part === "--assistant-timeout-ms" && next) args.assistantTimeoutMs = Number(next);
    if (part === "--profile" && next) args.profile = next;
    if (part === "--parallel" && next) args.parallel = Number(next);
    if (part === "--headed") args.headless = false;
    if (part === "--headless") args.headless = true;
  }

  args.runs = Number.isFinite(args.runs) && args.runs > 0 ? Math.floor(args.runs) : 1;
  args.maxTurns = Number.isFinite(args.maxTurns) && args.maxTurns > 0 ? Math.floor(args.maxTurns) : 24;
  args.assistantTimeoutMs = Number.isFinite(args.assistantTimeoutMs)
    ? Math.floor(args.assistantTimeoutMs)
    : 45000;
  args.parallel = Number.isFinite(args.parallel) && args.parallel > 0 ? Math.floor(args.parallel) : 1;

  return args;
}

function nowIso() {
  return new Date().toISOString();
}

function hasFiniteTimeout(timeoutMs) {
  return Number.isFinite(timeoutMs) && timeoutMs > 0;
}

function timestampSlug() {
  return nowIso().replace(/[:.]/g, "-");
}

function normalize(text) {
  return (text || "").toLowerCase().replace(/\s+/g, " ").trim();
}

function slugify(text) {
  return (text || "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 50) || "scenario";
}

function ensureDir(dirPath) {
  if (!fs.existsSync(dirPath)) fs.mkdirSync(dirPath, { recursive: true });
}

function appendNdjson(filePath, entry) {
  fs.appendFileSync(filePath, `${JSON.stringify(entry)}\n`, "utf8");
}

function debugLog(runId, message) {
  console.log(`[${runId}] ${message}`);
}

function buildScenarioProfiles() {
  return {
    thriveHybridFullCoverage: {
      id: "thriveHybridFullCoverage",
      description: "Thrive Boardroom hybrid meeting with full technician coverage",
      fullName: "Daniel Morel",
      status: "new",
      organization: "Morel Enterprises",
      location: "Brisbane",
      contact: "daniel@morel.co",
      eventType: "meeting",
      room: "thrive",
      date: "7 April 2026",
      attendees: "8",
      setupStyle: null,
      schedule: { ...DEFAULT_SCHEDULE },
      equipmentQueue: [
        "Yes a couple of laptops and we will be sharing presentations on screen with attendees in the room and others globally via Zoom and Teams",
        "Yes a clicker would be good",
        "A flipchart would be good too",
        "2 laptops, mac",
        "yes",
      ],
      laptopOwnershipReply: "We need Microhire to provide the laptops",
      laptopPreferenceReply: "2 laptops, mac",
      adaptorReply: "No adaptor needed",
      videoPackageReply: "yes please include it",
      technicianCoverageReply: "all stages",
      confirmAllReply: "yes this is all",
      expected: {
        requiresQuoteSummary: true,
        requiresTechnicianPrompt: true,
        requiresTechnicianSupport: true,
        stageLabels: ["Setup", "Rehearsal / Test & Connect", "Operate", "Pack down"],
        durationFragments: ["1h", "45m", "6h"],
        forbiddenAssistantPatterns: [
          "setup style",
          "select a layout",
          "choose a setup",
          "inbuilt speaker system",
          "external/portable pa speakers",
          "foldback monitor",
          "lectern",
        ],
      },
    },
    thriveHybridSelectiveCoverage: {
      id: "thriveHybridSelectiveCoverage",
      description: "Thrive Boardroom hybrid meeting with selective technician coverage",
      fullName: "Sophie Bennett",
      status: "existing",
      organization: "Northstar Labs",
      location: "Brisbane",
      contact: "sophie.bennett@northstar.io",
      eventType: "meeting",
      room: "Thrive Boardroom please",
      date: "9 April 2026",
      attendees: "10",
      setupStyle: null,
      schedule: { ...DEFAULT_SCHEDULE },
      equipmentQueue: [
        "Need two laptops, presentation screen, and hybrid meeting for Zoom and Teams",
        "A clicker would be great",
        "Please add a flipchart as well",
        "2 laptops, windows",
        "yes",
      ],
      laptopOwnershipReply: "We need Microhire to provide the laptops",
      laptopPreferenceReply: "2 laptops, windows",
      adaptorReply: "No adaptor needed",
      videoPackageReply: "yes include video package",
      technicianCoverageReply: "setup and operate",
      confirmAllReply: "yes this is all",
      expected: {
        requiresQuoteSummary: true,
        requiresTechnicianPrompt: true,
        requiresTechnicianSupport: true,
        stageLabels: ["Setup", "Operate"],
        forbiddenStageLabels: ["Rehearsal / Test & Connect", "Pack down"],
        durationFragments: ["1h", "6h"],
        forbiddenAssistantPatterns: [
          "setup style",
          "select a layout",
          "choose a setup",
          "inbuilt speaker system",
          "external/portable pa speakers",
        ],
      },
    },
    thriveSelfOperated: {
      id: "thriveSelfOperated",
      description: "Thrive Boardroom self-operated meeting without technician support",
      fullName: "Liam Parker",
      status: "new",
      organization: "Pacific Advisory",
      location: "Brisbane",
      contact: "liam.parker@example.com",
      eventType: "meeting",
      room: "thrive room",
      date: "12 April 2026",
      attendees: "6",
      setupStyle: null,
      schedule: { ...DEFAULT_SCHEDULE },
      equipmentQueue: [
        "We'll bring our own laptop and just need a clicker plus a USBC adaptor for the screen in the room",
        "yes that's all",
      ],
      laptopOwnershipReply: "We'll bring our own laptop",
      laptopPreferenceReply: null,
      adaptorReply: "Yes please add a USBC adaptor",
      videoPackageReply: "no not needed for now",
      technicianCoverageReply: "no technician needed",
      confirmAllReply: "yes that's all",
      expected: {
        requiresQuoteSummary: true,
        requiresTechnicianPrompt: false,
        requiresTechnicianSupport: false,
        forbiddenAssistantPatterns: [
          "setup style",
          "inbuilt speaker system",
          "external/portable pa speakers",
        ],
      },
    },
    elevateAudioVision: {
      id: "elevateAudioVision",
      description: "Elevate room package scenario with audio/vision labour",
      fullName: "Emma Nguyen",
      status: "existing",
      organization: "Atlas Health",
      location: "Brisbane",
      contact: "emma.nguyen@atlas.au",
      eventType: "presentation",
      room: "Elevate 1",
      date: "14 April 2026",
      attendees: "24",
      setupStyle: "theatre",
      schedule: { ...DEFAULT_SCHEDULE, setup: "08:00", rehearsal: "08:30", start: "09:00", end: "17:00", packup: "18:00" },
      equipmentQueue: [
        "We need to present slides with audio playback to attendees in the room and remote attendees on Zoom",
        "Yes include a clicker",
        "1 laptop, mac",
        "yes",
      ],
      laptopOwnershipReply: "We need Microhire to provide the laptop",
      laptopPreferenceReply: "1 laptop, mac",
      adaptorReply: "No adaptor needed",
      videoPackageReply: "yes please include it",
      speakerStyleReply: "external portable PA speakers",
      technicianCoverageReply: "all stages",
      confirmAllReply: "yes this is all",
      expected: {
        requiresQuoteSummary: true,
        requiresTechnicianPrompt: true,
        requiresTechnicianSupport: true,
        stageLabels: ["Setup", "Operate"],
        durationFragments: ["1h", "8h"],
      },
    },
  };
}

function resolveProfiles(profileArg, runs) {
  const profiles = buildScenarioProfiles();
  const allProfiles = Object.values(profiles);
  const profileIds = Object.keys(profiles);

  if (!profileArg || profileArg === "mixed") {
    return Array.from({ length: runs }, (_, idx) => allProfiles[idx % allProfiles.length]);
  }

  if (profileArg === "all") {
    return Array.from({ length: runs }, (_, idx) => allProfiles[idx % allProfiles.length]);
  }

  const requested = profileArg.split(",").map((part) => part.trim()).filter(Boolean);
  const selected = requested.map((id) => profiles[id]).filter(Boolean);
  if (selected.length === 0) {
    throw new Error(`Unknown profile '${profileArg}'. Available profiles: ${profileIds.join(", ")}`);
  }

  return Array.from({ length: runs }, (_, idx) => selected[idx % selected.length]);
}

async function getConversationState(page) {
  return page.evaluate(() => {
    const nodes = Array.from(document.querySelectorAll("#messagesWrap .chat-message[data-role]"));
    const messages = nodes.map((node, idx) => {
      const role = (node.getAttribute("data-role") || "").trim().toLowerCase();
      const textNode = node.querySelector(".chat-message-body");
      const text = (textNode ? textNode.innerText : node.innerText || "").trim();
      return { index: idx, role, text };
    });

    const assistants = messages.filter((m) => m.role === "assistant");
    const sendBtn = document.getElementById("sendBtn");
    const textInput = document.getElementById("textInput");
    const visibleTimePickers = Array.from(document.querySelectorAll("#messagesWrap .isla-multitime"))
      .filter((el) => {
        const style = window.getComputedStyle(el);
        return style.display !== "none" && style.visibility !== "hidden" && el.offsetParent !== null;
      })
      .length;

    return {
      messages,
      assistantCount: assistants.length,
      latestAssistant: assistants.length > 0 ? assistants[assistants.length - 1] : null,
      sendEnabled: !!sendBtn && !sendBtn.disabled,
      inputEnabled: !!textInput && !textInput.disabled,
      typingVisible: document.querySelectorAll(".typing-bubble").length > 0,
      visibleTimePickers,
    };
  });
}

async function waitForComposerReady(page, timeoutMs) {
  const start = Date.now();
  while (true) {
    const ready = await page.evaluate(() => {
      const sendBtn = document.getElementById("sendBtn");
      const textInput = document.getElementById("textInput");
      return !!sendBtn && !sendBtn.disabled && !!textInput && !textInput.disabled;
    });

    if (ready) return;
    if (hasFiniteTimeout(timeoutMs) && Date.now() - start >= timeoutMs) {
      throw new Error(`Timed out waiting for composer to become ready after ${timeoutMs}ms`);
    }

    await page.waitForTimeout(250);
  }
}

async function waitForAssistantTurnComplete(page, previousSnapshot, timeoutMs) {
  const start = Date.now();
  let stableSince = 0;
  let lastSignature = "";
  const previousAssistantCount = previousSnapshot?.assistantCount ?? 0;
  const previousAssistantText = previousSnapshot?.latestAssistantText ?? "";

  while (true) {
    const state = await getConversationState(page);
    const latestText = state.latestAssistant?.text || "";
    const signature = `${state.assistantCount}::${latestText}`;
    const assistantAdvanced =
      state.assistantCount > previousAssistantCount ||
      (state.assistantCount === previousAssistantCount && latestText && latestText !== previousAssistantText);

    if (assistantAdvanced) {
      if (signature !== lastSignature) {
        lastSignature = signature;
        stableSince = Date.now();
      }

      if (
        stableSince > 0 &&
        Date.now() - stableSince >= 900 &&
        state.sendEnabled &&
        state.inputEnabled
      ) {
        return {
          assistantCount: state.assistantCount,
          latestAssistant: state.latestAssistant,
          messages: state.messages,
        };
      }
    }

    await page.waitForTimeout(250);
    if (hasFiniteTimeout(timeoutMs) && Date.now() - start >= timeoutMs) {
      throw new Error(`Timed out waiting for assistant response after ${timeoutMs}ms`);
    }
  }
}

async function sendUserMessage(page, text, runLog, assistantTimeoutMs) {
  debugLog(runLog.runId, `USER -> ${text}`);
  await waitForComposerReady(page, assistantTimeoutMs);
  await page.fill("#textInput", text);
  try {
    await page.click("#sendBtn", { timeout: 10000 });
  } catch {
    await page.locator("#textInput").press("Enter");
  }
  runLog.transcript.push({ ts: nowIso(), role: "user", text });
}

function isTimePickerPromptText(text) {
  const normalized = normalize(text);
  return normalized.includes("confirm your schedule")
    || normalized.includes("here's a time picker")
    || normalized.includes("here’s a time picker")
    || normalized.includes("choose schedule:");
}

async function maybeSubmitTimePicker(page, schedule, runLog, state, previousSnapshot, assistantTimeoutMs) {
  if (state.timePickerSubmitted) return null;
  if (!isTimePickerPromptText(previousSnapshot?.latestAssistantText || "")) return null;

  const wrap = page.locator("#messagesWrap .isla-multitime").last();
  if ((await wrap.count()) === 0 || !(await wrap.isVisible())) return null;

  for (const [name, value] of Object.entries(schedule)) {
    const input = wrap.locator(`.mt-input[data-name="${name}"]`);
    if ((await input.count()) > 0) {
      await input.fill(value);
    }
  }

  const submitBtn = wrap.locator(".btn-apply-multitime");
  if (!(await submitBtn.isVisible()) || !(await submitBtn.isEnabled())) return null;

  await submitBtn.click();
  debugLog(runLog.runId, `TIME PICKER -> ${JSON.stringify(schedule)}`);
  state.timePickerSubmitted = true;
  runLog.metrics.timePickerSubmitted = true;
  runLog.transcript.push({
    ts: nowIso(),
    role: "system",
    text: `[auto] submitted multitime picker ${JSON.stringify(schedule)}`,
  });

  let snapshot = previousSnapshot;
  while (true) {
    const response = await waitForAssistantTurnComplete(page, snapshot, assistantTimeoutMs);
    const assistantText = response.latestAssistant?.text || "";
    if (!isTimePickerPromptText(assistantText)) {
      return response;
    }

    debugLog(runLog.runId, "Ignoring repeated time picker assistant turn");
    snapshot = {
      assistantCount: response.assistantCount,
      latestAssistantText: assistantText,
    };
  }
}

function detectSignals(text) {
  const t = normalize(text);
  return {
    asksStatus: /new or existing customer/.test(t),
    asksOrganization: /organisation'?s? name|organization'?s? name|share (?:the )?name of your organisation|share (?:the )?name of your organization|share your organisation|share your organization|provide the name.*organisation|provide the name.*organization|name of your organisation|name of your organization|name and location of your organisation|name and location of your organization|organisation or company name|organization or company name|\bcompany name\b|\borganisation name\b|\borganization name\b/.test(t),
    asksLocation: /location of|where .* located|organisation.*location|organization.*location|name and location of your organisation|name and location of your organization|\blocation\b/.test(t),
    asksContact: /email address|phone number|contact number|either your contact number or email|either your email|either your phone/.test(t),
    asksEventType: /share a bit about your event|what type of event|what kind of event|event you're organising|event you are organising|about your event/.test(t),
    asksRoom: /venue and room|venue or room|which room|what room|room you're considering|room you are considering/.test(t),
    asksDate: /provide the date|date for your|event date|what date|meeting date/.test(t),
    asksAttendees: /attendee count|number of attendees|how many attendees|how many people|how many guests|how many participants/.test(t),
    asksAttendeeReconfirm: /attendee count.*correct|just to confirm.*attendee|confirm.*attendees/.test(t),
    asksSetupStyle: /room setup style|what setup style|which setup style|which layout|what layout|theatre|boardroom style|banquet style|classroom style/.test(t),
    asksLaptopOwnership: /bringing your own laptop|bring your own laptop|own laptop or do you need one/.test(t),
    asksLaptopPreference: /windows or mac|would you prefer a windows or mac|windows or mac laptop/.test(t),
    asksAdaptor: /hdmi connection.*adaptors|need any adaptors|usb.?c adaptor/.test(t),
    asksVideoPackage: /would you like.*video conferencing package|camera.*microphones.*speaker support|dedicated video conferencing package/.test(t),
    asksSpeakerStyle: /inbuilt speaker system|external\/portable pa speakers|portable pa speakers/.test(t),
    asksTechnicianStages: /which stages would you like technician support for|setup, rehearsal\/test & connect, operate during the event, and\/or pack down/.test(t),
    asksConfirmEverything: /have i captured everything|anything else needs to be added|anything else to add|let me know if anything else needs to be added|have i captured everything you mentioned/.test(t),
    asksCreateQuote: /would you like me to create the quote now|yes, create quote|no, not yet/.test(t),
    hasQuoteSummary: /quote summary/.test(t),
    hasTechnicalIssue: /temporary issue|technical issue|still not processing the attendee count|escalate this issue/.test(t),
    assumesVideoWithoutAsk:
      /(i'?ll include|i will include).*(video conferencing|zoom|teams)/.test(t) && !t.includes("?"),
  };
}

function initializeRunState(profile) {
  return {
    statusSent: false,
    organizationSent: false,
    locationSent: false,
    contactSent: false,
    eventTypeSent: false,
    roomSent: false,
    dateSent: false,
    attendeeSent: false,
    attendeeConfirmSent: false,
    setupStyleSent: false,
    laptopOwnershipSent: false,
    laptopPreferenceSent: false,
    adaptorSent: false,
    videoPackageSent: false,
    speakerStyleSent: false,
    technicianCoverageSent: false,
    confirmAllSent: false,
    timePickerSubmitted: false,
    equipmentQueue: [...profile.equipmentQueue],
  };
}

function chooseNextMessage(profile, assistantText, state) {
  const signals = detectSignals(assistantText);

  if (signals.asksStatus && !state.statusSent) {
    state.statusSent = true;
    return profile.status;
  }
  if (signals.asksOrganization && signals.asksLocation && !state.organizationSent && !state.locationSent) {
    state.organizationSent = true;
    state.locationSent = true;
    return `${profile.organization}, ${profile.location}`;
  }
  if (signals.asksOrganization && !state.organizationSent) {
    state.organizationSent = true;
    return profile.organization;
  }
  if (signals.asksLocation && !state.locationSent) {
    state.locationSent = true;
    return profile.location;
  }
  if (signals.asksContact && !state.contactSent) {
    state.contactSent = true;
    return profile.contact;
  }
  if (signals.asksEventType && signals.asksRoom && !state.eventTypeSent && !state.roomSent) {
    state.eventTypeSent = true;
    state.roomSent = true;
    return `${profile.eventType} at ${profile.room}`;
  }
  if (signals.asksEventType && !state.eventTypeSent) {
    state.eventTypeSent = true;
    return profile.eventType;
  }
  if (signals.asksRoom && !state.roomSent) {
    state.roomSent = true;
    return profile.room;
  }
  if (signals.asksDate && !state.dateSent) {
    state.dateSent = true;
    return profile.date;
  }
  if (signals.asksAttendees && !state.attendeeSent) {
    state.attendeeSent = true;
    return profile.attendees;
  }
  if (signals.asksAttendeeReconfirm && !state.attendeeConfirmSent) {
    state.attendeeConfirmSent = true;
    return "yes";
  }
  if (signals.asksSetupStyle && profile.setupStyle && !state.setupStyleSent) {
    state.setupStyleSent = true;
    return profile.setupStyle;
  }
  if (signals.asksLaptopOwnership && profile.laptopOwnershipReply && !state.laptopOwnershipSent) {
    state.laptopOwnershipSent = true;
    return profile.laptopOwnershipReply;
  }
  if (signals.asksLaptopPreference && profile.laptopPreferenceReply && !state.laptopPreferenceSent) {
    state.laptopPreferenceSent = true;
    return profile.laptopPreferenceReply;
  }
  if (signals.asksAdaptor && profile.adaptorReply && !state.adaptorSent) {
    state.adaptorSent = true;
    return profile.adaptorReply;
  }
  if (signals.asksVideoPackage && profile.videoPackageReply && !state.videoPackageSent) {
    state.videoPackageSent = true;
    return profile.videoPackageReply;
  }
  if (signals.asksSpeakerStyle && profile.speakerStyleReply && !state.speakerStyleSent) {
    state.speakerStyleSent = true;
    return profile.speakerStyleReply;
  }
  if (signals.asksTechnicianStages && profile.technicianCoverageReply && !state.technicianCoverageSent) {
    state.technicianCoverageSent = true;
    return profile.technicianCoverageReply;
  }
  if (signals.asksConfirmEverything && !state.confirmAllSent) {
    state.confirmAllSent = true;
    return profile.confirmAllReply;
  }

  if (state.equipmentQueue.length > 0) {
    return state.equipmentQueue.shift();
  }

  return null;
}

function recordAssistantInsights(runLog, assistantText) {
  debugLog(runLog.runId, `ASSISTANT <- ${assistantText.slice(0, 220).replace(/\s+/g, " ")}`);
  const signals = detectSignals(assistantText);
  if (signals.asksAttendees) runLog.metrics.attendeePromptCount += 1;
  if (signals.asksAttendeeReconfirm) runLog.metrics.attendeeReconfirmCount += 1;
  if (signals.hasTechnicalIssue) runLog.metrics.technicalIssueCount += 1;
  if (signals.assumesVideoWithoutAsk) runLog.metrics.videoAssumptionCount += 1;
  if (signals.asksTechnicianStages) runLog.metrics.technicianPromptCount += 1;
  if (signals.asksCreateQuote || signals.hasQuoteSummary) runLog.metrics.quoteReached = true;
}

function evaluateScenario(runLog, profile) {
  const assistantTexts = runLog.transcript
    .filter((entry) => entry.role === "assistant")
    .map((entry) => entry.text || "");

  const normalizedAssistantTexts = assistantTexts.map(normalize);
  const normalizedJoined = normalizedAssistantTexts.join("\n");
  const assertions = [];
  const expected = profile.expected || {};

  function addAssertion(name, passed, details = {}) {
    assertions.push({ name, passed, details });
  }

  if (expected.requiresQuoteSummary) {
    const passed = normalizedJoined.includes("would you like me to create the quote now")
      || normalizedJoined.includes("quote summary");
    addAssertion("quote summary reached", passed);
  }

  if (expected.requiresTechnicianPrompt !== undefined) {
    const passed = expected.requiresTechnicianPrompt
      ? normalizedJoined.includes("which stages would you like technician support for")
      : !normalizedJoined.includes("which stages would you like technician support for");
    addAssertion("technician prompt expectation", passed);
  }

  if (expected.requiresTechnicianSupport !== undefined) {
    const passed = expected.requiresTechnicianSupport
      ? normalizedJoined.includes("technician support")
      : !normalizedJoined.includes("technician support");
    addAssertion("technician support summary expectation", passed);
  }

  for (const label of expected.stageLabels || []) {
    addAssertion(`stage label '${label}' present`, normalizedJoined.includes(normalize(label)));
  }

  for (const label of expected.forbiddenStageLabels || []) {
    addAssertion(`stage label '${label}' absent`, !normalizedJoined.includes(normalize(label)));
  }

  for (const fragment of expected.durationFragments || []) {
    addAssertion(`duration fragment '${fragment}' present`, normalizedJoined.includes(normalize(fragment)));
  }

  for (const pattern of expected.forbiddenAssistantPatterns || []) {
    addAssertion(`forbidden assistant pattern '${pattern}' absent`, !normalizedJoined.includes(normalize(pattern)));
  }

  runLog.assertions = assertions;
  const failedAssertions = assertions.filter((item) => !item.passed);
  if (failedAssertions.length > 0) {
    runLog.status = "FAIL";
    runLog.reason = `Assertions failed: ${failedAssertions.map((item) => item.name).join("; ")}`;
    return;
  }

  if (runLog.metrics.technicalIssueCount > 0) {
    runLog.status = "FAIL";
    runLog.reason = "Technical issue fallback detected";
    return;
  }

  if (runLog.metrics.videoAssumptionCount > 0) {
    runLog.status = "FAIL";
    runLog.reason = "Video package assumed in assistant response";
    return;
  }

  runLog.status = "PASS";
  runLog.reason = "Scenario assertions passed";
}

async function runSingleFlow(browser, config, descriptor) {
  const profile = descriptor.profile;
  const runDir = descriptor.runDir;
  ensureDir(runDir);

  const runLog = {
    runId: descriptor.runId,
    startedAt: nowIso(),
    scenarioId: profile.id,
    scenarioDescription: profile.description,
    status: "UNKNOWN",
    reason: "",
    metrics: {
      assistantTurns: 0,
      attendeePromptCount: 0,
      attendeeReconfirmCount: 0,
      technicalIssueCount: 0,
      videoAssumptionCount: 0,
      technicianPromptCount: 0,
      quoteReached: false,
      timePickerSubmitted: false,
    },
    assertions: [],
    transcript: [],
    screenshotOnFailure: null,
  };

  const context = await browser.newContext();
  const page = await context.newPage();
  const state = initializeRunState(profile);

  try {
    try {
      await page.goto(config.baseUrl, { waitUntil: "domcontentloaded", timeout: 15000 });
    } catch (navErr) {
      if (navErr.message && navErr.message.includes("ERR_CONNECTION_REFUSED")) {
        throw new Error(`Cannot reach ${config.baseUrl}. Start the app first (e.g. dotnet run in MicrohireAgentChat/) and try again.`);
      }
      throw navErr;
    }

    await page.waitForSelector("#textInput", { timeout: 20000 });
    debugLog(runLog.runId, "Chat page ready");
    let conversationState = await getConversationState(page);
    let assistantCount = conversationState.assistantCount;
    let latestAssistantText = conversationState.latestAssistant?.text || "";

    await sendUserMessage(page, profile.fullName, runLog, config.assistantTimeoutMs);
    let response = await waitForAssistantTurnComplete(page, { assistantCount, latestAssistantText }, config.assistantTimeoutMs);
    assistantCount = response.assistantCount;
    latestAssistantText = response.latestAssistant?.text || "";
    runLog.metrics.assistantTurns += 1;
    runLog.transcript.push({ ts: nowIso(), role: "assistant", text: response.latestAssistant?.text || "" });
    appendNdjson(config.ndjsonPath, {
      runId: descriptor.runId,
      scenarioId: profile.id,
      event: "assistant",
      text: response.latestAssistant?.text || "",
      ts: nowIso(),
    });
    recordAssistantInsights(runLog, response.latestAssistant?.text || "");

    for (let turn = 0; turn < config.maxTurns; turn++) {
      const pickerResponse = await maybeSubmitTimePicker(
        page,
        profile.schedule,
        runLog,
        state,
        { assistantCount, latestAssistantText },
        config.assistantTimeoutMs
      );
      if (pickerResponse) {
        assistantCount = pickerResponse.assistantCount;
        latestAssistantText = pickerResponse.latestAssistant?.text || "";
        runLog.metrics.assistantTurns += 1;
        runLog.transcript.push({ ts: nowIso(), role: "assistant", text: pickerResponse.latestAssistant?.text || "" });
        appendNdjson(config.ndjsonPath, {
          runId: descriptor.runId,
          scenarioId: profile.id,
          event: "assistant",
          text: pickerResponse.latestAssistant?.text || "",
          ts: nowIso(),
          tag: "after-timepicker-submit-loop",
        });
        recordAssistantInsights(runLog, pickerResponse.latestAssistant?.text || "");
      }

      conversationState = await getConversationState(page);
      latestAssistantText = conversationState.latestAssistant?.text || "";
      const latestSignals = detectSignals(latestAssistantText);
      if (latestSignals.asksCreateQuote || latestSignals.hasQuoteSummary) {
        break;
      }

      const nextMessage = chooseNextMessage(profile, latestAssistantText, state);
      if (!nextMessage) {
        break;
      }

      await sendUserMessage(page, nextMessage, runLog, config.assistantTimeoutMs);
      const response = await waitForAssistantTurnComplete(page, { assistantCount, latestAssistantText }, config.assistantTimeoutMs);
      assistantCount = response.assistantCount;
      latestAssistantText = response.latestAssistant?.text || "";
      runLog.metrics.assistantTurns += 1;
      runLog.transcript.push({ ts: nowIso(), role: "assistant", text: response.latestAssistant?.text || "" });
      appendNdjson(config.ndjsonPath, {
        runId: descriptor.runId,
        scenarioId: profile.id,
        event: "assistant",
        text: response.latestAssistant?.text || "",
        ts: nowIso(),
      });
      recordAssistantInsights(runLog, response.latestAssistant?.text || "");
    }

    evaluateScenario(runLog, profile);

    if (runLog.status !== "PASS") {
      const shotPath = path.join(runDir, "failure.png");
      await page.screenshot({ path: shotPath, fullPage: true });
      runLog.screenshotOnFailure = shotPath;
    }
  } catch (error) {
    runLog.status = "FAIL";
    runLog.reason = `Exception: ${error.message}`;
    const shotPath = path.join(runDir, "error.png");
    try {
      await page.screenshot({ path: shotPath, fullPage: true });
      runLog.screenshotOnFailure = shotPath;
    } catch {
      // ignore screenshot failures
    }
  } finally {
    runLog.endedAt = nowIso();
    await context.close();
  }

  appendNdjson(config.ndjsonPath, {
    runId: descriptor.runId,
    scenarioId: profile.id,
    event: "run-complete",
    status: runLog.status,
    reason: runLog.reason,
    metrics: runLog.metrics,
    ts: nowIso(),
  });

  fs.writeFileSync(path.join(runDir, "run.json"), JSON.stringify(runLog, null, 2), "utf8");
  return runLog;
}

async function runWithConcurrency(items, limit, worker) {
  const results = new Array(items.length);
  let nextIndex = 0;

  async function runWorker() {
    while (nextIndex < items.length) {
      const current = nextIndex++;
      results[current] = await worker(items[current], current);
    }
  }

  const workers = Array.from({ length: Math.min(limit, items.length) }, () => runWorker());
  await Promise.all(workers);
  return results;
}

async function main() {
  const parsed = parseArgs(process.argv.slice(2));
  ensureDir(parsed.outDir);

  const invocationDir = path.join(parsed.outDir, `invocation-${timestampSlug()}`);
  ensureDir(invocationDir);

  const config = {
    ...parsed,
    invocationDir,
    ndjsonPath: path.join(invocationDir, "runs.ndjson"),
    summaryPath: path.join(invocationDir, "summary.json"),
  };

  fs.writeFileSync(config.ndjsonPath, "", "utf8");

  const profiles = resolveProfiles(config.profile, config.runs);
  const descriptors = profiles.map((profile, index) => {
    const runId = `run-${String(index + 1).padStart(3, "0")}-${slugify(profile.id)}`;
    return {
      runId,
      runDir: path.join(invocationDir, runId),
      profile,
    };
  });

  const browser = await chromium.launch({ headless: config.headless });
  try {
    console.log(
      `Starting Playwright flows: runs=${descriptors.length}, profile=${config.profile}, parallel=${config.parallel}, baseUrl=${config.baseUrl}, headless=${config.headless}`
    );

    const runLogs = await runWithConcurrency(descriptors, config.parallel, async (descriptor, idx) => {
      console.log(`Running flow ${idx + 1}/${descriptors.length} (${descriptor.profile.id})...`);
      const runLog = await runSingleFlow(browser, config, descriptor);
      console.log(`  -> ${runLog.status}: ${runLog.reason}`);
      return runLog;
    });

    const summary = {
      generatedAt: nowIso(),
      config: {
        ...config,
        profiles: profiles.map((profile) => profile.id),
      },
      totals: {
        runs: runLogs.length,
        pass: runLogs.filter((r) => r.status === "PASS").length,
        fail: runLogs.filter((r) => r.status === "FAIL").length,
        warn: runLogs.filter((r) => r.status === "WARN").length,
      },
      runs: runLogs,
    };

    fs.writeFileSync(config.summaryPath, JSON.stringify(summary, null, 2), "utf8");

    console.log("");
    console.log("Completed Playwright flow run.");
    console.log(`Summary: ${config.summaryPath}`);
    console.log(`NDJSON: ${config.ndjsonPath}`);
    console.log(`PASS=${summary.totals.pass} FAIL=${summary.totals.fail} WARN=${summary.totals.warn}`);

    process.exit(summary.totals.fail > 0 ? 1 : 0);
  } finally {
    await browser.close();
  }
}

main().catch((err) => {
  console.error("Fatal error running Playwright flows:", err);
  process.exit(1);
});

