-- =====================================================================
-- 178_platform_payment_settings.sql  —  Platform (merchant-pays-us) gateway config.
-- Single row. Lets the platform owner set the Razorpay provider/keys from the
-- super-admin console instead of editing /etc/wavcomm/api.env + restarting.
-- When a row exists it OVERRIDES the app-wide `Payments` env config; otherwise
-- the env config still applies. Secret encrypted at rest (DataProtection), same
-- purpose string as the per-tenant merchant keys. Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `PlatformPaymentSettings` (
    `PlatformPaymentSettingId` TINYINT UNSIGNED NOT NULL DEFAULT 1,
    `Provider`          VARCHAR(20)  NOT NULL DEFAULT 'Mock',   -- Mock | Razorpay
    `RazorpayKeyId`     VARCHAR(100) NULL,
    `RazorpayKeySecret` VARCHAR(500) NULL,                      -- encrypted at rest
    `UpdatedAt`         DATETIME     NULL,
    `CreatedAt`         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PlatformPaymentSettingId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '178_platform_payment_settings.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '178_platform_payment_settings.sql');
