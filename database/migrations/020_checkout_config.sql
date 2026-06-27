-- =====================================================================
-- 020_checkout_config.sql  —  Stage 5 (Checkout) configuration & seed
--   * Map the calendar catalog to an HSN code + GST 12% tax rate
--   * Store identity (state + GSTIN) for CGST/SGST vs IGST resolution
--   * A non-serviceable shipping zone to demo pincode serviceability
-- Idempotent; forward-only.
-- =====================================================================

-- Printed calendars → HSN 4910, GST 12% --------------------------------
UPDATE `TaxRates` SET `HsnCode` = '4910' WHERE `TenantId` = 1 AND `Name` = 'GST 12%';

UPDATE `Products` SET `HsnCode` = '4910'
WHERE `TenantId` = 1 AND (`HsnCode` IS NULL OR `HsnCode` = '');

-- Store identity (used by the tax engine: intra-state → CGST+SGST, else IGST)
INSERT IGNORE INTO `Settings` (`TenantId`, `SettingKey`, `SettingValue`, `DataType`, `Category`) VALUES
    (1, 'StoreState',        'Tamil Nadu',           'string', 'Billing'),
    (1, 'StoreGstin',        '33AAAAA0000A1Z5',      'string', 'Billing'),
    (1, 'StoreLegalName',    'CalendarShop Pvt Ltd', 'string', 'Billing'),
    (1, 'DefaultTaxRateName','GST 12%',              'string', 'Billing');

-- Shipping serviceability: default is serviceable everywhere via the flat
-- 'Standard Delivery' method; seed one NON-serviceable range to demonstrate
-- pincode rejection at checkout.
INSERT INTO `ShippingZones` (`TenantId`, `Name`, `ShippingMethodId`, `PincodeStart`, `PincodeEnd`, `Rate`, `IsServiceable`)
SELECT 1, 'Remote — not serviceable', NULL, '999000', '999999', 0.00, 0
WHERE NOT EXISTS (SELECT 1 FROM `ShippingZones` WHERE `TenantId` = 1 AND `Name` = 'Remote — not serviceable');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '020_checkout_config.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '020_checkout_config.sql');
