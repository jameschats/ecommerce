-- =====================================================================
-- 261_cart_notes.sql  —  Order notes on the cart (Part 3 of the dynamic-
-- sections parity plan). PlaceOrderRequest.Notes already existed and
-- flowed into Order.Notes, but nothing on the frontend ever captured a
-- note at all — this lets a shopper add one on the cart page, persisted
-- so it survives a reload, and checkout carries it through automatically.
-- =====================================================================

-- Guarded (this tenant's MySQL build doesn't accept `ADD COLUMN IF NOT EXISTS`) so the script is safely
-- re-runnable — same pattern as 260_product_bundles.sql, after that one caught a real partial-failure.
SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Cart' AND COLUMN_NAME = 'Notes');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `Cart` ADD COLUMN `Notes` VARCHAR(500) NULL AFTER `Status`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '261_cart_notes.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '261_cart_notes.sql');
