-- =====================================================================
-- 295_marketing_plan_settings.sql — Marketing Studio MS2 (sub-step 1):
-- weekly-plan cadence preferences (posts/week per type, week start, default
-- post hour, auto-recur) + per-channel x per-type preference matrix. Both
-- per-tenant. Marketing* cluster, no FKs into core commerce tables. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `MarketingPlanSettings` (
    `MarketingPlanSettingsId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT      NOT NULL,
    `TextPerWeek`     INT         NOT NULL DEFAULT 3,
    `PostersPerWeek`  INT         NOT NULL DEFAULT 2,
    `VideosPerWeek`   INT         NOT NULL DEFAULT 0,
    `WeekStartDay`    INT         NOT NULL DEFAULT 1,
    `DefaultPostHour` INT         NOT NULL DEFAULT 10,
    `AutoRecur`       TINYINT(1)  NOT NULL DEFAULT 0,
    `CreatedAt`       DATETIME(6) NOT NULL,
    `UpdatedAt`       DATETIME(6) NULL,
    PRIMARY KEY (`MarketingPlanSettingsId`),
    UNIQUE KEY `UX_MarketingPlanSettings_Tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `MarketingChannelPrefs` (
    `MarketingChannelPrefId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT      NOT NULL,
    `Platform`     VARCHAR(20) NOT NULL,
    `Enabled`      TINYINT(1)  NOT NULL DEFAULT 1,
    `AllowText`    TINYINT(1)  NOT NULL DEFAULT 1,
    `AllowPoster`  TINYINT(1)  NOT NULL DEFAULT 1,
    `AllowVideo`   TINYINT(1)  NOT NULL DEFAULT 1,
    `UpdatedAt`    DATETIME(6) NULL,
    PRIMARY KEY (`MarketingChannelPrefId`),
    UNIQUE KEY `UX_MarketingChannelPrefs_Tenant_Platform` (`TenantId`, `Platform`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '295_marketing_plan_settings.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '295_marketing_plan_settings.sql');
