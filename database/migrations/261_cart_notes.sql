-- =====================================================================
-- 261_cart_notes.sql  —  Order notes on the cart (Part 3 of the dynamic-
-- sections parity plan). PlaceOrderRequest.Notes already existed and
-- flowed into Order.Notes, but nothing on the frontend ever captured a
-- note at all — this lets a shopper add one on the cart page, persisted
-- so it survives a reload, and checkout carries it through automatically.
-- =====================================================================

ALTER TABLE `Carts`
  ADD COLUMN `Notes` VARCHAR(500) NULL AFTER `Status`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '261_cart_notes.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '261_cart_notes.sql');
