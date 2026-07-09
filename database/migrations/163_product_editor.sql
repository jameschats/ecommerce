-- =====================================================================
-- 163_product_editor.sql  —  V2 Merchant-Admin M6e: product editor adds.
-- Product Type + Tags + per-product SEO (meta title/description). Draft status
-- already exists on Products.Status. Tags also power rule-based Collections (M6a).
-- Additive (V2 band 160-169).
-- =====================================================================

ALTER TABLE `Products`
    ADD COLUMN `ProductType`     VARCHAR(120)  NULL AFTER `BrandId`,
    ADD COLUMN `Tags`            VARCHAR(1000) NULL AFTER `ProductType`,   -- comma-separated
    ADD COLUMN `MetaTitle`       VARCHAR(200)  NULL AFTER `Description`,
    ADD COLUMN `MetaDescription` VARCHAR(500)  NULL AFTER `MetaTitle`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '163_product_editor.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '163_product_editor.sql');
