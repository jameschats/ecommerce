-- =====================================================================
-- 251_conversation_model.sql  —  Generalise support tickets into a two-axis
-- conversation model (AI-Support C1), and add the ticket fields V2-9 specced
-- but never shipped (reference, priority, category, assignee, SLA stamps).
--
-- WHY: SupportTicket.OpenedByPlatform and SupportMessage.FromPlatform are
-- two-party booleans — they cannot express a third participant. Shopper→merchant
-- conversations need one. Axis says which pair of parties a thread is between;
-- AuthorType says who wrote a message.
--
-- EXPAND-ONLY, deliberately. `FromPlatform` is backfilled into `AuthorType` and
-- then LEFT IN PLACE, unused. The deploy applies migrations BEFORE restarting the
-- API (see documents/deployment.md §10-WAV), so during that window the OLD binary
-- is still reading these tables — dropping the column here would 500 the support
-- screens until the restart landed. A later migration drops it once this code is
-- live everywhere. Same reasoning for OpenedByPlatform.
-- =====================================================================

-- ---- SupportTickets: axis, shopper linkage, and the V2-9 fields ----
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportTickets' AND COLUMN_NAME = 'Axis');
SET @sql := IF(@has = 0, '
ALTER TABLE `SupportTickets`
    ADD COLUMN `Axis`             VARCHAR(20)  NOT NULL DEFAULT ''MerchantPlatform'' AFTER `TenantId`,
    ADD COLUMN `Reference`        VARCHAR(20)  NULL AFTER `Axis`,
    ADD COLUMN `Priority`         VARCHAR(10)  NOT NULL DEFAULT ''Normal'' AFTER `Status`,
    ADD COLUMN `Category`         VARCHAR(40)  NULL AFTER `Priority`,
    ADD COLUMN `AssignedToUserId` BIGINT       NULL AFTER `CreatedByUserId`,
    ADD COLUMN `ShopperUserId`    BIGINT       NULL AFTER `AssignedToUserId`,
    ADD COLUMN `ShopperEmail`     VARCHAR(200) NULL AFTER `ShopperUserId`,
    ADD COLUMN `OrderId`          BIGINT       NULL AFTER `ShopperEmail`,
    ADD COLUMN `ProductId`        BIGINT       NULL AFTER `OrderId`,
    ADD COLUMN `FirstResponseAt`  DATETIME     NULL AFTER `LastMessageAt`,
    ADD COLUMN `ResolvedAt`       DATETIME     NULL AFTER `FirstResponseAt`
', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---- SupportMessages: who wrote it (replaces the FromPlatform boolean) ----
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportMessages' AND COLUMN_NAME = 'AuthorType');
SET @sql := IF(@has = 0,
    'ALTER TABLE `SupportMessages` ADD COLUMN `AuthorType` VARCHAR(10) NOT NULL DEFAULT ''Merchant'' AFTER `AuthorUserId`',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---- Backfill (idempotent: only rows still on the defaults) ----
UPDATE `SupportMessages` SET `AuthorType` = 'Platform' WHERE `FromPlatform` = 1 AND `AuthorType` = 'Merchant';

-- Every existing thread is merchant↔platform by definition; Axis default already says so.
UPDATE `SupportTickets`
   SET `Reference` = CONCAT('TKT-', YEAR(`CreatedAt`), '-', LPAD(`SupportTicketId`, 5, '0'))
 WHERE `Reference` IS NULL;

UPDATE `SupportTickets` SET `ResolvedAt` = `UpdatedAt`
 WHERE `Status` = 'Closed' AND `ResolvedAt` IS NULL AND `UpdatedAt` IS NOT NULL;

-- ---- Indexes for the two inbox views ----
SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportTickets' AND INDEX_NAME = 'IX_SupportTickets_Tenant_Axis_Status');
SET @sql := IF(@idx = 0,
    'CREATE INDEX `IX_SupportTickets_Tenant_Axis_Status` ON `SupportTickets` (`TenantId`, `Axis`, `Status`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupportTickets' AND INDEX_NAME = 'IX_SupportTickets_Reference');
SET @sql := IF(@idx = 0,
    'CREATE UNIQUE INDEX `IX_SupportTickets_Reference` ON `SupportTickets` (`Reference`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '251_conversation_model.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '251_conversation_model.sql');
