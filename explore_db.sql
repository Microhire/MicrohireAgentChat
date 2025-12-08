-- Explore pricing structure in tblInvmas
SELECT TOP 10 
    product_code,
    PrintedDesc,
    groupFld,
    retail_price,
    wholesale_price,
    trade_price,
    cost_price,
    def_price
FROM tblInvmas 
WHERE retail_price > 0
ORDER BY product_code;

-- Check a specific item that was in the quote
SELECT 
    product_code,
    PrintedDesc,
    groupFld,
    retail_price,
    wholesale_price,
    trade_price,
    cost_price
FROM tblInvmas 
WHERE product_code LIKE '%13MBP%' OR product_code LIKE '%CHRST%' OR product_code LIKE '%2BAY%';

-- Check how prices are stored in tblItemtran for a recent booking
SELECT TOP 20
    booking_no_v32,
    product_code_v42,
    Comment_desc_v42,
    trans_qty,
    price,
    unitRate,
    Undisc_amt,
    CostPrice
FROM tblItemtran
WHERE booking_no_v32 = '250020'
ORDER BY sub_seq_no;

-- Check booking totals structure
SELECT 
    booking_no,
    hire_price,
    labour,
    sundry_total,
    tax1,
    price_quoted
FROM tblbookings
WHERE booking_no = '250020';
