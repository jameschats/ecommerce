-- =====================================================================
-- 006_tax_shipping.sql  —  Tax & Shipping domain
-- Tables: TaxRates, ShippingMethods, ShippingZones, Shipments
-- TaxRates carry GST split (CGST/SGST/IGST) resolved by HSN code.
-- ShippingZones model pincode serviceability as inclusive ranges.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `TaxRates` (
    `TaxRateId`     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `HsnCode`       VARCHAR(20)  NULL,
    `Name`          VARCHAR(100) NOT NULL,            -- e.g. "GST 18%"
    `CgstRate`      DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    `SgstRate`      DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    `IgstRate`      DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    `TotalRate`     DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    `IsActive`      TINYINT(1) NOT NULL DEFAULT 1,
    `EffectiveFrom` DATE NULL,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TaxRateId`),
    KEY `ix_taxrates_hsn` (`HsnCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ShippingMethods` (
    `ShippingMethodId`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`              BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`                  VARCHAR(100) NOT NULL,
    `Description`           VARCHAR(255) NULL,
    `RateType`              VARCHAR(20) NOT NULL DEFAULT 'Flat',  -- Flat | Free | Weight | Zone
    `BaseRate`              DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `FreeShippingThreshold` DECIMAL(12,2) NULL,                   -- free above this order value
    `EstimatedDays`         INT NULL,
    `IsActive`              TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`             DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`             DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ShippingMethodId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ShippingZones` (
    `ShippingZoneId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`             VARCHAR(100) NOT NULL,
    `ShippingMethodId` BIGINT UNSIGNED NULL,
    `PincodeStart`     VARCHAR(10) NULL,              -- inclusive range start
    `PincodeEnd`       VARCHAR(10) NULL,              -- inclusive range end
    `Rate`             DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `IsServiceable`    TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ShippingZoneId`),
    KEY `ix_shippingzones_method` (`ShippingMethodId`),
    KEY `ix_shippingzones_pincode` (`PincodeStart`, `PincodeEnd`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Shipments` (
    `ShipmentId`           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`             BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `OrderId`              BIGINT UNSIGNED NOT NULL,
    `ShippingMethodId`     BIGINT UNSIGNED NULL,
    `Courier`              VARCHAR(100) NULL,
    `TrackingNumber`       VARCHAR(100) NULL,
    `Status`               VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending|Packed|Shipped|InTransit|Delivered|Returned
    `EstimatedDeliveryDate` DATE NULL,
    `ShippedAt`            DATETIME NULL,
    `DeliveredAt`          DATETIME NULL,
    `CreatedAt`            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`            DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ShipmentId`),
    KEY `ix_shipments_order` (`OrderId`),
    KEY `ix_shipments_method` (`ShippingMethodId`),
    KEY `ix_shipments_tracking` (`TrackingNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '006_tax_shipping.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '006_tax_shipping.sql');
