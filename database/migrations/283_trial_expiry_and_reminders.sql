-- =====================================================================
-- 283_trial_expiry_and_reminders.sql  —  Billing P1: make trials actually expire.
-- The lifecycle sweep only acts on TenantSubscriptions with a non-null
-- CurrentPeriodEnd, but trials were created with it NULL — so trials never
-- expired. This: (1) adds TrialReminderStage to track which "ends in N days"
-- reminder was last sent, (2) backfills CurrentPeriodEnd for existing trials
-- from the tenant's TrialEndsAt, giving ALREADY-expired trials a fair 7-day
-- runway (rather than instant suspension on the next sweep). Idempotent.
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TenantSubscriptions' AND COLUMN_NAME = 'TrialReminderStage');
SET @s := IF(@c = 0, 'ALTER TABLE `TenantSubscriptions` ADD COLUMN `TrialReminderStage` INT NULL', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- Backfill CurrentPeriodEnd for trials that never had one. Future trial-end → use it as-is;
-- already-past trial-end → now + 7 days, so those merchants get a warning window + reminders
-- before the sweep suspends them, instead of a surprise same-day suspension.
UPDATE `TenantSubscriptions` ts
JOIN `Tenants` t ON t.`TenantId` = ts.`TenantId`
SET ts.`CurrentPeriodEnd` = IF(t.`TrialEndsAt` > UTC_TIMESTAMP(), t.`TrialEndsAt`, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 7 DAY)),
    ts.`UpdatedAt` = UTC_TIMESTAMP()
WHERE ts.`Status` = 'Trial' AND ts.`CurrentPeriodEnd` IS NULL AND t.`TrialEndsAt` IS NOT NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '283_trial_expiry_and_reminders.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '283_trial_expiry_and_reminders.sql');
