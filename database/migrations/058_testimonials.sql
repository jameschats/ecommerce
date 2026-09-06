-- =====================================================================
-- 058_testimonials.sql  —  Admin-managed home page testimonials
-- Independent of the real product Reviews table, which has no "feature this on the
-- homepage" concept and no cross-product public read — this is a small, admin-curated
-- quote list, same shape as HomeBanners/GalleryImages (optional photo upload).
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Testimonials` (
    `TestimonialId`     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`              VARCHAR(150) NOT NULL,
    `RoleOrCompany`     VARCHAR(150) NULL,
    `Quote`             VARCHAR(1000) NOT NULL,
    `Rating`            TINYINT UNSIGNED NOT NULL DEFAULT 5,
    `PhotoUrl`          VARCHAR(1000) NULL,
    `PhotoData`         LONGBLOB NULL,
    `PhotoContentType`  VARCHAR(100) NULL,
    `DisplayOrder`      INT NOT NULL DEFAULT 0,
    `IsActive`          TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`TestimonialId`),
    KEY `ix_testimonials_active` (`TenantId`, `IsActive`, `DisplayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '058_testimonials.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '058_testimonials.sql');
