# Microhire Quote Format Comparison: Short vs Our Implementation

## Summary

The **Microhire Proposal Short** PDF is their official "short form" template. Despite the name, it can still list many equipment items—the "short" refers to the **format** (no per-line prices), not the quantity of equipment.

---

## Key Finding: Our Implementation Is Already Mostly "Short Form"

Our current HTML quote **does not show** unit price or line total per equipment item—we show **Description + Qty only**, with section totals (Vision Total, Audio Total, etc.). This matches the Microhire short form format.

---

## Detailed Comparison

| Aspect | Microhire Short (PDF) | Our Current Implementation |
|--------|----------------------|----------------------------|
| **Equipment display** | Description + Qty only | Description + Qty only ✓ |
| **Per-line prices** | None | None ✓ |
| **Section totals** | AUDIO Total, VISION Total, LIGHTING Total, GENERAL Total | Vision Total, Audio Total, plus others ✓ |
| **Technical Services** | Description, Task, Qty, Start, Finish, Hrs, Total ($) | Same structure ✓ |
| **Budget Summary** | Rental Equipment, **Transport**, Labour, Service Charge, Sub Total (ex GST), GST, Total | Rental Equipment, Labour, Service Charge — **missing Transport** |
| **BRIEF section** | Event description (e.g. "A series of professional development Intensives…") | **Missing** |
| **NOTES section** | Tech support availability note | **Missing** |
| **Equipment order** | AUDIO → VISION → LIGHTING → GENERAL | Vision → Audio → others |
| **Per-room breakdown** | Multiple rooms with separate timings (Ballroom, Elevate Room) | Single room |
| **Pages** | 8 | 6 |

---

## Differences to Address (Short Form Variant)

1. **Transport** – Microhire budget includes a Transport line ($180 in their sample). We have `booking.delivery` for this.
2. **BRIEF** – Event description before equipment. We can use `showName` or a generic description.
3. **NOTES** – "Please note that our tech support team will be available…" standard note.
4. **Category order** – Match Microhire: AUDIO, VISION, LIGHTING, GENERAL.
5. **Multi-room** – Future enhancement when we support multiple rooms per booking.

---

## Equipment Count

The Microhire Short PDF example (Family Law Section Intensive Conference) has many items because it is a real event: speakers, mics, consoles, projectors, monitors, lighting, power, etc. A "short" quote can still be long in page count when the event requires a lot of equipment.

---

## Recommendation

Add a **Short Form** quote variant that:
- Adds Transport to the budget (from `booking.delivery` or 0)
- Adds the BRIEF section
- Adds the NOTES section
- Uses Microhire category order (AUDIO, VISION, LIGHTING, GENERAL)

The current format remains the default ("long" / standard) and continues to be used unless the user explicitly requests the short form.
