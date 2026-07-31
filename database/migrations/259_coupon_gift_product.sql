-- =====================================================================
-- 259_coupon_gift_product.sql  —  Free-gift-at-spend-threshold promos
-- (Phase G). Extends Coupons with an optional gift product reward
-- (independent of DiscountValue/FreeShipping — a coupon can carry any
-- combination), and flags OrderItems that were added as a free gift so
-- they read correctly everywhere instead of looking like a ₹0 pricing
-- bug.
-- =====================================================================

ALTER TABLE `Coupons`
  ADD COLUMN `GiftProductId` BIGINT NULL AFTER `FreeShipping`,
  ADD COLUMN `GiftVariantId` BIGINT NULL AFTER `GiftProductId`;

ALTER TABLE `OrderItems`
  ADD COLUMN `IsFreeGift` TINYINT(1) NOT NULL DEFAULT 0 AFTER `DiscountAmount`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '259_coupon_gift_product.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '259_coupon_gift_product.sql');
