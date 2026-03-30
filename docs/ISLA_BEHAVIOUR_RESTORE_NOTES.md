# Isla Behaviour Restore Notes

This note records the rule baseline restored from `initializer.txt` and the
requested corrections that remain intentionally active.

## Restored baseline behaviours

- Strict flow order remains: contact -> event details -> schedule picker -> AV
  questions -> recommendation summary -> quote.
- One-question-per-message behaviour remains enforced in prompt instructions.
- Required-field gates remain before recommendation/quote steps.
- Westin Ballroom disambiguation remains required (full vs Ballroom 1 vs 2).

## Intentional corrections retained

- No automatic assumptions for paid equipment:
  - Switchers are not auto-added; user confirmation is required.
  - Microphone quantity is user-confirmed (not forced 1:1 from speakers).
  - Video conference components are confirm-first.
- Thrive package handling:
  - THRVAVP is not auto-included; the user must confirm they want it.
- Room language:
  - Isla does not book venue rooms; Isla bases AV quote on selected room.
- Room focus:
  - Quoting focus remains on Thrive, Elevate, and Westin Ballroom variants.
- Operator phrasing:
  - Standardised wording: "Would you like a technical operator to assist you
    during the entire event or only during setup and rehearsal?"
- Labour baseline:
  - Setup and pack down are mandatory baseline stages when labour applies.

## Runtime guardrail adjustments applied

- Quote-summary projector-area guardrail no longer forces 2 areas for all
  Westin Ballroom-family requests.
- Required projector area count now follows requested projector quantity
  (default 1 unless multi-projector is requested).
- Room-missing instruction examples were aligned to current quotable rooms and
  removed outdated references.

## Tool metadata adjustments applied

- `presenter_count` and `speaker_count` schema descriptions now avoid
  auto-assumption language and require user-confirmed values.

