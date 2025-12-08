#!/bin/bash
# Complete booking creation test script
# This script demonstrates the full booking creation workflow

echo "=== Microhire Agent Chat - Booking Creation Test ==="
echo

# Check if we're in the right directory
if [ ! -f "MicrohireAgentChat/MicrohireAgentChat.csproj" ]; then
    echo "❌ Error: Please run this script from the MicrohireAgentChat root directory"
    exit 1
fi

echo "1. Running data extraction test..."
python test_extraction.py
echo

echo "2. Running booking workflow simulation..."
python test_booking_workflow.py
echo

echo "3. Checking for sample conversation data..."
if [ -f "sample_conversation.json" ]; then
    echo "✓ Sample conversation data available: sample_conversation.json"
    echo "   This file contains the conversation messages that would trigger booking creation."
else
    echo "❌ Sample conversation file not found"
fi
echo

echo "4. Next Steps:"
echo "   a) Start the service: cd MicrohireAgentChat && dotnet run"
echo "   b) Open browser to http://localhost:5000"
echo "   c) Have a conversation with Isla about booking equipment"
echo "   d) When asked to confirm, say 'Yes'"
echo "   e) Check the database for the new booking"
echo

echo "5. Database Tables to Check After Booking Creation:"
echo "   - tblContact: New/updated contact record"
echo "   - tblcust: Organization record (if new)"
echo "   - tblLinkCustContact: Link between contact and organization"
echo "   - tblbookings: Main booking record"
echo "   - tblitemtran: Equipment items"
echo "   - tblcrew: Labor/crew assignments (if applicable)"
echo "   - tblbooknote: Conversation transcript"
echo

echo "6. Key Booking Fields to Verify:"
echo "   - booking_no: Auto-generated (format: YYXXXX)"
echo "   - SDate: Event date (2025-03-15)"
echo "   - VenueID: Venue reference (defaults to 1 if not found)"
echo "   - contact_nameV6: Contact name"
echo "   - OrganizationV6: Organization name"
echo "   - price_quoted: Total quote amount"
echo "   - expAttendees: Expected attendees"
echo "   - Times: showStartTime, ShowEndTime, setupTimeV61, StrikeTime"
echo

echo "✓ Test preparation complete!"
echo "Run the actual booking test by starting the service and having a chat conversation."
