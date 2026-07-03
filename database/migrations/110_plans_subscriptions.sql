-- =====================================================================
-- 110_plans_subscriptions.sql  —  V2-1: plans, subscriptions, billing,
-- per-tenant settings, payment accounts. Additive (V2 band 110-119).
-- See documents/design-v2.md §7 and v2-stages/v2-stage-1-plans-onboarding.md.
-- =====================================================================

-- Subscription plans you sell to merchants (Starter/Growth/Pro/Enterprise).
CREATE TABLE IF NOT EXISTS `Plans` (
    `PlanId`       INT           NOT NULL AUTO_INCREMENT,
    `Name`         VARCHAR(80)   NOT NULL,
    `Slug`         VARCHAR(80)   NOT NULL,
    `MonthlyPrice` DECIMAL(10,2) NOT NULL DEFAULT 0,
    `MaxProducts`  INT           NULL,                 -- NULL = unlimited
    `MaxOrders`    INT           NULL,                 -- NULL = unlimited (per month)
    `AiCredits`    INT           NOT NULL DEFAULT 0,
    `Features`     JSON          NULL,
    `IsActive`     TINYINT(1)    NOT NULL DEFAULT 1,
    `DisplayOrder` INT           NOT NULL DEFAULT 0,
    `CreatedAt`    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`PlanId`),
    UNIQUE KEY `ux_plans_slug` (`Slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- One current subscription per tenant (state machine: Trial|Active|Suspended|Cancelled).
CREATE TABLE IF NOT EXISTS `TenantSubscriptions` (
    `TenantSubscriptionId`   BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`               BIGINT       NOT NULL,
    `PlanId`                 INT          NOT NULL,
    `Status`                 VARCHAR(20)  NOT NULL DEFAULT 'Trial',
    `CurrentPeriodStart`     DATETIME     NULL,
    `CurrentPeriodEnd`       DATETIME     NULL,
    `RazorpaySubscriptionId` VARCHAR(64)  NULL,
    `CreatedAt`              DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`              DATETIME     NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantSubscriptionId`),
    KEY `ix_tenantsubs_tenant` (`TenantId`),
    KEY `ix_tenantsubs_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Append-only record of each charge (written by the billing webhook).
CREATE TABLE IF NOT EXISTS `TenantBillingHistory` (
    `TenantBillingHistoryId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`               BIGINT       NOT NULL,
    `Amount`                 DECIMAL(10,2) NOT NULL,
    `Status`                 VARCHAR(20)  NOT NULL,
    `RazorpayPaymentId`      VARCHAR(64)  NULL,
    `BilledAt`               DATETIME     NOT NULL,
    `PeriodStart`            DATETIME     NULL,
    `PeriodEnd`              DATETIME     NULL,
    `CreatedAt`              DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantBillingHistoryId`),
    KEY `ix_tenantbilling_tenant` (`TenantId`),
    UNIQUE KEY `ux_tenantbilling_payment` (`RazorpayPaymentId`)   -- webhook idempotency
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Per-tenant key/value config (StoreName, SenderEmail, CurrencyCode, TimeZone, ...).
CREATE TABLE IF NOT EXISTS `TenantSettings` (
    `TenantSettingId` BIGINT      NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT      NOT NULL,
    `Key`             VARCHAR(100) NOT NULL,
    `Value`           TEXT        NULL,
    `CreatedAt`       DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME    NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantSettingId`),
    UNIQUE KEY `ux_tenantsettings` (`TenantId`, `Key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Merchant's own payment account (Razorpay Route) — used in V2-5.
CREATE TABLE IF NOT EXISTS `TenantPaymentAccounts` (
    `TenantPaymentAccountId` BIGINT      NOT NULL AUTO_INCREMENT,
    `TenantId`               BIGINT      NOT NULL,
    `Provider`               VARCHAR(20) NOT NULL DEFAULT 'Razorpay',
    `AccountId`              VARCHAR(120) NULL,
    `IsVerified`             TINYINT(1)  NOT NULL DEFAULT 0,
    `ConnectedAt`            DATETIME    NULL,
    `CreatedAt`              DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`TenantPaymentAccountId`),
    KEY `ix_tenantpayacct_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed the plans (design-v2.md §11). MaxProducts/MaxOrders NULL = unlimited.
INSERT INTO `Plans` (`Name`, `Slug`, `MonthlyPrice`, `MaxProducts`, `MaxOrders`, `AiCredits`, `DisplayOrder`)
SELECT * FROM (
    SELECT 'Starter'    AS Name, 'starter'    AS Slug,  499.00 AS MonthlyPrice,  500  AS MaxProducts, 2000 AS MaxOrders,    0 AS AiCredits, 1 AS DisplayOrder UNION ALL
    SELECT 'Growth',    'growth',     999.00, 5000, 2000,  500, 2 UNION ALL
    SELECT 'Pro',       'pro',       1999.00, NULL, NULL, 2000, 3 UNION ALL
    SELECT 'Enterprise','enterprise',   0.00, NULL, NULL,    0, 4
) seed
WHERE NOT EXISTS (SELECT 1 FROM `Plans`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '110_plans_subscriptions.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '110_plans_subscriptions.sql');
