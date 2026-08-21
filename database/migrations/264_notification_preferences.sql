-- =====================================================================
-- 264_notification_preferences.sql — v4 Phase 1, Track B: Consent &
-- Preferences. One row per (user, channel, category) the user has
-- explicitly set. Absence of a row for a "marketing" category means
-- NOT opted in (safe default — no row means no evidence of consent,
-- never inferred). "transactional" sends are never gated by this table
-- at all (checked in application code, not enforced here) — a merchant
-- can't accidentally silence an order confirmation via this mechanism.
-- OptedInAt is the actual consent-event timestamp (not just a boolean)
-- since WhatsApp/marketing compliance needs evidence of *when* consent
-- was given, not just its current state.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `UserNotificationPreferences` (
  `UserNotificationPreferenceId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `UserId` BIGINT UNSIGNED NOT NULL,
  `Channel` VARCHAR(20) NOT NULL,
  `Category` VARCHAR(20) NOT NULL,
  `IsOptedIn` TINYINT(1) NOT NULL DEFAULT 0,
  `OptedInAt` DATETIME NULL,
  `UpdatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`UserNotificationPreferenceId`),
  UNIQUE KEY `UX_UserNotificationPreferences_User_Channel_Category` (`TenantId`, `UserId`, `Channel`, `Category`),
  CONSTRAINT `FK_UserNotificationPreferences_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '264_notification_preferences.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '264_notification_preferences.sql');
