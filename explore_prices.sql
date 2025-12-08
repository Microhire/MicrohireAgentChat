-- Check tblItemtran pricing columns for a recent booking
SELECT TOP 5 
    booking_no_v32,
    product_code_v42,
    Comment_desc_v42,
    trans_qty,
    price,
    unitRate,
    Undisc_amt,
    CostPrice,
    BeforeDiscountAmount
FROM tblItemtran
WHERE booking_no_v32 LIKE '250%'
ORDER BY booking_no_v32 DESC;

-- Check tblInvmas pricing columns
SELECT TOP 10
    product_code,
    descriptionv6,
    PrintedDesc,
    groupFld,
    retail_price,
    cost_price,
    wholesale_price,
    trade_price
FROM tblInvmas
WHERE retail_price > 0
ORDER BY product_code;
