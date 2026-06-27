-- =====================================================================
-- 004_inventory.sql  —  Inventory domain
-- Tables: Inventory, InventoryTransactions
-- Tracks available vs reserved stock; transactions form an audit trail.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Inventory` (
    `InventoryId`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ProductId`        BIGINT UNSIGNED NOT NULL,
    `ProductVariantId` BIGINT UNSIGNED NULL,
    `AvailableQty`     INT NOT NULL DEFAULT 0,
    `ReservedQty`      INT NOT NULL DEFAULT 0,
    `ReorderLevel`     INT NOT NULL DEFAULT 0,   -- low-stock alert threshold
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`InventoryId`),
    UNIQUE KEY `uq_inventory_product_variant` (`ProductId`, `ProductVariantId`),
    KEY `ix_inventory_variant` (`ProductVariantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `InventoryTransactions` (
    `InventoryTransactionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `InventoryId`            BIGINT UNSIGNED NOT NULL,
    `ProductId`              BIGINT UNSIGNED NOT NULL,
    `ChangeQty`              INT NOT NULL,            -- signed: +receipt / -sale
    `BalanceAfter`           INT NULL,
    `TransactionType`        VARCHAR(30) NOT NULL,    -- Purchase|Sale|Reservation|Release|Adjustment|Return
    `ReferenceType`          VARCHAR(50) NULL,        -- Order|ImportJob|Manual
    `ReferenceId`            BIGINT UNSIGNED NULL,
    `Notes`                  VARCHAR(255) NULL,
    `CreatedBy`              BIGINT UNSIGNED NULL,
    `CreatedAt`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`InventoryTransactionId`),
    KEY `ix_invtxn_inventory` (`InventoryId`),
    KEY `ix_invtxn_product` (`ProductId`),
    KEY `ix_invtxn_reference` (`ReferenceType`, `ReferenceId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '004_inventory.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '004_inventory.sql');
