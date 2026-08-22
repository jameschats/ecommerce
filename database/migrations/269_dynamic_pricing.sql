-- =====================================================================
-- 269_dynamic_pricing.sql — v4 Phase 5: Dynamic Pricing (approval-mode
-- only). Merchant controls (floor/ceiling/lock) are non-negotiable
-- foundation, added first per the plan's own design decision — the
-- engine must never be able to operate without them already in place.
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'MinPrice');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `Products`
     ADD COLUMN `MinPrice` DECIMAL(12,2) NULL AFTER `CostPrice`,
     ADD COLUMN `MaxPrice` DECIMAL(12,2) NULL AFTER `MinPrice`,
     ADD COLUMN `PriceLocked` TINYINT(1) NOT NULL DEFAULT 0 AFTER `MaxPrice`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `PricingSeasonRules` (
  `PricingSeasonRuleId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `Name` VARCHAR(120) NOT NULL,
  `StartDate` DATE NOT NULL,
  `EndDate` DATE NOT NULL,
  `BiasPercent` DECIMAL(5,2) NOT NULL,
  `CategoryId` BIGINT UNSIGNED NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`PricingSeasonRuleId`),
  KEY `IX_PricingSeasonRules_Tenant_Dates` (`TenantId`, `StartDate`, `EndDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `PriceSuggestions` (
  `PriceSuggestionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `ProductId` BIGINT UNSIGNED NOT NULL,
  `OldPrice` DECIMAL(12,2) NOT NULL,
  `SuggestedPrice` DECIMAL(12,2) NOT NULL,
  `InventorySignalPercent` DECIMAL(5,2) NOT NULL DEFAULT 0,
  `DemandSignalPercent` DECIMAL(5,2) NOT NULL DEFAULT 0,
  `SeasonalitySignalPercent` DECIMAL(5,2) NOT NULL DEFAULT 0,
  `Reason` VARCHAR(500) NULL,
  `Status` VARCHAR(20) NOT NULL DEFAULT 'Pending',
  `SuggestedAt` DATETIME NOT NULL,
  `ApprovedAt` DATETIME NULL,
  `ApprovedByUserId` BIGINT UNSIGNED NULL,
  `AppliedAt` DATETIME NULL,
  PRIMARY KEY (`PriceSuggestionId`),
  KEY `IX_PriceSuggestions_Tenant_Product` (`TenantId`, `ProductId`),
  KEY `IX_PriceSuggestions_Tenant_Status` (`TenantId`, `Status`),
  CONSTRAINT `FK_PriceSuggestions_Products` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`ProductId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '269_dynamic_pricing.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '269_dynamic_pricing.sql');
