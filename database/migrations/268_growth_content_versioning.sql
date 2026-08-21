-- =====================================================================
-- 268_growth_content_versioning.sql — v4 Phase 4, Track A: Content
-- Library hardening (version history). GrowthContent.Body/Title are
-- edited destructively in place today, with no record of the original
-- AI-generated text once a merchant edits it. OriginalBody/OriginalTitle
-- snapshot the text at generation time and are never touched by an
-- edit, so "was this ever hand-edited" becomes a real, diffable fact
-- (Body != OriginalBody) instead of unknowable. Existing rows get NULL
-- Original* — treated as "unknown, not flagged as edited" in code,
-- never as a false "this was edited" alarm.
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthContents' AND COLUMN_NAME = 'OriginalBody');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `GrowthContents`
     ADD COLUMN `OriginalBody` TEXT NULL AFTER `Body`,
     ADD COLUMN `OriginalTitle` VARCHAR(200) NULL AFTER `Title`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '268_growth_content_versioning.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '268_growth_content_versioning.sql');
