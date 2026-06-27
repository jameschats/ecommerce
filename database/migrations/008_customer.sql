-- =====================================================================
-- 008_customer.sql  —  Customer domain
-- Tables: CustomerAddresses, Reviews
-- =====================================================================

CREATE TABLE IF NOT EXISTS `CustomerAddresses` (
    `CustomerAddressId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`            BIGINT UNSIGNED NOT NULL,
    `Label`             VARCHAR(50)  NULL,            -- Home | Work
    `RecipientName`     VARCHAR(150) NULL,
    `Phone`             VARCHAR(20)  NULL,
    `Line1`             VARCHAR(255) NOT NULL,
    `Line2`             VARCHAR(255) NULL,
    `City`              VARCHAR(100) NOT NULL,
    `State`             VARCHAR(100) NOT NULL,
    `Pincode`           VARCHAR(10)  NOT NULL,
    `Country`           VARCHAR(100) NOT NULL DEFAULT 'India',
    `AddressType`       VARCHAR(20)  NOT NULL DEFAULT 'Both', -- Billing | Shipping | Both
    `IsDefault`         TINYINT(1) NOT NULL DEFAULT 0,
    `IsDeleted`         TINYINT(1) NOT NULL DEFAULT 0,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CustomerAddressId`),
    KEY `ix_customeraddresses_user` (`UserId`),
    KEY `ix_customeraddresses_pincode` (`Pincode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Reviews` (
    `ReviewId`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`           BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ProductId`          BIGINT UNSIGNED NOT NULL,
    `UserId`             BIGINT UNSIGNED NOT NULL,
    `OrderId`            BIGINT UNSIGNED NULL,
    `Rating`             TINYINT UNSIGNED NOT NULL,
    `Title`              VARCHAR(200) NULL,
    `Comment`            VARCHAR(2000) NULL,
    `IsApproved`         TINYINT(1) NOT NULL DEFAULT 0,
    `IsVerifiedPurchase` TINYINT(1) NOT NULL DEFAULT 0,
    `CreatedAt`          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`          DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ReviewId`),
    KEY `ix_reviews_product` (`ProductId`),
    KEY `ix_reviews_user` (`UserId`),
    KEY `ix_reviews_order` (`OrderId`),
    CONSTRAINT `chk_reviews_rating` CHECK (`Rating` BETWEEN 1 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '008_customer.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '008_customer.sql');
