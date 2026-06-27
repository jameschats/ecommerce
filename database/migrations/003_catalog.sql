-- =====================================================================
-- 003_catalog.sql  —  Catalog domain
-- Tables: Categories, Brands, Products, ProductImages,
--         ProductVariants, VariantOptions,
--         Attributes, AttributeValues, ProductAttributeValues
-- Cross-domain FKs (TaxRateId, SellerId, MediaFileId) are added in 013.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Categories` (
    `CategoryId`       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ParentCategoryId` BIGINT UNSIGNED NULL,
    `Name`             VARCHAR(150) NOT NULL,
    `Slug`             VARCHAR(180) NOT NULL,
    `Description`      VARCHAR(500) NULL,
    `ImageUrl`         VARCHAR(500) NULL,
    `DisplayOrder`     INT NOT NULL DEFAULT 0,
    `IsActive`         TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CategoryId`),
    UNIQUE KEY `uq_categories_tenant_slug` (`TenantId`, `Slug`),
    KEY `ix_categories_parent` (`ParentCategoryId`),
    CONSTRAINT `fk_categories_parent` FOREIGN KEY (`ParentCategoryId`) REFERENCES `Categories` (`CategoryId`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Brands` (
    `BrandId`     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`    BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`        VARCHAR(150) NOT NULL,
    `Slug`        VARCHAR(180) NOT NULL,
    `LogoUrl`     VARCHAR(500) NULL,
    `Description` VARCHAR(500) NULL,
    `IsActive`    TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`   DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`BrandId`),
    UNIQUE KEY `uq_brands_tenant_slug` (`TenantId`, `Slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Products` (
    `ProductId`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `SellerId`         BIGINT UNSIGNED NULL,            -- future marketplace
    `CategoryId`       BIGINT UNSIGNED NOT NULL,
    `BrandId`          BIGINT UNSIGNED NULL,
    `TaxRateId`        BIGINT UNSIGNED NULL,
    `Sku`              VARCHAR(64)  NOT NULL,
    `Name`             VARCHAR(250) NOT NULL,
    `Slug`             VARCHAR(280) NOT NULL,
    `ShortDescription` VARCHAR(500) NULL,
    `Description`      TEXT NULL,
    `HsnCode`          VARCHAR(20)  NULL,
    `Price`            DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `CompareAtPrice`   DECIMAL(12,2) NULL,
    `CostPrice`        DECIMAL(12,2) NULL,
    `Status`           VARCHAR(20)  NOT NULL DEFAULT 'Draft',  -- Draft | Active | Inactive
    `IsFeatured`       TINYINT(1)   NOT NULL DEFAULT 0,
    `IsActive`         TINYINT(1)   NOT NULL DEFAULT 1,
    `IsDeleted`        TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedBy`        BIGINT UNSIGNED NULL,
    `UpdatedBy`        BIGINT UNSIGNED NULL,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductId`),
    UNIQUE KEY `uq_products_tenant_sku` (`TenantId`, `Sku`),
    KEY `ix_products_tenant_category` (`TenantId`, `CategoryId`),
    KEY `ix_products_brand` (`BrandId`),
    KEY `ix_products_taxrate` (`TaxRateId`),
    KEY `ix_products_slug` (`Slug`),
    FULLTEXT KEY `ft_products_search` (`Name`, `ShortDescription`, `Description`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ProductImages` (
    `ProductImageId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ProductId`      BIGINT UNSIGNED NOT NULL,
    `MediaFileId`    BIGINT UNSIGNED NULL,
    `Url`            VARCHAR(500) NOT NULL,
    `AltText`        VARCHAR(200) NULL,
    `DisplayOrder`   INT NOT NULL DEFAULT 0,
    `IsPrimary`      TINYINT(1) NOT NULL DEFAULT 0,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductImageId`),
    KEY `ix_productimages_product` (`ProductId`),
    KEY `ix_productimages_media` (`MediaFileId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ProductVariants` (
    `ProductVariantId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ProductId`        BIGINT UNSIGNED NOT NULL,
    `Sku`              VARCHAR(64)  NOT NULL,
    `Name`             VARCHAR(200) NULL,             -- e.g. "XL / Black"
    `PriceAdjustment`  DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `IsActive`         TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductVariantId`),
    UNIQUE KEY `uq_productvariants_sku` (`Sku`),
    KEY `ix_productvariants_product` (`ProductId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `VariantOptions` (
    `VariantOptionId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ProductVariantId` BIGINT UNSIGNED NOT NULL,
    `OptionName`       VARCHAR(50)  NOT NULL,         -- Size | Color | Capacity
    `OptionValue`      VARCHAR(100) NOT NULL,         -- XL | Black | 64Wh
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`VariantOptionId`),
    KEY `ix_variantoptions_variant` (`ProductVariantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Attributes` (
    `AttributeId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`         VARCHAR(100) NOT NULL,             -- Warranty | Voltage | Pages
    `Code`         VARCHAR(100) NOT NULL,
    `DataType`     VARCHAR(20)  NOT NULL DEFAULT 'string', -- string | int | decimal | bool
    `IsFilterable` TINYINT(1) NOT NULL DEFAULT 0,
    `IsActive`     TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`AttributeId`),
    UNIQUE KEY `uq_attributes_tenant_code` (`TenantId`, `Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AttributeValues` (
    `AttributeValueId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `AttributeId`      BIGINT UNSIGNED NOT NULL,
    `Value`            VARCHAR(255) NOT NULL,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`AttributeValueId`),
    KEY `ix_attributevalues_attribute` (`AttributeId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ProductAttributeValues` (
    `ProductAttributeValueId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ProductId`               BIGINT UNSIGNED NOT NULL,
    `AttributeId`             BIGINT UNSIGNED NOT NULL,
    `AttributeValueId`        BIGINT UNSIGNED NULL,    -- predefined value, if any
    `ValueText`               VARCHAR(255) NULL,       -- free-form value, if any
    `CreatedAt`               DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ProductAttributeValueId`),
    KEY `ix_pav_product` (`ProductId`),
    KEY `ix_pav_attribute` (`AttributeId`),
    KEY `ix_pav_value` (`AttributeValueId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '003_catalog.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '003_catalog.sql');
