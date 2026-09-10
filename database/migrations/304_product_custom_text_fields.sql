-- =====================================================================
-- 304_product_custom_text_fields.sql  —  Admin-defined personalization
-- fields on a product (e.g. "Mention your design number"). Merchant-side
-- definition only for now — storefront capture + cart/order persistence
-- is a separate, later pass. Idempotent.
--
-- Originally authored as 301_product_custom_text_fields.sql; renumbered
-- to 302 after a concurrent session claimed 301 for
-- 301_whatsapp_template_wording.sql first (applied before this file's
-- rename), then renumbered again to 304 for the same reason (302 and
-- 303 were both claimed by two more concurrent-session migrations
-- applied first). Same content each time, number only.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ProductCustomTextFields` (
    `ProductCustomTextFieldId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ProductId`                BIGINT UNSIGNED NOT NULL,
    `Label`                    VARCHAR(300) NOT NULL,
    `MaxLength`                INT NOT NULL DEFAULT 255,
    `IsMandatory`              TINYINT(1) NOT NULL DEFAULT 0,
    `DisplayOrder`             INT NOT NULL DEFAULT 0,
    `CreatedAt`                DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`                DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductCustomTextFieldId`),
    KEY `ix_productcustomtextfields_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '304_product_custom_text_fields.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '304_product_custom_text_fields.sql');
