-- =====================================================================
-- 275_cart_recovery_email.sql  —  Abandoned-cart recovery: one-time email marker.
-- RecoveryEmailSentAt is stamped when the recovery email goes out, so a cart is
-- never emailed twice. Idempotent column add. Additive.
-- =====================================================================

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Carts' AND COLUMN_NAME = 'RecoveryEmailSentAt');
SET @sql := IF(@col = 0, 'ALTER TABLE `Carts` ADD COLUMN `RecoveryEmailSentAt` DATETIME NULL AFTER `UpdatedAt`', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '275_cart_recovery_email.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '275_cart_recovery_email.sql');
