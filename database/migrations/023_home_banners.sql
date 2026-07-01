-- =====================================================================
-- 023_home_banners.sql  —  Admin-managed home hero banners
-- Powers the Flipkart-style horizontal banner carousel on the storefront home.
-- Each banner can use an uploaded image (bytes stored in ImageData, served via
-- the API) OR an external ImageUrl. Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `HomeBanners` (
    `HomeBannerId`     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`         BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Title`            VARCHAR(200) NULL,
    `Subtitle`         VARCHAR(300) NULL,
    `CtaText`          VARCHAR(60)  NULL,
    `LinkUrl`          VARCHAR(500) NULL,             -- where the banner links (relative or absolute)
    `ImageUrl`         VARCHAR(1000) NULL,            -- external image URL (used when no upload)
    `ImageData`        LONGBLOB NULL,                 -- uploaded image bytes (served via /api/cms/banners/{id}/image)
    `ImageContentType` VARCHAR(100) NULL,
    `DisplayOrder`     INT NOT NULL DEFAULT 0,
    `IsActive`         TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`HomeBannerId`),
    KEY `ix_homebanners_active` (`TenantId`, `IsActive`, `DisplayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed the 6 current banners (external picsum images) so the storefront keeps
-- showing them until an admin edits/uploads. Guarded so re-runs don't duplicate.
INSERT INTO `HomeBanners` (`TenantId`, `Title`, `Subtitle`, `CtaText`, `LinkUrl`, `ImageUrl`, `DisplayOrder`, `IsActive`)
SELECT * FROM (
    SELECT 1 AS t, 'Customizable 2026 Calendars' AS ti, 'Wall, desk & pocket — with your photos, brand & logo.' AS su, 'Shop calendars' AS c, '/products' AS l, 'https://picsum.photos/seed/calbanner1/900/300' AS im, 1 AS o, 1 AS a UNION ALL
    SELECT 1, 'Corporate Gifting', 'Branded calendars in bulk.', 'Order in bulk', '/products', 'https://picsum.photos/seed/calbanner2/900/300', 2, 1 UNION ALL
    SELECT 1, 'Desk Calendars', 'Elegant picks for any workspace.', 'Browse', '/category/desk-calendars', 'https://picsum.photos/seed/calbanner3/900/300', 3, 1 UNION ALL
    SELECT 1, 'Photo Calendars', 'Turn your memories into a year.', 'Create yours', '/products', 'https://picsum.photos/seed/calbanner4/900/300', 4, 1 UNION ALL
    SELECT 1, 'New-Year Offers', 'Up to 30% off select ranges.', 'Grab deals', '/products', 'https://picsum.photos/seed/calbanner5/900/300', 5, 1 UNION ALL
    SELECT 1, 'Pocket & Tent Calendars', 'Handy formats for every desk.', 'Explore', '/products', 'https://picsum.photos/seed/calbanner6/900/300', 6, 1
) AS seed
WHERE NOT EXISTS (SELECT 1 FROM `HomeBanners` WHERE `TenantId` = 1);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '023_home_banners.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '023_home_banners.sql');
