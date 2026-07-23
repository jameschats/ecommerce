-- =====================================================================
-- 243_growth_campaigns.sql  —  Campaign builder (G2).
--
-- One goal ("Diwali sale on this saree") fans out to several channels at
-- once — Instagram, Facebook, WhatsApp, email — each a GrowthContent row
-- linked back to the campaign. The campaign is the unit a merchant thinks
-- in; the per-channel copy is what they edit and send.
--
-- Adds GrowthCampaigns and a nullable CampaignId on GrowthContents. Band
-- 240-249 is the AI-Growth module (documents/ai-growth/README.md).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `GrowthCampaigns` (
    `GrowthCampaignId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT       NOT NULL DEFAULT 1,
    `Name`             VARCHAR(200) NOT NULL,
    `Goal`             VARCHAR(30)  NOT NULL,               -- new-arrival | festival | weekend-sale | restock | clearance
    `ProductId`        BIGINT       NULL,
    `Language`         VARCHAR(20)  NOT NULL DEFAULT 'English',
    `Status`           VARCHAR(20)  NOT NULL DEFAULT 'Draft',   -- Draft | Kept
    `CreatedByUserId`  BIGINT       NULL,
    `CreatedAt`        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME     NULL,
    PRIMARY KEY (`GrowthCampaignId`),
    KEY `IX_GrowthCampaigns_Tenant` (`TenantId`, `GrowthCampaignId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Link generated content back to its campaign (null for standalone generations).
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'GrowthContents' AND COLUMN_NAME = 'CampaignId');
SET @sql := IF(@has = 0,
    'ALTER TABLE `GrowthContents` ADD COLUMN `CampaignId` BIGINT NULL AFTER `ProductId`, ADD KEY `IX_GrowthContents_Campaign` (`CampaignId`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '243_growth_campaigns.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '243_growth_campaigns.sql');
