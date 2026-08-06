-- ---------------------------------------------------------------------------
-- 051_restate_order15_bill_of_supply.sql — correct the one pre-TaxMode invoice
--
-- ORD20260730-00015 was placed while TaxMode was still Exclusive, so it charged
-- 12% GST (₹15.12 on ₹126) and its invoice printed the seeded placeholder GSTIN
-- 33AAAAA0000A1Z5. The shop is not GST-registered: that tax was never collectible
-- and that GSTIN belongs to nobody. It is the only such document — every invoice
-- since is a Bill of Supply with no GST and no GST number.
--
-- The correction keeps the customer whole. They paid ₹191.12 and they still pay
-- ₹191.12; what changes is that the ₹15.12 is restated as part of the price of the
-- goods rather than as tax. So each line absorbs the tax it was charged:
--
--     ₹4.00  + ₹0.48  = ₹4.48    ₹90.00 + ₹10.80 = ₹100.80
--     ₹4.00  + ₹0.48  = ₹4.48    ₹28.00 + ₹3.36  = ₹31.36
--                                          goods  = ₹141.12
--                                       shipping  = ₹50.00
--                                          total  = ₹191.12   (unchanged)
--
-- Order, order lines, invoice and invoice lines are all restated together so the
-- document reconciles with the record behind it. The invoice is corrected in place
-- rather than reissued: deleting and regenerating would mint a new number for a
-- document the customer already holds.
--
-- Idempotent — @oid only resolves while TaxAmount is still > 0, so a second run is
-- a no-op. Guarded by order number, so it does nothing where that order is absent.
-- Rows backed up to /root/backups/order15-before-restatement.sql before first run.
-- ---------------------------------------------------------------------------

SET @oid := (SELECT `OrderId` FROM `Orders`
  WHERE `OrderNumber` = 'ORD20260730-00015' AND `TaxAmount` > 0 LIMIT 1);

-- Lines first: the invoice subtotal below is recomputed from them.
UPDATE `OrderItems`
   SET `UnitPrice` = ROUND((`LineTotal` + `TaxAmount`) / `Quantity`, 2),
       `LineTotal` = `LineTotal` + `TaxAmount`,
       `TaxRate`   = 0,
       `TaxAmount` = 0
 WHERE @oid IS NOT NULL AND `OrderId` = @oid;

UPDATE `InvoiceItems` `ii`
  JOIN `Invoices` `i` ON `i`.`InvoiceId` = `ii`.`InvoiceId`
   SET `ii`.`UnitPrice` = ROUND((`ii`.`LineTotal` + `ii`.`TaxAmount`) / `ii`.`Quantity`, 2),
       `ii`.`LineTotal` = `ii`.`LineTotal` + `ii`.`TaxAmount`,
       `ii`.`TaxRate`   = 0,
       `ii`.`TaxAmount` = 0
 WHERE @oid IS NOT NULL AND `i`.`OrderId` = @oid;

-- TotalAmount is deliberately untouched: what was paid is not in question.
UPDATE `Orders`
   SET `Subtotal`  = `Subtotal` + `TaxAmount`,
       `TaxAmount` = 0,
       `UpdatedAt` = UTC_TIMESTAMP()
 WHERE @oid IS NOT NULL AND `OrderId` = @oid;

UPDATE `Invoices`
   SET `Subtotal`    = (SELECT SUM(`LineTotal`) FROM `OrderItems` WHERE `OrderId` = @oid),
       `TaxAmount`   = 0,
       `CgstAmount`  = 0,
       `SgstAmount`  = 0,
       `IgstAmount`  = 0,
       -- A Bill of Supply carries no GST number, least of all a placeholder.
       `GstNumber`   = NULL
 WHERE @oid IS NOT NULL AND `OrderId` = @oid;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '051_restate_order15_bill_of_supply.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '051_restate_order15_bill_of_supply.sql');
