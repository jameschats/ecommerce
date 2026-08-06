-- ---------------------------------------------------------------------------
-- 045_buyer_gstin_bill_ship.sql — business name, buyer GSTIN, and a real bill-to/ship-to
--
-- The order form collects one free-text address and no GST number, and the quick-order
-- path serialises the lot into Orders.Notes:
--
--   Notes = "Name: …\nMobile: …\nEmail: …\nState: …\nCity: …\nAddress: …"
--
-- So order addresses are unqueryable prose. Orders.BillingAddressId and ShippingAddressId
-- have existed since 005 and are left NULL, and CustomerAddresses.AddressType already
-- distinguishes Billing/Shipping/Both — the shape was there, nothing filled it.
--
-- Dealers have meanwhile been typing their shop name into the personal Name field, which
-- QuickOrderService itself notes in a comment. This gives it a column of its own, along
-- with the buyer's GSTIN so it can be printed on their bill.
--
-- All columns are nullable: nothing is required of orders already placed, and the two
-- new order fields stay optional on the form.
-- ---------------------------------------------------------------------------

-- 1) Buyer identity on the address ----------------------------------------------------
SET @has_company := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerAddresses' AND COLUMN_NAME = 'CompanyName');
SET @sql := IF(@has_company = 0,
  'ALTER TABLE `CustomerAddresses` ADD COLUMN `CompanyName` VARCHAR(200) NULL AFTER `RecipientName`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @has_gstin := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerAddresses' AND COLUMN_NAME = 'Gstin');
SET @sql := IF(@has_gstin = 0,
  'ALTER TABLE `CustomerAddresses` ADD COLUMN `Gstin` VARCHAR(20) NULL AFTER `CompanyName`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 2) Snapshot the buyer's details onto the invoice -------------------------------------
--    Invoices already snapshot BillingName and BillingAddress rather than pointing at the
--    address book, so a later edit cannot rewrite a document already issued. The buyer's
--    GSTIN and company belong in that same snapshot, not read live at render time.
--    Note Invoices.GstNumber is the SELLER's and stays that way.
SET @has_bgst := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'BuyerGstin');
SET @sql := IF(@has_bgst = 0,
  'ALTER TABLE `Invoices` ADD COLUMN `BuyerGstin` VARCHAR(20) NULL AFTER `GstNumber`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @has_bco := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'BuyerCompanyName');
SET @sql := IF(@has_bco = 0,
  'ALTER TABLE `Invoices` ADD COLUMN `BuyerCompanyName` VARCHAR(200) NULL AFTER `BillingName`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @has_ship := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'ShippingAddress');
SET @sql := IF(@has_ship = 0,
  'ALTER TABLE `Invoices` ADD COLUMN `ShippingAddress` VARCHAR(600) NULL AFTER `BillingAddress`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '045_buyer_gstin_bill_ship.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '045_buyer_gstin_bill_ship.sql');
