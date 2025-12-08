#!/usr/bin/env python3
"""
Test script to generate a quote PDF with dummy data.
This calls the quote generation endpoint directly.
"""

import requests
import json
from datetime import datetime, timedelta

BASE_URL = "http://localhost:5216"

def generate_test_quote():
    """Generate a test quote by creating a booking and then generating the quote."""
    
    # First, let's check if we can use an existing booking or need to create test data
    # For now, we'll use the QuotesFromBlank controller which accepts QuoteAllFields directly
    
    # Dummy data for a complete quote
    dummy_quote = {
        "Client": "Acme Corporation",
        "ContactName": "John Smith",
        "Email": "john.smith@acmecorp.com",
        "EventDate": "Monday 15 December 2025",
        "EventTitle": "Annual Conference 2025",
        
        "Location": "The Westin Brisbane",
        "Address": "111 Mary Street, Brisbane City QLD 4000",
        "Room": "Grand Ballroom",
        "DateRange": "Monday 15 December 2025 to Tuesday 16 December 2025",
        
        "DeliveryContact": "John Smith",
        "AccountMgrName": "Nishal Kumar",
        "AccountMgrMobile": "+61 04 84814633",
        "AccountMgrEmail": "nishal.kumar@microhire.com.au",
        
        "Reference": "TEST-001",
        
        "SetupDate": "Monday 15 December 2025",
        "SetupTime": "07:00",
        "RehearsalDate": "Monday 15 December 2025",
        "RehearsalTime": "08:00",
        "EventStartDate": "Monday 15 December 2025",
        "EventStartTime": "09:00",
        "EventEndDate": "Monday 15 December 2025",
        "EventEndTime": "17:00",
        
        "RoomNoteHeader": "Grand Ballroom",
        "RoomNoteStarts": "Event Starts - 09:00",
        "RoomNoteEnds": "Event Ends - 17:00",
        "RoomNoteTotal": "$2,500.00",
        
        "VisionRows": [
            {"Description": "4K Projector - Panasonic PT-RZ120", "Qty": "2", "LineTotal": "$800.00", "IsGroup": False, "IsComponent": False},
            {"Description": "150\" Motorized Screen", "Qty": "2", "LineTotal": "$400.00", "IsGroup": False, "IsComponent": False},
            {"Description": "MacBook Pro 16\" Presentation Laptop", "Qty": "1", "LineTotal": "$150.00", "IsGroup": False, "IsComponent": False},
            {"Description": "  └ Laptop Power Supply", "Qty": "1", "LineTotal": None, "IsGroup": False, "IsComponent": True},
            {"Description": "  └ Wireless Presenter", "Qty": "1", "LineTotal": None, "IsGroup": False, "IsComponent": True},
        ],
        "VisionTotal": "$1,350.00",
        
        "AudioRows": [
            {"Description": "Shure Wireless Handheld Microphone", "Qty": "4", "LineTotal": "$320.00", "IsGroup": False, "IsComponent": False},
            {"Description": "Shure Wireless Lapel Microphone", "Qty": "2", "LineTotal": "$200.00", "IsGroup": False, "IsComponent": False},
            {"Description": "JBL EON 615 Powered Speaker", "Qty": "4", "LineTotal": "$400.00", "IsGroup": False, "IsComponent": False},
            {"Description": "Yamaha MG12XU Mixer", "Qty": "1", "LineTotal": "$150.00", "IsGroup": False, "IsComponent": False},
        ],
        "AudioTotal": "$1,070.00",
        
        "LightingRows": [
            {"Description": "LED Wash Light - Chauvet COLORado", "Qty": "8", "LineTotal": "$480.00", "IsGroup": False, "IsComponent": False},
            {"Description": "LED Spotlight - ADJ Focus Spot", "Qty": "4", "LineTotal": "$240.00", "IsGroup": False, "IsComponent": False},
        ],
        "LightingTotal": "$720.00",
        
        "LabourRows": [
            {"Description": "Senior AV Technician", "Task": "Setup", "Qty": "2", "Start": "15/12/25 06:00", "Finish": "15/12/25 09:00", "Hrs": "03:00", "Total": "$330.00"},
            {"Description": "Senior AV Technician", "Task": "Operation", "Qty": "2", "Start": "15/12/25 09:00", "Finish": "15/12/25 17:00", "Hrs": "08:00", "Total": "$880.00"},
            {"Description": "Senior AV Technician", "Task": "Pack Down", "Qty": "2", "Start": "15/12/25 17:00", "Finish": "15/12/25 19:00", "Hrs": "02:00", "Total": "$220.00"},
        ],
        "LabourTotal": "$1,430.00",
        
        "RecordingRows": None,
        "RecordingTotal": "$0.00",
        "DrapeRows": None,
        "DrapeTotal": "$0.00",
        
        "RentalTotal": "$3,140.00",
        "ServiceCharge": "$314.00",
        "SubTotalExGst": "$4,884.00",
        "Gst": "$488.40",
        "GrandTotalIncGst": "$5,372.40",
        
        "BudgetNotesTopLine": "The team at Microhire look forward to working with you to make every aspect of your event a success.",
        "BudgetValidityLine": "To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is valid for 30 days and our resources are subject to availability at the time of booking.",
        "BudgetConfirmLine": "Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.",
        "BudgetContactLine": "However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.",
        "BudgetSignoffLine": "We look forward to working with you on a seamless and successful event.",
        
        "FooterOfficeLine1": "Microhire | The Westin Brisbane",
        "FooterOfficeLine2": "Microhire @ 111 Mary Street, Brisbane City QLD 4000",
        
        "ConfirmP1": "On behalf of Acme Corporation, I accept this proposal and wish to proceed with the details that are confirmed to be correct.",
        "ConfirmP2": "Upon request, any additions or amendments will be updated to this proposal accordingly.",
        "ConfirmP3": "We understand that equipment and personnel are not allocated until this document is signed and returned.",
        "ConfirmP4": "This proposal and billing details are subject to Microhire's terms and conditions.",
        "ConfirmTermsUrl": "https://www.microhire.com.au/terms-conditions/"
    }
    
    print("Generating test quote with dummy data...")
    print(f"Client: {dummy_quote['Client']}")
    print(f"Event: {dummy_quote['EventTitle']}")
    print(f"Date: {dummy_quote['EventDate']}")
    print(f"Total: {dummy_quote['GrandTotalIncGst']}")
    print()
    
    # Try to call the quote generation endpoint
    try:
        # Check if there's a direct endpoint for generating quotes
        # If not, we can use an existing booking
        
        # Option 1: Try the QuotesFromBlank controller
        response = requests.post(
            f"{BASE_URL}/QuotesFromBlank/GenerateFromData",
            json=dummy_quote,
            headers={"Content-Type": "application/json"}
        )
        
        if response.status_code == 200:
            result = response.json()
            if result.get("success"):
                pdf_url = result.get("url", result.get("pdfUrl"))
                print(f"✓ Quote generated successfully!")
                print(f"  PDF URL: {BASE_URL}{pdf_url}")
                return pdf_url
            else:
                print(f"✗ Quote generation failed: {result.get('error', 'Unknown error')}")
        else:
            print(f"Endpoint not found (status {response.status_code}), trying alternative...")
            
    except requests.exceptions.ConnectionError:
        print("✗ Could not connect to server. Make sure the app is running on port 5216")
        return None
    except Exception as e:
        print(f"✗ Error: {e}")
    
    # Option 2: Use an existing booking number to generate quote
    try:
        # Try with a known booking number
        booking_no = "250027"  # Or any existing booking
        response = requests.post(
            f"{BASE_URL}/api/quote/generate/{booking_no}",
            headers={"Content-Type": "application/json"}
        )
        
        if response.status_code == 200:
            result = response.json()
            pdf_url = result.get("url", result.get("pdfUrl"))
            print(f"✓ Quote generated for booking {booking_no}!")
            print(f"  PDF URL: {BASE_URL}{pdf_url}")
            return pdf_url
        else:
            print(f"Status: {response.status_code}")
            print(f"Response: {response.text[:500]}")
            
    except Exception as e:
        print(f"✗ Error with booking method: {e}")
    
    return None


def main():
    print("=" * 60)
    print("QUOTE PDF GENERATION TEST")
    print("=" * 60)
    print()
    
    result = generate_test_quote()
    
    if result:
        print()
        print("=" * 60)
        print("TEST PASSED - Quote PDF generated successfully!")
        print("=" * 60)
    else:
        print()
        print("=" * 60)
        print("TEST INCOMPLETE - Could not generate via API")
        print("Try using the chat interface to generate a quote")
        print("=" * 60)


if __name__ == "__main__":
    main()
