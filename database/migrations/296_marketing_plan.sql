-- =====================================================================
-- 296_marketing_plan.sql — Marketing Studio MS2 (sub-step 2): the weekly
-- content plan. MarketingPlan (one per week, draft->confirmed) +
-- MarketingPlanItem (proposed creatives: slot, type, subject, channels,
-- brand toggles). Cheap AI OUTLINE — no creatives generated yet. Marketing*
-- cluster, no FKs into core commerce tables. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `MarketingPlans` (
    `MarketingPlanId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`  BIGINT      NOT NULL,
    `WeekStart` DATETIME(6) NOT NULL,
    `Status`    VARCHAR(20) NOT NULL DEFAULT 'draft',
    `CreatedAt` DATETIME(6) NOT NULL,
    `UpdatedAt` DATETIME(6) NULL,
    PRIMARY KEY (`MarketingPlanId`),
    KEY `IX_MarketingPlans_Tenant_Status` (`TenantId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `MarketingPlanItems` (
    `MarketingPlanItemId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT       NOT NULL,
    `MarketingPlanId` BIGINT       NOT NULL,
    `ScheduledAt`     DATETIME(6)  NOT NULL,
    `Type`            VARCHAR(20)  NOT NULL DEFAULT 'text',
    `ProductId`       BIGINT       NULL,
    `Topic`           VARCHAR(300) NOT NULL,
    `Angle`           VARCHAR(500) NULL,
    `Channels`        VARCHAR(300) NOT NULL DEFAULT '',
    `IncludeLogo`     TINYINT(1)   NOT NULL DEFAULT 1,
    `IncludeName`     TINYINT(1)   NOT NULL DEFAULT 1,
    `Status`          VARCHAR(20)  NOT NULL DEFAULT 'proposed',
    `SortOrder`       INT          NOT NULL DEFAULT 0,
    `CreatedAt`       DATETIME(6)  NOT NULL,
    `UpdatedAt`       DATETIME(6)  NULL,
    PRIMARY KEY (`MarketingPlanItemId`),
    KEY `IX_MarketingPlanItems_Plan` (`MarketingPlanId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '296_marketing_plan.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '296_marketing_plan.sql');
