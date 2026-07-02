-- =====================================================================
-- 026_login_lockout.sql  —  Stage 8: brute-force protection on login
-- Tracks consecutive failed password logins; locks the account for a
-- short window after too many failures. Forward-only (runs once via the
-- guarded migration loop / __schema_migrations).
-- =====================================================================

ALTER TABLE `Users`
    ADD COLUMN `FailedLoginCount` INT NOT NULL DEFAULT 0 AFTER `LastLoginAt`,
    ADD COLUMN `LockoutEndUtc` DATETIME NULL AFTER `FailedLoginCount`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '026_login_lockout.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '026_login_lockout.sql');
