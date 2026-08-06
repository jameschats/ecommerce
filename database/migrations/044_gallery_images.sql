-- =====================================================================
-- 044_gallery_images.sql  —  Admin-managed home page gallery
-- Powers the continuous-scroll photo strip below the price list on the storefront
-- home page (horizontal on desktop, vertical on mobile). Same shape as HomeBanners:
-- an uploaded image (bytes in ImageData, served via the API) OR an external ImageUrl.
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `GalleryImages` (
    `GalleryImageId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Title`            VARCHAR(200) NULL,
    `LinkUrl`          VARCHAR(500) NULL,
    `ImageUrl`         VARCHAR(1000) NULL,
    `ImageData`        LONGBLOB NULL,
    `ImageContentType` VARCHAR(100) NULL,
    `DisplayOrder`     INT NOT NULL DEFAULT 0,
    `IsActive`         TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`GalleryImageId`),
    KEY `ix_galleryimages_active` (`TenantId`, `IsActive`, `DisplayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '044_gallery_images.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '044_gallery_images.sql');
