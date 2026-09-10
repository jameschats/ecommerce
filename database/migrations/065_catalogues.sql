-- ---------------------------------------------------------------------------
-- 065_catalogues.sql — downloadable PDF catalogues (design booklets) listed on
-- their own storefront page, admin-manageable (upload/rename/reorder/remove).
-- The file itself lives on disk via IMediaStorage; this table is metadata only.
-- Forward-only; idempotent.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `Catalogues` (
    `CatalogueId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Title`         VARCHAR(200) NOT NULL,
    `FileUrl`       VARCHAR(500) NOT NULL,
    `FileName`      VARCHAR(255) NOT NULL,
    `FileSizeBytes` BIGINT UNSIGNED NOT NULL DEFAULT 0,
    `DisplayOrder`  INT NOT NULL DEFAULT 0,
    `IsActive`      TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`CatalogueId`),
    KEY `ix_catalogues_tenant` (`TenantId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '065_catalogues.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '065_catalogues.sql');
