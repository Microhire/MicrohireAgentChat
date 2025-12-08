-- Create booking system tables for PostgreSQL
-- This script creates all the necessary tables for the Microhire Agent Chat booking system

-- Drop tables if they exist (for clean recreation)
DROP TABLE IF EXISTS tblbooknote CASCADE;
DROP TABLE IF EXISTS tblcrew CASCADE;
DROP TABLE IF EXISTS tblitemtran CASCADE;
DROP TABLE IF EXISTS tbllinkcustcontact CASCADE;
DROP TABLE IF EXISTS tblbookings CASCADE;
DROP TABLE IF EXISTS tblcontact CASCADE;
DROP TABLE IF EXISTS tblcust CASCADE;

-- Create tblcust (Organizations/Customers)
CREATE TABLE tblcust (
    id SERIAL PRIMARY KEY,
    customer_code VARCHAR(50) UNIQUE,
    organisationv6 VARCHAR(120),
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    lastupdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create tblcontact (Contacts)
CREATE TABLE tblcontact (
    id SERIAL PRIMARY KEY,
    contactname VARCHAR(100),
    firstname VARCHAR(50),
    surname VARCHAR(50),
    email VARCHAR(80) UNIQUE,
    cell VARCHAR(16),
    position VARCHAR(50),
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    lastupdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    lastcontact TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    active BOOLEAN DEFAULT true
);

-- Create tbllinkcustcontact (Contact-Organization links)
CREATE TABLE tbllinkcustcontact (
    id SERIAL PRIMARY KEY,
    customer_code VARCHAR(50) NOT NULL,
    contactid INTEGER NOT NULL REFERENCES tblcontact(id),
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(customer_code, contactid)
);

-- Create tblbookings (Main bookings table)
CREATE TABLE tblbookings (
    id SERIAL PRIMARY KEY,
    booking_no VARCHAR(35) UNIQUE,
    order_no VARCHAR(25),
    booking_type_v32 SMALLINT DEFAULT 2,
    status SMALLINT DEFAULT 0,
    bookingprogressstatus SMALLINT DEFAULT 1,
    bbookingiscomplete BOOLEAN DEFAULT false,

    -- Dates
    sdate DATE,
    rdate DATE,
    setdate DATE,
    showsdate TIMESTAMP,
    showedate TIMESTAMP,
    rehdate DATE,
    order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    entrydate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Times (stored as strings in HHMM format)
    showstarttime VARCHAR(4),
    showendtime VARCHAR(4),
    setuptimev61 VARCHAR(4),
    striketime VARCHAR(4),

    -- Time components (hours/minutes as numbers)
    del_time_h SMALLINT,
    del_time_m SMALLINT,
    ret_time_h SMALLINT,
    ret_time_m SMALLINT,

    -- Venue
    venueid INTEGER DEFAULT 1,
    venueroom VARCHAR(50),

    -- Contact/Organization
    contact_namev6 VARCHAR(35),
    organizationv6 VARCHAR(100),
    custid INTEGER REFERENCES tblcust(id),
    contactid INTEGER REFERENCES tblcontact(id),

    -- Financial
    price_quoted DECIMAL(19,4),
    hire_price DECIMAL(19,4),
    labour DECIMAL(19,4),
    insurance_v5 DECIMAL(19,4),
    sundry_total DECIMAL(19,4),
    tax2 DECIMAL(19,4),

    -- Other details
    days_using INTEGER DEFAULT 1,
    expattendees INTEGER,
    from_locn INTEGER DEFAULT 20,
    trans_to_locn INTEGER DEFAULT 20,
    return_to_locn INTEGER DEFAULT 20,
    invoiced VARCHAR(1) DEFAULT 'N',
    perm_casual VARCHAR(1) DEFAULT 'Y',
    taxauthority1 INTEGER DEFAULT 0,
    taxauthority2 INTEGER DEFAULT 1,
    showname VARCHAR(100),

    -- Additional fields (simplified to avoid duplicates)
    payment_type SMALLINT,
    deposit_quoted_v50 DECIMAL(19,4),
    docs_produced SMALLINT,
    delivery DECIMAL(19,4),
    percent_disc DECIMAL(19,4),
    delivery_viav71 INTEGER,
    delivery_time VARCHAR(50),
    pickup_viaV71 INTEGER,
    pickup_time VARCHAR(50),
    invoice_no DECIMAL(19,0),
    event_code VARCHAR(50),
    discount_rate DECIMAL(19,4),
    same_address VARCHAR(50),
    un_disc_amount DECIMAL(19,4),
    sales_discount_rate DECIMAL(19,4),
    sales_amount DECIMAL(19,4),
    tax1 DECIMAL(19,4),
    sales_tax_no VARCHAR(50),
    last_modified_by VARCHAR(50),
    delivery_address_exist VARCHAR(50),
    sales_percent_disc DECIMAL(19,4),
    division SMALLINT,
    Item_cnt INTEGER,
    days_charged_v51 DECIMAL(19,4),
    sale_of_asset DECIMAL(19,4),
    retail_value DECIMAL(19,4),
    HourBooked SMALLINT,
    MinBooked SMALLINT,
    SecBooked SMALLINT,
    currencyStr VARCHAR(10),
    ConfirmedBy VARCHAR(100),
    ConfirmedDocRef VARCHAR(100),
    transferNo DECIMAL(19,0)
);

-- Create tblitemtran (Equipment/Items)
CREATE TABLE tblitemtran (
    id SERIAL PRIMARY KEY,
    booking_no_v32 VARCHAR(35),
    item_desc VARCHAR(255),
    hire_rate DECIMAL(19,4),
    qty INTEGER DEFAULT 1,
    line_total DECIMAL(19,4),
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    item_type VARCHAR(50) DEFAULT 'Equipment'
);

-- Create tblcrew (Labor/Crew)
CREATE TABLE tblcrew (
    id SERIAL PRIMARY KEY,
    booking_no VARCHAR(35),
    crew_desc VARCHAR(255),
    hours DECIMAL(10,2),
    rate DECIMAL(19,4),
    line_total DECIMAL(19,4),
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create tblbooknote (Conversation transcripts and notes)
CREATE TABLE tblbooknote (
    id SERIAL PRIMARY KEY,
    bookingno VARCHAR(35),
    textline TEXT,
    notetype SMALLINT DEFAULT 1, -- 1 = transcript
    createdate TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for better performance
CREATE INDEX idx_tblbookings_booking_no ON tblbookings(booking_no);
CREATE INDEX idx_tblbookings_sdate ON tblbookings(sdate);
CREATE INDEX idx_tblbookings_contact_name ON tblbookings(contact_namev6);
CREATE INDEX idx_tblbookings_organization ON tblbookings(organizationv6);
CREATE INDEX idx_tblbookings_order_date ON tblbookings(order_date);

CREATE INDEX idx_tblcontact_email ON tblcontact(email);
CREATE INDEX idx_tblcontact_contactname ON tblcontact(contactname);

CREATE INDEX idx_tblcust_organisation ON tblcust(organisationv6);
CREATE INDEX idx_tblcust_customer_code ON tblcust(customer_code);

CREATE INDEX idx_tbllinkcustcontact_customer_code ON tbllinkcustcontact(customer_code);
CREATE INDEX idx_tbllinkcustcontact_contactid ON tbllinkcustcontact(contactid);

CREATE INDEX idx_tblitemtran_booking_no ON tblitemtran(booking_no_v32);
CREATE INDEX idx_tblcrew_booking_no ON tblcrew(booking_no);
CREATE INDEX idx_tblbooknote_bookingno ON tblbooknote(bookingno);

-- Insert a default venue (since VenueID can't be null)
INSERT INTO tblcust (customer_code, organisationv6) VALUES ('VENUE001', 'Default Venue');

-- Insert sample organization for testing
INSERT INTO tblcust (customer_code, organisationv6) VALUES ('C14503', 'Yes 100 Attendees');

COMMIT;

SELECT 'Booking system tables created successfully!' as status;
SELECT
    'tblcust' as table_name, COUNT(*) as row_count FROM tblcust
UNION ALL
SELECT 'tblcontact' as table_name, COUNT(*) as row_count FROM tblcontact
UNION ALL
SELECT 'tblbookings' as table_name, COUNT(*) as row_count FROM tblbookings
UNION ALL
SELECT 'tblitemtran' as table_name, COUNT(*) as row_count FROM tblitemtran
UNION ALL
SELECT 'tblcrew' as table_name, COUNT(*) as row_count FROM tblcrew
UNION ALL
SELECT 'tblbooknote' as table_name, COUNT(*) as row_count FROM tblbooknote
UNION ALL
SELECT 'tbllinkcustcontact' as table_name, COUNT(*) as row_count FROM tbllinkcustcontact;
