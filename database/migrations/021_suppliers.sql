-- =====================================================================
-- 021_suppliers.sql  —  Supplier / procurement domain
-- Supports today: cost sourcing + "profit by supplier" reporting.
-- Supports later: multi-supplier sourcing, per-supplier cost/lead time,
--                 and purchase orders (POs reference these tables).
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Suppliers` (
    `SupplierId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`         VARCHAR(200) NOT NULL,
    `Code`         VARCHAR(50)  NULL,              -- internal supplier code
    `ContactName`  VARCHAR(150) NULL,
    `Email`        VARCHAR(255) NULL,
    `Phone`        VARCHAR(30)  NULL,
    `Gstin`        VARCHAR(20)  NULL,
    `AddressLine1` VARCHAR(255) NULL,
    `AddressLine2` VARCHAR(255) NULL,
    `City`         VARCHAR(100) NULL,
    `State`        VARCHAR(100) NULL,
    `Pincode`      VARCHAR(10)  NULL,
    `Country`      VARCHAR(100) NOT NULL DEFAULT 'India',
    `PaymentTerms` VARCHAR(100) NULL,              -- e.g. "Net 30"
    `LeadTimeDays` INT NULL,                        -- default replenishment lead time
    `Notes`        VARCHAR(500) NULL,
    `IsActive`     TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`SupplierId`),
    UNIQUE KEY `uq_suppliers_tenant_code` (`TenantId`, `Code`),
    KEY `ix_suppliers_tenant` (`TenantId`),
    KEY `ix_suppliers_name` (`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- A product (optionally a specific variant) can be sourced from many suppliers,
-- each with its own SKU, cost and lead time; one is flagged primary.
CREATE TABLE IF NOT EXISTS `ProductSuppliers` (
    `ProductSupplierId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ProductId`         BIGINT UNSIGNED NOT NULL,
    `ProductVariantId`  BIGINT UNSIGNED NULL,       -- NULL = applies to the whole product
    `SupplierId`        BIGINT UNSIGNED NOT NULL,
    `SupplierSku`       VARCHAR(64) NULL,
    `CostPrice`         DECIMAL(12,2) NULL,          -- per-supplier landed cost
    `Currency`          VARCHAR(3) NOT NULL DEFAULT 'INR',
    `LeadTimeDays`      INT NULL,
    `MinOrderQty`       INT NULL,
    `IsPrimary`         TINYINT(1) NOT NULL DEFAULT 0,
    `IsActive`          TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductSupplierId`),
    UNIQUE KEY `uq_productsupplier` (`TenantId`, `ProductId`, `ProductVariantId`, `SupplierId`),
    KEY `ix_productsuppliers_product` (`ProductId`),
    KEY `ix_productsuppliers_supplier` (`SupplierId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '021_suppliers.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '021_suppliers.sql');
