-- =====================================================================
-- 162_discounts.sql  —  V2 Merchant-Admin M5a: Discounts upgrade.
-- Adds automatic discounts (apply at checkout with no code) and free-shipping
-- discounts to the existing Coupons table (from migration 005).
-- Product/collection targeting + BXGY are a later pass (collections land in M6).
-- Additive (V2 band 160-169).
-- =====================================================================

ALTER TABLE `Coupons`
    ADD COLUMN `Method`       VARCHAR(20) NOT NULL DEFAULT 'Code' AFTER `Code`,        -- Code | Automatic
    ADD COLUMN `FreeShipping` TINYINT(1)  NOT NULL DEFAULT 0      AFTER `DiscountValue`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '162_discounts.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '162_discounts.sql');
