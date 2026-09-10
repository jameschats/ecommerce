-- =====================================================================
-- 305_tenant_number_sequence_and_golive.sql  —  Per-tenant order/invoice
-- numbering (OrderNumber/InvoiceNumber were derived from the shared
-- cross-tenant Orders/Invoices AUTO_INCREMENT PK, which can't be
-- "restarted" per merchant without colliding with other tenants) + the
-- Go Live gate flag. Idempotent (guarded ADD COLUMN via information_schema).
-- =====================================================================

SET @col_exists = (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Tenants' AND COLUMN_NAME = 'NextOrderSeq'
);
SET @sql = IF(@col_exists = 0,
  'ALTER TABLE `Tenants`
     ADD COLUMN `NextOrderSeq`   BIGINT UNSIGNED NOT NULL DEFAULT 1 AFTER `OffboardedAt`,
     ADD COLUMN `NextInvoiceSeq` BIGINT UNSIGNED NOT NULL DEFAULT 1 AFTER `NextOrderSeq`,
     ADD COLUMN `GoneLiveAt`     DATETIME NULL AFTER `NextInvoiceSeq`',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '305_tenant_number_sequence_and_golive.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '305_tenant_number_sequence_and_golive.sql');
