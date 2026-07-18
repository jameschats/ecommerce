-- =====================================================================
-- 175_support_tickets.sql  —  Super-admin SA10: support ticketing.
-- Merchants open tickets; the platform works a cross-tenant queue. Messages
-- can be internal notes (platform-only). Both tables ITenantScoped (auto
-- tenant filter + stamp). Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `SupportTickets` (
    `SupportTicketId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL,
    `Subject`          VARCHAR(200)  NOT NULL,
    `Status`           VARCHAR(20)   NOT NULL DEFAULT 'Open',   -- Open | Pending | Closed
    `CreatedByUserId`  BIGINT UNSIGNED NULL,
    `OpenedByPlatform` TINYINT(1)    NOT NULL DEFAULT 0,
    `LastMessageAt`    DATETIME      NULL,
    `CreatedAt`        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`SupportTicketId`),
    KEY `ix_ticket_tenant` (`TenantId`, `SupportTicketId`),
    KEY `ix_ticket_status` (`Status`, `SupportTicketId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SupportMessages` (
    `SupportMessageId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL,
    `SupportTicketId`  BIGINT UNSIGNED NOT NULL,
    `AuthorUserId`     BIGINT UNSIGNED NULL,
    `FromPlatform`     TINYINT(1)    NOT NULL DEFAULT 0,
    `IsInternalNote`   TINYINT(1)    NOT NULL DEFAULT 0,   -- platform-only, hidden from the merchant
    `Body`             VARCHAR(4000) NOT NULL,
    `CreatedAt`        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SupportMessageId`),
    KEY `ix_message_ticket` (`SupportTicketId`, `SupportMessageId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '175_support_tickets.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '175_support_tickets.sql');
