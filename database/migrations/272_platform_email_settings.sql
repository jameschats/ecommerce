-- =====================================================================
-- 272_platform_email_settings.sql  —  Platform transactional-email (SMTP) config.
-- Single row. Lets the platform owner set the SMTP provider/credentials from the
-- super-admin console instead of editing /etc/wavcomm/api.env + restarting.
-- When a row exists it OVERRIDES the app-wide `Email` env config; otherwise the
-- env config still applies. Password encrypted at rest (DataProtection). Mirrors
-- 178_platform_payment_settings. Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `PlatformEmailSettings` (
    `PlatformEmailSettingId` TINYINT UNSIGNED NOT NULL DEFAULT 1,
    `Provider`    VARCHAR(20)  NOT NULL DEFAULT 'Logging',  -- Logging (mock, nothing sent) | Smtp (live)
    `Host`        VARCHAR(200) NULL,
    `Port`        INT          NOT NULL DEFAULT 587,
    `Username`    VARCHAR(200) NULL,
    `Password`    VARCHAR(500) NULL,                        -- encrypted at rest
    `FromAddress` VARCHAR(200) NULL,
    `FromName`    VARCHAR(200) NULL,
    `UseSsl`      TINYINT(1)   NOT NULL DEFAULT 1,
    `UpdatedAt`   DATETIME     NULL,
    `CreatedAt`   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`PlatformEmailSettingId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '272_platform_email_settings.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '272_platform_email_settings.sql');
