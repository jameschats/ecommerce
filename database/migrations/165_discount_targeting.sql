-- =====================================================================
-- 165_discount_targeting.sql  —  V2 Merchant-Admin M6b: discount targeting.
-- A discount can apply to the whole Order (default), to specific Products, or to
-- specific Collections. Targets live in CouponTargets; the discount is computed off
-- the eligible line subtotal. Deferred from M5a (needed Collections, built in M6a).
-- Additive (V2 band 160-169).
-- =====================================================================

ALTER TABLE `Coupons`
    ADD COLUMN `AppliesTo` VARCHAR(20) NOT NULL DEFAULT 'Order' AFTER `FreeShipping`;   -- Order | Products | Collections

CREATE TABLE IF NOT EXISTS `CouponTargets` (
    `CouponTargetId` BIGINT      NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT      NOT NULL DEFAULT 1,
    `CouponId`       BIGINT      NOT NULL,
    `TargetType`     VARCHAR(20) NOT NULL,   -- Product | Collection
    `TargetId`       BIGINT      NOT NULL,
    PRIMARY KEY (`CouponTargetId`),
    KEY `ix_coupontargets_coupon` (`CouponId`),
    KEY `ix_coupontargets_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '165_discount_targeting.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '165_discount_targeting.sql');
