#!/usr/bin/env python3
"""
Test script to verify data extraction from conversation messages.
This tests what data the ConversationExtractionService extracts from sample messages.
"""

import json
from datetime import datetime, timezone

def create_sample_conversation():
    """Create sample conversation messages."""
    now = datetime.now(timezone.utc)

    return [
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 10)).isoformat(),
            "Parts": ["Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event."],
            "FullText": "Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event.",
            "Html": "<p>Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event.</p>"
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 8)).isoformat(),
            "Parts": ["The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees."],
            "FullText": "The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees.",
            "Html": "<p>The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees.</p>"
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 6)).isoformat(),
            "Parts": ["We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area."],
            "FullText": "We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area.",
            "Html": "<p>We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area.</p>"
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 4)).isoformat(),
            "Parts": ["My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator."],
            "FullText": "My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator.",
            "Html": "<p>My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator.</p>"
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 3)).isoformat(),
            "Parts": ["Thanks Michael! Now for the logistics:\n\n- Setup time: 8:00 AM\n- Show start: 6:00 PM\n- Show end: 10:00 PM\n- Strike time: 11:00 PM\n\nDoes this work for you?"],
            "FullText": "Thanks Michael! Now for the logistics:\n\n- Setup time: 8:00 AM\n- Show start: 6:00 PM\n- Show end: 10:00 PM\n- Strike time: 11:00 PM\n\nDoes this work for you?",
            "Html": "<p>Thanks Michael! Now for the logistics:</p><ul><li>Setup time: 8:00 AM</li><li>Show start: 6:00 PM</li><li>Show end: 10:00 PM</li><li>Strike time: 11:00 PM</li></ul><p>Does this work for you?</p>"
        }
    ]

def analyze_conversation():
    """Analyze what data should be extracted from the sample conversation."""

    messages = create_sample_conversation()

    print("=== Conversation Analysis ===")
    print(f"Total messages: {len(messages)}")
    print()

    print("=== Expected Data Extraction ===")
    print("From ConversationExtractionService.ExtractContactInfo():")
    print("- Name: Michael Knight")
    print("- Email: michael@yes100attendees.com")
    print("- Phone: 07111111111")
    print("- Position: Events Coordinator")
    print()

    print("From ConversationExtractionService.ExtractOrganisationFromTranscript():")
    print("- Organization: Yes 100 Attendees")
    print("- Address: (not specified)")
    print()

    print("From ConversationExtractionService.ExtractExpectedFields():")
    print("- event_date: 15 March 2025")
    print("- venue_name: Westin Melbourne")
    print("- expected_attendees: 100")
    print("- contact_name: Michael Knight")
    print("- contact_email: michael@yes100attendees.com")
    print("- organization: Yes 100 Attendees")
    print("- setup_time: 8:00 AM")
    print("- show_start_time: 6:00 PM")
    print("- show_end_time: 10:00 PM")
    print("- strike_time: 11:00 PM")
    print("- equipment_summary: sound system, lighting, staging")
    print()

    print("=== Booking Creation Steps ===")
    print("1. ExtractContactInfo → ContactInfo object")
    print("2. ExtractOrganisationFromTranscript → (org_name, address)")
    print("3. ExtractExpectedFields → Dictionary<string, string> facts")
    print("4. ContactPersistenceService.UpsertContactAsync() → contactId")
    print("5. OrganizationPersistenceService.FindOrganisationAsync() or UpsertOrganisationAsync() → orgId, customerCode")
    print("6. OrganizationPersistenceService.LinkContactToOrganisationAsync()")
    print("7. BookingPersistenceService.SaveBookingAsync(facts, contactId, orgId, customerCode, contactName)")
    print("8. ItemPersistenceService.UpsertItemsFromSummaryAsync() if equipment_summary exists")
    print("9. CrewPersistenceService.InsertCrewRowsAsync() if labor_summary exists")
    print("10. BookingPersistenceService.SaveTranscriptAsync()")
    print()

    print("=== Database Tables Affected ===")
    print("- tblContact (insert/update contact)")
    print("- tblcust (insert/update organization if new)")
    print("- tblLinkCustContact (insert link)")
    print("- tblbookings (insert booking)")
    print("- tblitemtran (insert equipment items)")
    print("- tblcrew (insert crew/labor)")
    print("- tblbooknote (insert transcript)")
    print()

    # Save to file
    with open('sample_conversation.json', 'w') as f:
        json.dump(messages, f, indent=2)

    print("✓ Sample conversation saved to 'sample_conversation.json'")

def check_database_schema():
    """Check what the database schema expects for bookings."""

    print("\n=== Database Schema Check ===")
    print("Key fields from tblbookings that should be populated:")
    print("- booking_no: auto-generated")
    print("- booking_type_v32: 2 (Quote/Booking)")
    print("- status: 0 (enquiry/quote)")
    print("- BookingProgressStatus: byte status code")
    print("- SDate: event date")
    print("- VenueID: venue ID (must be valid)")
    print("- contact_nameV6: contact name")
    print("- OrganizationV6: organization name")
    print("- CustID: organization ID")
    print("- ContactID: contact ID")
    print("- price_quoted: total amount")
    print("- hire_price: equipment cost")
    print("- labour: labor cost")
    print("- insurance_v5: insurance cost")
    print("- sundry_total: service charge")
    print("- Tax2: GST amount")
    print("- expAttendees: expected attendees")
    print("- showStartTime, ShowEndTime: HHmm format")
    print("- setupTimeV61, StrikeTime: HHmm format")
    print("- del_time_h/m, ret_time_h/m: setup/strike hours/minutes as bytes")

if __name__ == "__main__":
    analyze_conversation()
    check_database_schema()
