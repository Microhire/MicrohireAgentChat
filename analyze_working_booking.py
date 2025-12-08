#!/usr/bin/env python3
"""
Analyze a working booking (C1374900080) to understand the proper structure
for equipment items, packages, and related data.
"""

import json

# This script will help you understand what fields are needed
# You'll need to run this against your production SQL Server database

print("""
To analyze booking C1374900080, run these SQL queries against your production database:

1. Get the booking details:
SELECT 
    booking_no, order_no, CustCode, contact_nameV6, OrganizationV6,
    VenueID, VenueRoom, status, booking_type_v32, BookingProgressStatus,
    price_quoted, hire_price, labour, insurance_v5, sundry_total,
    SDate, ShowSDate, ShowEdate, SetDate, RehDate,
    showStartTime, ShowEndTime, setupTimeV61, StrikeTime,
    expAttendees, showName, order_date, ID
FROM tblbookings
WHERE booking_no = 'C1374900080' OR CustCode = 'C1374900080'

2. Get equipment items with full structure:
SELECT 
    ID, booking_no_v32, heading_no, seq_no, sub_seq_no,
    trans_type_v41, product_code_v42, Comment_desc_v42,
    trans_qty, price, unitRate, item_type, days_using,
    PackageLevel, ParentCode, GroupSeqNo,
    booking_id, AvailRecFlag, AssignType, QtyShort,
    View_Logi, View_client, FirstDate, RetnDate
FROM tblitemtran
WHERE booking_no_v32 = 'C1374900080'
ORDER BY heading_no, seq_no, sub_seq_no

3. Get crew items:
SELECT 
    ID, booking_no, crew_desc, hours, rate, line_total,
    createdate
FROM tblcrew
WHERE booking_no = 'C1374900080'
ORDER BY ID

4. Check for packages and components:
SELECT 
    i.product_code_v42, i.Comment_desc_v42, i.PackageLevel, i.ParentCode,
    i.trans_qty, i.price, i.seq_no, i.sub_seq_no
FROM tblitemtran i
WHERE i.booking_no_v32 = 'C1374900080'
ORDER BY i.PackageLevel NULLS LAST, i.seq_no, i.sub_seq_no

Save the results and we'll update the insert script to match this structure.
""")


