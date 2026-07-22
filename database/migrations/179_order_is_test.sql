-- =====================================================================
-- 179_order_is_test.sql  —  Flag for merchant "try a test order" walkthrough (M10b).
-- A test order runs the REAL pipeline (pricing, tax, shipping, inventory,
-- invoice, notifications) so the merchant sees the machine work before a
-- customer does. It is flagged rather than deleted: deleting would leave
-- gaps in invoice numbering. Analytics excludes IsTest=1 everywhere.
-- Additive, nullable-safe, backfills to 0 for existing rows. (V2 band 170+.)
-- =====================================================================

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'IsTest');
SET @sql := IF(@col = 0,
    'ALTER TABLE `Orders` ADD COLUMN `IsTest` TINYINT(1) NOT NULL DEFAULT 0 AFTER `Notes`',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Analytics filters on it on every read; keep those scans cheap.
SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Orders' AND INDEX_NAME = 'IX_Orders_Tenant_IsTest');
SET @sql := IF(@idx = 0,
    'CREATE INDEX `IX_Orders_Tenant_IsTest` ON `Orders` (`TenantId`, `IsTest`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '179_order_is_test.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '179_order_is_test.sql');
