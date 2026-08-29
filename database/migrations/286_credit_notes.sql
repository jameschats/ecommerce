-- =====================================================================
-- 286_credit_notes.sql  —  Billing C2/C3: platform refunds + GST credit notes.
-- A refund of a subscription charge issues a GST credit note against the original
-- tax invoice. Credit notes are PlatformInvoices with DocumentType='CreditNote'
-- and their own per-FY sequence. Additive + idempotent.
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PlatformInvoices' AND COLUMN_NAME = 'DocumentType');
SET @s := IF(@c = 0, "ALTER TABLE `PlatformInvoices` ADD COLUMN `DocumentType` VARCHAR(20) NOT NULL DEFAULT 'Invoice'", 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PlatformInvoices' AND COLUMN_NAME = 'OriginalInvoiceId');
SET @s := IF(@c = 0, 'ALTER TABLE `PlatformInvoices` ADD COLUMN `OriginalInvoiceId` BIGINT NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PlatformInvoices' AND COLUMN_NAME = 'Notes');
SET @s := IF(@c = 0, 'ALTER TABLE `PlatformInvoices` ADD COLUMN `Notes` VARCHAR(255) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '286_credit_notes.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '286_credit_notes.sql');
