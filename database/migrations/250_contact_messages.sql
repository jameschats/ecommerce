-- =====================================================================
-- 250_contact_messages.sql  —  Storefront contact form (AI-Support C0).
-- Until now /contact rendered a form whose submit() set a success flag and
-- made no HTTP call: every shopper enquiry was silently discarded. This is
-- the store for those messages — the first shopper→merchant channel in the
-- product. Anonymous writes, so the endpoint is rate-limited + honeypotted.
-- Band 250–259 is the AI-Support module (see documents/ai-support/README.md);
-- 180–249 stay reserved for V2-8…V2-13 and AI-Growth.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ContactMessages` (
    `ContactMessageId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT       NOT NULL DEFAULT 1,
    `Name`             VARCHAR(120) NOT NULL,
    `Email`            VARCHAR(200) NOT NULL,
    `Phone`            VARCHAR(30)  NULL,
    `Subject`          VARCHAR(200) NULL,
    `Body`             TEXT         NOT NULL,
    `SourceUrl`        VARCHAR(500) NULL,             -- page the shopper sent it from
    `Status`           VARCHAR(20)  NOT NULL DEFAULT 'New',   -- New | Handled
    `HandledByUserId`  BIGINT       NULL,
    `HandledAt`        DATETIME     NULL,
    `CreatedAt`        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ContactMessageId`),
    KEY `IX_ContactMessages_Tenant_Status` (`TenantId`, `Status`, `ContactMessageId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '250_contact_messages.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '250_contact_messages.sql');
