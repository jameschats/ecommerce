-- =====================================================================
-- 172_ai_credits.sql  —  V2 AI-0 foundation.
-- Per-tenant AI credit balance + a signed usage ledger, plus global buyable
-- top-up packs. Credits are abstract, per-action units (the platform pays the
-- AI provider; merchants spend credits). Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `TenantAiCredits` (
    `TenantAiCreditId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT UNSIGNED NOT NULL,
    `Balance`       INT           NOT NULL DEFAULT 0,
    `CycleGrant`    INT           NOT NULL DEFAULT 0,
    `CycleResetAt`  DATETIME      NULL,
    `CreatedAt`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantAiCreditId`),
    UNIQUE KEY `uq_tenantaicredit_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AiUsageLogs` (
    `AiUsageLogId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT UNSIGNED NOT NULL,
    `Feature`       VARCHAR(40)   NOT NULL,
    `Credits`       INT           NOT NULL,           -- signed: <0 spent, >0 granted/topup
    `Tokens`        INT           NULL,
    `CostMicros`    BIGINT        NULL,               -- platform provider cost, INR x 1e6 (margin tuning)
    `Model`         VARCHAR(80)   NULL,
    `UserId`        BIGINT UNSIGNED NULL,
    `CreatedAt`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`AiUsageLogId`),
    KEY `ix_aiusage_tenant` (`TenantId`, `AiUsageLogId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AiCreditPacks` (
    `AiCreditPackId` INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Name`          VARCHAR(80)   NOT NULL,
    `Credits`       INT           NOT NULL,
    `PriceInr`      DECIMAL(10,2) NOT NULL,
    `IsActive`      TINYINT(1)    NOT NULL DEFAULT 1,
    `DisplayOrder`  INT           NOT NULL DEFAULT 0,
    `CreatedAt`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`AiCreditPackId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed the three starter packs (editable later in super-admin). Idempotent by Name.
INSERT INTO `AiCreditPacks` (`Name`, `Credits`, `PriceInr`, `DisplayOrder`)
SELECT 'Starter', 200, 199.00, 1
WHERE NOT EXISTS (SELECT 1 FROM `AiCreditPacks` WHERE `Name` = 'Starter');
INSERT INTO `AiCreditPacks` (`Name`, `Credits`, `PriceInr`, `DisplayOrder`)
SELECT 'Growth', 600, 499.00, 2
WHERE NOT EXISTS (SELECT 1 FROM `AiCreditPacks` WHERE `Name` = 'Growth');
INSERT INTO `AiCreditPacks` (`Name`, `Credits`, `PriceInr`, `DisplayOrder`)
SELECT 'Pro', 1500, 999.00, 3
WHERE NOT EXISTS (SELECT 1 FROM `AiCreditPacks` WHERE `Name` = 'Pro');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '172_ai_credits.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '172_ai_credits.sql');
