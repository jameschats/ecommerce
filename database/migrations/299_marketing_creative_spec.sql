-- =====================================================================
-- 299_marketing_creative_spec.sql — Poster Studio v2: persist the editable
-- spec (kind/headline/price/colours/template/format...) that produced a
-- poster creative, not just its final rendered output. Without this, a
-- poster in the Library could never be reopened for editing or safely
-- duplicated — only viewed as a fixed image. Marketing* cluster, no FKs
-- into core commerce tables. Idempotent.
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='MarketingCreatives' AND COLUMN_NAME='Spec');
SET @s := IF(@c=0, 'ALTER TABLE `MarketingCreatives` ADD COLUMN `Spec` JSON NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='MarketingCreatives' AND COLUMN_NAME='UpdatedAt');
SET @s := IF(@c=0, 'ALTER TABLE `MarketingCreatives` ADD COLUMN `UpdatedAt` DATETIME(6) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '299_marketing_creative_spec.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '299_marketing_creative_spec.sql');
