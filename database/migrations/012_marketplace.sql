-- =====================================================================
-- 012_marketplace.sql  —  Future marketplace domain (V3)
-- Tables: Sellers, SellerUsers, SellerCommissions, SellerSettlements
-- Created from Day 1 per the design-for-V3 principle; UNUSED in V1.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Sellers` (
    `SellerId`       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`           VARCHAR(150) NOT NULL,
    `LegalName`      VARCHAR(200) NULL,
    `Email`          VARCHAR(256) NULL,
    `Phone`          VARCHAR(20)  NULL,
    `GstNumber`      VARCHAR(20)  NULL,
    `Status`         VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending | Active | Suspended
    `CommissionRate` DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`SellerId`),
    KEY `ix_sellers_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SellerUsers` (
    `SellerUserId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `SellerId`     BIGINT UNSIGNED NOT NULL,
    `UserId`       BIGINT UNSIGNED NOT NULL,
    `Role`         VARCHAR(50) NULL,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SellerUserId`),
    UNIQUE KEY `uq_sellerusers_seller_user` (`SellerId`, `UserId`),
    KEY `ix_sellerusers_user` (`UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SellerCommissions` (
    `SellerCommissionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `SellerId`           BIGINT UNSIGNED NOT NULL,
    `OrderId`            BIGINT UNSIGNED NOT NULL,
    `OrderItemId`        BIGINT UNSIGNED NULL,
    `CommissionRate`     DECIMAL(5,2)  NOT NULL DEFAULT 0.00,
    `CommissionAmount`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `Status`             VARCHAR(20) NOT NULL DEFAULT 'Pending',
    `CreatedAt`          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SellerCommissionId`),
    KEY `ix_sellercommissions_seller` (`SellerId`),
    KEY `ix_sellercommissions_order` (`OrderId`),
    KEY `ix_sellercommissions_orderitem` (`OrderItemId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SellerSettlements` (
    `SellerSettlementId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `SellerId`           BIGINT UNSIGNED NOT NULL,
    `PeriodStart`        DATE NULL,
    `PeriodEnd`          DATE NULL,
    `TotalSales`         DECIMAL(14,2) NOT NULL DEFAULT 0.00,
    `TotalCommission`    DECIMAL(14,2) NOT NULL DEFAULT 0.00,
    `NetPayable`         DECIMAL(14,2) NOT NULL DEFAULT 0.00,
    `Status`             VARCHAR(20) NOT NULL DEFAULT 'Pending',
    `SettledAt`          DATETIME NULL,
    `CreatedAt`          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SellerSettlementId`),
    KEY `ix_sellersettlements_seller` (`SellerId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '012_marketplace.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '012_marketplace.sql');
