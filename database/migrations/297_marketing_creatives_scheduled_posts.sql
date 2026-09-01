-- =====================================================================
-- 297_marketing_creatives_scheduled_posts.sql — Marketing Studio MS2
-- (sub-step 3): generated creatives + scheduled posts. Confirming a plan
-- generates a MarketingCreative per approved item (text now; poster/video
-- later) and fans out one ScheduledPost per (item x channel). ScheduledPost
-- is both the schedule and the job history. Marketing* cluster, no FKs into
-- core commerce tables. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `MarketingCreatives` (
    `MarketingCreativeId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`            BIGINT      NOT NULL,
    `MarketingPlanItemId` BIGINT      NOT NULL,
    `Type`                VARCHAR(20) NOT NULL DEFAULT 'text',
    `Status`              VARCHAR(20) NOT NULL DEFAULT 'generated',
    `Body`                TEXT        NULL,
    `OutputMediaUrl`      VARCHAR(500) NULL,
    `ProductId`           BIGINT      NULL,
    `CreatedAt`           DATETIME(6) NOT NULL,
    PRIMARY KEY (`MarketingCreativeId`),
    KEY `IX_MarketingCreatives_Item` (`MarketingPlanItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `ScheduledPosts` (
    `ScheduledPostId`     BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`            BIGINT      NOT NULL,
    `MarketingPlanItemId` BIGINT      NOT NULL,
    `MarketingCreativeId` BIGINT      NOT NULL,
    `Platform`            VARCHAR(20) NOT NULL,
    `ScheduledAt`         DATETIME(6) NOT NULL,
    `Status`              VARCHAR(20) NOT NULL DEFAULT 'pending_approval',
    `ExternalPostId`      VARCHAR(190) NULL,
    `Error`               VARCHAR(500) NULL,
    `CreatedAt`           DATETIME(6) NOT NULL,
    `UpdatedAt`           DATETIME(6) NULL,
    `PublishedAt`         DATETIME(6) NULL,
    PRIMARY KEY (`ScheduledPostId`),
    KEY `IX_ScheduledPosts_Tenant_Status_When` (`TenantId`, `Status`, `ScheduledAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '297_marketing_creatives_scheduled_posts.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '297_marketing_creatives_scheduled_posts.sql');
