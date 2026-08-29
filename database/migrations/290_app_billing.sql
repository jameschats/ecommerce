-- =====================================================================
-- 290_app_billing.sql  —  App Store S4: app pricing + charges.
-- Adds pricing to Apps and an AppCharges ledger. Additive + idempotent.
-- Recurring collection + developer payouts ride the Razorpay Subscriptions/Route
-- rails (enabled when those go live, same as platform P2 billing).
-- =====================================================================

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Apps' AND COLUMN_NAME='Price');
SET @s := IF(@c=0, 'ALTER TABLE `Apps` ADD COLUMN `Price` DECIMAL(12,2) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Apps' AND COLUMN_NAME='BillingInterval');
SET @s := IF(@c=0, "ALTER TABLE `Apps` ADD COLUMN `BillingInterval` VARCHAR(20) NOT NULL DEFAULT 'once'", 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Apps' AND COLUMN_NAME='RevenueSharePercent');
SET @s := IF(@c=0, 'ALTER TABLE `Apps` ADD COLUMN `RevenueSharePercent` DECIMAL(5,2) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

CREATE TABLE IF NOT EXISTS `AppCharges` (
  `AppChargeId`       BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId`          BIGINT NOT NULL,
  `AppId`             BIGINT NOT NULL,
  `AppInstallationId` BIGINT NOT NULL,
  `Type`              VARCHAR(20) NOT NULL DEFAULT 'onetime',
  `Amount`            DECIMAL(12,2) NOT NULL DEFAULT 0,
  `PlatformFee`       DECIMAL(12,2) NOT NULL DEFAULT 0,
  `DeveloperShare`    DECIMAL(12,2) NOT NULL DEFAULT 0,
  `Status`            VARCHAR(20) NOT NULL DEFAULT 'pending',
  `PaymentReference`  VARCHAR(100) NULL,
  `PaidAt`            DATETIME NULL,
  `CreatedAt`         DATETIME NOT NULL,
  PRIMARY KEY (`AppChargeId`),
  KEY `IX_AppCharge_Tenant` (`TenantId`),
  KEY `IX_AppCharge_Install` (`AppInstallationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '290_app_billing.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '290_app_billing.sql');
