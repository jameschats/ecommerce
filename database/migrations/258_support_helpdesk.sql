-- =====================================================================
-- 258_support_helpdesk.sql  —  Turn support tickets into a mini helpdesk.
--
-- Adds escalation tiers, tags, and a per-ticket activity log so a ticket
-- reads as a worked item (assigned, escalated, status-changed by whom and
-- when), not just a message thread. Status vocabulary widens in code
-- (New/On-hold/Resolved added to Open/Pending/Closed) — no data change,
-- existing values stay valid.
--
-- Band note: support tables live in 250+. Additive and idempotent.
-- =====================================================================

-- Tier + tags on the ticket.
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportTickets' AND COLUMN_NAME = 'EscalationTier');
SET @sql := IF(@has = 0,
    'ALTER TABLE `SupportTickets`
        ADD COLUMN `EscalationTier` VARCHAR(4)   NOT NULL DEFAULT ''L1'' AFTER `Priority`,
        ADD COLUMN `Tags`           VARCHAR(300) NULL AFTER `Category`',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Per-ticket activity trail: status/priority/assignment/tier/tag changes, who and when.
CREATE TABLE IF NOT EXISTS `SupportTicketActivities` (
    `SupportTicketActivityId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`                BIGINT       NOT NULL DEFAULT 1,
    `SupportTicketId`         BIGINT       NOT NULL,
    `ActorUserId`             BIGINT       NULL,
    `Type`                    VARCHAR(20)  NOT NULL,          -- created | status | priority | tier | assignee | category | tags | escalated
    `Detail`                  VARCHAR(300) NOT NULL,
    `CreatedAt`               DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SupportTicketActivityId`),
    KEY `IX_SupportTicketActivities_Ticket` (`SupportTicketId`, `SupportTicketActivityId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Assignee + tier are filtered on in the queue.
SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportTickets' AND INDEX_NAME = 'IX_SupportTickets_Assignee');
SET @sql := IF(@idx = 0,
    'CREATE INDEX `IX_SupportTickets_Assignee` ON `SupportTickets` (`AssignedToUserId`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '258_support_helpdesk.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '258_support_helpdesk.sql');
