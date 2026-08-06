-- ---------------------------------------------------------------------------
-- 050_product_seo.sql — per-product SEO fields
--
-- Product meta tags are derived ad-hoc at render time from Name and ShortDescription,
-- with no way to write anything better. Search engines and AI summarisers lean on these
-- heavily, and a description written for a shopper is rarely the one you want quoted.
--
-- Pages have had MetaTitle/MetaDescription since 009 and nothing reads or writes them —
-- no DTO, no UI. These columns are the same idea done properly, on the entity that
-- actually gets crawled.
-- ---------------------------------------------------------------------------

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'MetaTitle');
SET @sql := IF(@c = 0,
  'ALTER TABLE `Products` ADD COLUMN `MetaTitle` VARCHAR(200) NULL AFTER `Description`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'MetaDescription');
SET @sql := IF(@c = 0,
  'ALTER TABLE `Products` ADD COLUMN `MetaDescription` VARCHAR(500) NULL AFTER `MetaTitle`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'MetaKeywords');
SET @sql := IF(@c = 0,
  'ALTER TABLE `Products` ADD COLUMN `MetaKeywords` VARCHAR(500) NULL AFTER `MetaDescription`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '050_product_seo.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '050_product_seo.sql');
