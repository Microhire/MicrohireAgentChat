#!/usr/bin/env python3
"""
Comprehensive test script for booking creation workflow.
This script tests the step-by-step process of creating a booking from conversation data.
"""

import json
import os
from datetime import datetime, timezone

def show_database_setup():
    """Show database connection requirements."""
    print("Database Connection Requirements:")
    print("- SQL Server database 'AITESTDB'")
    print("- Connection string in MicrohireAgentChat/appsettings.json")
    print("- Or environment variables: DB_SERVER, DB_NAME, DB_USER, DB_PASSWORD")
    print()

def create_sample_conversation_data():
    """Create the data structures that would be passed to the orchestration service."""
    now = datetime.now(timezone.utc)

    # This simulates what DisplayMessage objects would look like
    messages = [
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 10)).isoformat(),
            "Parts": ["Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event."],
            "FullText": "Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event."
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 8)).isoformat(),
            "Parts": ["The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees."],
            "FullText": "The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees."
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 6)).isoformat(),
            "Parts": ["We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area."],
            "FullText": "We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area."
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 4)).isoformat(),
            "Parts": ["My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator."],
            "FullText": "My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator."
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 3)).isoformat(),
            "Parts": ["Thanks Michael! Now for the logistics:\n\n- Setup time: 8:00 AM\n- Show start: 6:00 PM\n- Show end: 10:00 PM\n- Strike time: 11:00 PM"],
            "FullText": "Thanks Michael! Now for the logistics:\n\n- Setup time: 8:00 AM\n- Show start: 6:00 PM\n- Show end: 10:00 PM\n- Strike time: 11:00 PM"
        }
    ]

    return messages

def simulate_extraction():
    """Simulate what the ConversationExtractionService would extract."""
    messages = create_sample_conversation_data()

    print("\n=== Simulated Data Extraction ===")

    # ExtractContactInfo simulation
    print("ContactInfo:")
    print("  Name: Michael Knight")
    print("  Email: michael@yes100attendees.com")
    print("  Phone: 07111111111")
    print("  Position: Events Coordinator")

    # ExtractOrganisationFromTranscript simulation
    print("\nOrganization:")
    print("  Name: Yes 100 Attendees")
    print("  Address: (none found)")

    # ExtractExpectedFields simulation
    print("\nFacts Dictionary:")
    facts = {
        "event_date": "15 March 2025",
        "venue_name": "Westin Melbourne",
        "expected_attendees": "100",
        "contact_name": "Michael Knight",
        "contact_email": "michael@yes100attendees.com",
        "organization": "Yes 100 Attendees",
        "setup_time": "8:00 AM",
        "show_start_time": "6:00 PM",
        "show_end_time": "10:00 PM",
        "strike_time": "11:00 PM",
        "equipment_summary": "sound system with microphones, speakers, and mixer. lighting setup with LED lights. staging for the main area.",
        "price_quoted": "6900",
        "hire_price": "5200",
        "labour": "1600",
        "insurance": "100",
        "gst": "0"  # GST included in total
    }

    for key, value in facts.items():
        print(f"  {key}: {value}")

    return facts

def simulate_booking_creation():
    """Simulate what the booking creation process would do."""

    print("\n=== Simulated Booking Creation Steps ===")

    # Step 1: Extract data
    facts = simulate_extraction()

    # Step 2: Contact upsert
    print("\n1. Contact Upsert:")
    print("   - Find/create contact: Michael Knight")
    print("   - Email: michael@yes100attendees.com")
    print("   - Phone: 07111111111")
    print("   - Position: Events Coordinator")
    print("   - Result: ContactID = <generated>")

    # Step 3: Organization lookup/creation
    print("\n2. Organization Processing:")
    print("   - Organization: Yes 100 Attendees")
    print("   - Check if exists: Assume exists from previous runs")
    print("   - Result: OrgID = <existing>, CustomerCode = C14503")

    # Step 4: Link contact to organization
    print("\n3. Contact-Organization Link:")
    print("   - Link ContactID to CustomerCode C14503")

    # Step 5: Create booking
    print("\n4. Booking Creation:")
    print("   - Generate booking number: 25XXXX")
    print("   - Set booking_type_v32 = 2 (Quote/Booking)")
    print("   - Set status = 0 (enquiry)")
    print("   - Set BookingProgressStatus = 1 (enquiry)")
    print("   - Set SDate = 2025-03-15")
    print("   - Set VenueID = 1 (default)")
    print("   - Set contact_nameV6 = Michael Knight")
    print("   - Set OrganizationV6 = Yes 100 Attendees")
    print("   - Set CustID = <org_id>")
    print("   - Set ContactID = <contact_id>")
    print("   - Set price_quoted = 6900")
    print("   - Set hire_price = 5200")
    print("   - Set labour = 1600")
    print("   - Set insurance_v5 = 100")
    print("   - Set sundry_total = 100")
    print("   - Set expAttendees = 100")
    print("   - Set showStartTime = 1800")
    print("   - Set ShowEndTime = 2200")
    print("   - Set setupTimeV61 = 0800")
    print("   - Set StrikeTime = 2300")
    print("   - Set del_time_h = 8, del_time_m = 0")
    print("   - Set ret_time_h = 23, ret_time_m = 0")

    # Step 6: Equipment items
    print("\n5. Equipment Items:")
    print("   - Parse equipment_summary")
    print("   - Create items: Sound System, Lighting Setup, Staging")
    print("   - Insert into tblitemtran")

    # Step 7: Crew/labor
    print("\n6. Crew/Labor:")
    print("   - Would create crew entries if labor_summary existed")

    # Step 8: Transcript
    print("\n7. Conversation Transcript:")
    print("   - Save full conversation to tblbooknote")

    print("\n✓ Booking creation simulation complete")

def main():
    """Main test function."""
    print("=== Booking Creation Workflow Test ===")

    # Show database setup info
    show_database_setup()

    # Simulate the extraction and booking creation
    simulate_booking_creation()

    print("\n=== Testing Complete ===")
    print("\nTo actually test booking creation:")
    print("1. Ensure database is accessible and service is configured")
    print("2. Start the MicrohireAgentChat service: cd MicrohireAgentChat && dotnet run")
    print("3. Open browser to http://localhost:5000")
    print("4. Go through a chat conversation with Isla")
    print("5. When asked to confirm booking, say 'Yes'")
    print("6. Check the application logs for booking creation")
    print("7. Check the database for the new booking record")
    print("8. Verify all related tables were updated correctly")
    print("\nUse the sample_conversation.json file created by test_extraction.py")
    print("to manually verify what data would be extracted from a conversation.")

if __name__ == "__main__":
    main()
