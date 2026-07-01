-- =====================================================================
-- 022_tax_mode.sql  —  Tax display mode setting
-- TaxMode: Exclusive (default) | Inclusive | None
--   Exclusive = GST added on top, shown as CGST/SGST/IGST lines
--   Inclusive = prices include GST; shown as "inclusive of all taxes"
--   None      = no GST (Bill of Supply)
-- Idempotent; default keeps current behaviour.
-- =====================================================================

INSERT IGNORE INTO `Settings` (`TenantId`, `SettingKey`, `SettingValue`, `DataType`, `Category`, `Description`)
VALUES (1, 'TaxMode', 'Exclusive', 'string', 'Billing', 'How GST is charged/shown: Exclusive | Inclusive | None');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '022_tax_mode.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '022_tax_mode.sql');
