#!/usr/bin/env python3
"""
Test script to simulate booking creation process as it happens in chat.
This script creates sample conversation data and tests the booking creation flow.
"""

import json
import requests
from datetime import datetime, timezone
import time

def create_sample_conversation():
    """Create sample conversation messages that simulate a real booking chat."""
    now = datetime.now(timezone.utc)

    messages = [
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 10)).isoformat(),
            "Parts": ["Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event."],
            "FullText": "Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event.",
            "Html": "<p>Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event.</p>"
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 9)).isoformat(),
            "Parts": ["Hello Michael! I'd be happy to help you book equipment for your event. Could you please tell me:\n\n1. When is the event?\n2. Where is the venue?\n3. What equipment do you need?\n4. How many people will attend?"],
            "FullText": "Hello Michael! I'd be happy to help you book equipment for your event. Could you please tell me:\n\n1. When is the event?\n2. Where is the venue?\n3. What equipment do you need?\n4. How many people will attend?",
            "Html": "<p>Hello Michael! I'd be happy to help you book equipment for your event. Could you please tell me:</p><ol><li>When is the event?</li><li>Where is the venue?</li><li>What equipment do you need?</li><li>How many people will attend?</li></ol>"
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 8)).isoformat(),
            "Parts": ["The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees."],
            "FullText": "The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees.",
            "Html": "<p>The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees.</p>"
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 7)).isoformat(),
            "Parts": ["Great! So we have:\n- Event: 15 March 2025\n- Venue: Westin Melbourne\n- Attendees: 100\n\nWhat equipment do you need? For example: sound system, lighting, staging, etc."],
            "FullText": "Great! So we have:\n- Event: 15 March 2025\n- Venue: Westin Melbourne\n- Attendees: 100\n\nWhat equipment do you need? For example: sound system, lighting, staging, etc.",
            "Html": "<p>Great! So we have:</p><ul><li>Event: 15 March 2025</li><li>Venue: Westin Melbourne</li><li>Attendees: 100</li></ul><p>What equipment do you need? For example: sound system, lighting, staging, etc.</p>"
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 6)).isoformat(),
            "Parts": ["We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area."],
            "FullText": "We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area.",
            "Html": "<p>We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area.</p>"
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 5)).isoformat(),
            "Parts": ["Perfect! Let me get your contact details for the booking.\n\nWhat's your phone number and email address?"],
            "FullText": "Perfect! Let me get your contact details for the booking.\n\nWhat's your phone number and email address?",
            "Html": "<p>Perfect! Let me get your contact details for the booking.</p><p>What's your phone number and email address?</p>"
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
        },
        {
            "Role": "user",
            "Timestamp": (now.replace(minute=now.minute - 2)).isoformat(),
            "Parts": ["Yes, that timing works perfectly."],
            "FullText": "Yes, that timing works perfectly.",
            "Html": "<p>Yes, that timing works perfectly.</p>"
        },
        {
            "Role": "assistant",
            "Timestamp": (now.replace(minute=now.minute - 1)).isoformat(),
            "Parts": ["Based on your requirements, here's the quote:\n\n**Equipment:**\n- Sound System: $2,500\n- Lighting Setup: $1,800\n- Staging: $900\n\n**Labor:**\n- Setup Crew (2 people, 2 hours): $400\n- Show Crew (3 people, 4 hours): $1,200\n- Strike Crew (2 people, 1 hour): $200\n\n**Total: $6,900** (includes GST)\n\nShall I proceed with creating this booking?"],
            "FullText": "Based on your requirements, here's the quote:\n\n**Equipment:**\n- Sound System: $2,500\n- Lighting Setup: $1,800\n- Staging: $900\n\n**Labor:**\n- Setup Crew (2 people, 2 hours): $400\n- Show Crew (3 people, 4 hours): $1,200\n- Strike Crew (2 people, 1 hour): $200\n\n**Total: $6,900** (includes GST)\n\nShall I proceed with creating this booking?",
            "Html": "<p>Based on your requirements, here's the quote:</p><p><strong>Equipment:</strong></p><ul><li>Sound System: $2,500</li><li>Lighting Setup: $1,800</li><li>Staging: $900</li></ul><p><strong>Labor:</strong></p><ul><li>Setup Crew (2 people, 2 hours): $400</li><li>Show Crew (3 people, 4 hours): $1,200</li><li>Strike Crew (2 people, 1 hour): $200</li></ul><p><strong>Total: $6,900</strong> (includes GST)</p><p>Shall I proceed with creating this booking?</p>"
        },
        {
            "Role": "user",
            "Timestamp": now.isoformat(),
            "Parts": ["Yes, please create the booking."],
            "FullText": "Yes, please create the booking.",
            "Html": "<p>Yes, please create the booking.</p>"
        }
    ]

    return messages

def test_booking_creation(base_url="http://localhost:5000"):
    """Test the booking creation process by simulating the chat flow."""

    print("=== Booking Creation Test ===")
    print(f"Testing against: {base_url}")

    # Create sample conversation
    messages = create_sample_conversation()
    print(f"✓ Created {len(messages)} sample messages")

    # Test the booking creation endpoint
    # Note: This assumes there's an endpoint that accepts the conversation messages
    # and processes them through the orchestration service

    # For now, let's check if the service is running
    try:
        response = requests.get(f"{base_url}/", timeout=5)
        if response.status_code == 200:
            print("✓ Service is running")
        else:
            print(f"⚠ Service responded with status {response.status_code}")
    except requests.exceptions.RequestException as e:
        print(f"❌ Cannot connect to service: {e}")
        print("Make sure the MicrohireAgentChat service is running on port 5000")
        return

    # Check if there's a way to test booking creation via API
    # Looking at the controllers, there might be endpoints we can use

    print("\n=== Manual Testing Instructions ===")
    print("1. Open the chat interface in your browser")
    print("2. Have a conversation similar to the sample messages above")
    print("3. When Isla asks to confirm the booking, say 'Yes'")
    print("4. Check the database to verify the booking was created")
    print("5. Look for the booking number in the logs")

    print("\n=== Expected Data Extraction ===")
    print("- Contact: Michael Knight, michael@yes100attendees.com, 07111111111")
    print("- Organization: Yes 100 Attendees")
    print("- Event Date: 15 March 2025")
    print("- Venue: Westin Melbourne")
    print("- Attendees: 100")
    print("- Equipment: Sound system, lighting, staging")
    print("- Times: Setup 8:00 AM, Show 6:00 PM - 10:00 PM, Strike 11:00 PM")
    print("- Total Quote: $6,900")

    print("\n=== Database Verification ===")
    print("After running the test, check these tables:")
    print("1. tblContact - Should have Michael Knight")
    print("2. tblcust - Should have 'Yes 100 Attendees' organization")
    print("3. tblLinkCustContact - Should link contact to organization")
    print("4. tblbookings - Should have new booking with proper data")

    # Save the sample conversation to a file for manual testing
    with open('sample_conversation.json', 'w') as f:
        json.dump(messages, f, indent=2)

    print("\n✓ Sample conversation saved to 'sample_conversation.json'")
    print("You can use this file to manually test the booking creation process.")

if __name__ == "__main__":
    test_booking_creation()
