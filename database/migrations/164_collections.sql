-- =====================================================================
-- 164_collections.sql  —  V2 Merchant-Admin M6a: Collections.
-- Merchandising groups (distinct from Categories): Manual membership via a join,
-- or Automated membership from rules (JSON) evaluated against products.
-- Additive (V2 band 160-169).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Collections` (
    `CollectionId`    BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT       NOT NULL DEFAULT 1,
    `Name`            VARCHAR(200) NOT NULL,
    `Slug`            VARCHAR(200) NOT NULL,
    `Description`     TEXT         NULL,
    `ImageUrl`        VARCHAR(500) NULL,
    `CollectionType`  VARCHAR(20)  NOT NULL DEFAULT 'Manual',   -- Manual | Automated
    `MatchType`       VARCHAR(10)  NOT NULL DEFAULT 'All',      -- All | Any (automated)
    `RulesJson`       JSON         NULL,
    `MetaTitle`       VARCHAR(200) NULL,
    `MetaDescription` VARCHAR(500) NULL,
    `DisplayOrder`    INT          NOT NULL DEFAULT 0,
    `IsActive`        TINYINT(1)   NOT NULL DEFAULT 1,
    `CreatedAt`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME     NULL,
    PRIMARY KEY (`CollectionId`),
    UNIQUE KEY `uq_collections_tenant_slug` (`TenantId`, `Slug`),
    KEY `ix_collections_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ProductCollections` (
    `ProductCollectionId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`            BIGINT NOT NULL DEFAULT 1,
    `CollectionId`        BIGINT NOT NULL,
    `ProductId`           BIGINT NOT NULL,
    `DisplayOrder`        INT    NOT NULL DEFAULT 0,
    PRIMARY KEY (`ProductCollectionId`),
    UNIQUE KEY `uq_productcollections` (`CollectionId`, `ProductId`),
    KEY `ix_productcollections_product` (`ProductId`),
    KEY `ix_productcollections_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '164_collections.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '164_collections.sql');
