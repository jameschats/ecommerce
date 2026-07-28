-- =====================================================================
-- 255_color_swatches.sql  —  Merchant-editable colour name → hex lookup.
--
-- `ProductVariant`/`VariantOption` stores colour as free text per variant
-- (e.g. "Black", "Rose Gold") with no normalized colour dictionary anywhere
-- in the schema. To render an accurate swatch dot on product cards (a
-- near-universal feature across real Shopify themes) we need a small,
-- tenant-scoped Name -> HexCode lookup a merchant can manage, rather than
-- hardcoding a CSS colour-name list into app code (which silently fails for
-- any name a merchant phrases uniquely, e.g. "Midnight", "Ocean Blue").
--
-- Seeded with common defaults per tenant; VariantOption.OptionValue matches
-- against `Name` case-insensitively at read time (no FK — deliberately loose,
-- since variant text is free-form and shouldn't block product save on a
-- missing swatch mapping).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ColorSwatches` (
    `ColorSwatchId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT       NOT NULL DEFAULT 1,
    `Name`          VARCHAR(60)  NOT NULL,
    `HexCode`       CHAR(7)      NOT NULL,
    `CreatedAt`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME     NULL,
    PRIMARY KEY (`ColorSwatchId`),
    UNIQUE KEY `UX_ColorSwatches_Tenant_Name` (`TenantId`, `Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `ColorSwatches` (`TenantId`, `Name`, `HexCode`)
SELECT t.`TenantId`, d.`Name`, d.`HexCode`
  FROM `Tenants` t
  CROSS JOIN (
      SELECT 'Black' AS Name, '#000000' AS HexCode
      UNION ALL SELECT 'White', '#FFFFFF'
      UNION ALL SELECT 'Grey', '#6B7280'
      UNION ALL SELECT 'Gray', '#6B7280'
      UNION ALL SELECT 'Charcoal', '#36454F'
      UNION ALL SELECT 'Silver', '#C0C0C0'
      UNION ALL SELECT 'Navy', '#001F3F'
      UNION ALL SELECT 'Blue', '#2563EB'
      UNION ALL SELECT 'Sky Blue', '#87CEEB'
      UNION ALL SELECT 'Teal', '#0D9488'
      UNION ALL SELECT 'Green', '#16A34A'
      UNION ALL SELECT 'Olive', '#556B2F'
      UNION ALL SELECT 'Red', '#DC2626'
      UNION ALL SELECT 'Maroon', '#800000'
      UNION ALL SELECT 'Pink', '#EC4899'
      UNION ALL SELECT 'Rose Gold', '#B76E79'
      UNION ALL SELECT 'Orange', '#EA580C'
      UNION ALL SELECT 'Yellow', '#EAB308'
      UNION ALL SELECT 'Gold', '#D4AF37'
      UNION ALL SELECT 'Brown', '#78350F'
      UNION ALL SELECT 'Tan', '#D2B48C'
      UNION ALL SELECT 'Beige', '#F5F5DC'
      UNION ALL SELECT 'Cream', '#FFFDD0'
      UNION ALL SELECT 'Purple', '#7C3AED'
      UNION ALL SELECT 'Lavender', '#E6E6FA'
      UNION ALL SELECT 'Multicolor', '#9CA3AF'
  ) d
 WHERE NOT EXISTS (SELECT 1 FROM `ColorSwatches` c WHERE c.`TenantId` = t.`TenantId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '255_color_swatches.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '255_color_swatches.sql');
