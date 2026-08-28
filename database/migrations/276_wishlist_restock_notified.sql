-- =====================================================================
-- 276_wishlist_restock_notified.sql  —  Wishlist restock alerts throttle.
-- RestockNotifiedAt is stamped when a wishlist owner is emailed that a saved
-- product is back in stock; re-alerts are throttled (14d) off this. Idempotent.
-- =====================================================================

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'WishlistItems' AND COLUMN_NAME = 'RestockNotifiedAt');
SET @sql := IF(@col = 0, 'ALTER TABLE `WishlistItems` ADD COLUMN `RestockNotifiedAt` DATETIME NULL', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '276_wishlist_restock_notified.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '276_wishlist_restock_notified.sql');
