-- =====================================================================
-- 262_two_factor_auth.sql — v4 Phase 1: 2FA for Merchant Admin / Super Admin /
-- Staff accounts. TOTP (authenticator app) primary, existing SMS OTP as the
-- 2FA fallback method (OtpService, unchanged — reused, not duplicated).
-- Purely additive: TwoFactorEnabled defaults false, so no existing account
-- is affected until it explicitly completes enrollment (which requires
-- scanning a QR code) — safe to ship without a coordinated rollout.
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'TwoFactorEnabled');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `Users`
     ADD COLUMN `TwoFactorEnabled` TINYINT(1) NOT NULL DEFAULT 0 AFTER `LockoutEndUtc`,
     ADD COLUMN `TwoFactorSecret` VARCHAR(500) NULL AFTER `TwoFactorEnabled`,
     ADD COLUMN `TwoFactorEnabledAt` DATETIME NULL AFTER `TwoFactorSecret`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `UserTwoFactorBackupCodes` (
  `UserTwoFactorBackupCodeId` BIGINT NOT NULL AUTO_INCREMENT,
  `UserId` BIGINT NOT NULL,
  `CodeHash` VARCHAR(255) NOT NULL,
  `UsedAtUtc` DATETIME NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`UserTwoFactorBackupCodeId`),
  KEY `IX_UserTwoFactorBackupCodes_UserId` (`UserId`),
  CONSTRAINT `FK_UserTwoFactorBackupCodes_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '262_two_factor_auth.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '262_two_factor_auth.sql');
