-- =====================================================================
-- 278_growth_campaign_sends.sql  —  AI Growth M1: real campaign sends.
-- Extends GrowthCampaigns so a campaign can be sent (or scheduled) to a
-- customer segment on an owned channel (email in v1), with per-send counts.
-- Idempotent: each column added only if missing.
-- =====================================================================

-- Channel: the owned channel this campaign sends on (v1: 'email').
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'Channel');
SET @s := IF(@c = 0, "ALTER TABLE `GrowthCampaigns` ADD COLUMN `Channel` VARCHAR(30) NOT NULL DEFAULT 'email'", 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- SegmentKey: which customer segment to send to (all|paid|repeat|prospect|subscribers).
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'SegmentKey');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `SegmentKey` VARCHAR(40) NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ScheduledAt: when the send should fire (null = not scheduled). SentAt: when it actually went out.
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'ScheduledAt');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `ScheduledAt` DATETIME NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'SentAt');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `SentAt` DATETIME NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- Per-send counters (audited after a send completes).
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'RecipientCount');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `RecipientCount` INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'SentCount');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `SentCount` INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthCampaigns' AND COLUMN_NAME = 'FailedCount');
SET @s := IF(@c = 0, 'ALTER TABLE `GrowthCampaigns` ADD COLUMN `FailedCount` INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '278_growth_campaign_sends.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '278_growth_campaign_sends.sql');
