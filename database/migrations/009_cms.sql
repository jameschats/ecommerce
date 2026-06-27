-- =====================================================================
-- 009_cms.sql  —  CMS / Home-page builder domain (V1.1)
-- Tables: Pages, PageSections, SectionConfigurations
-- Admin composes a page from ordered, schedulable, hideable sections.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Pages` (
    `PageId`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Title`           VARCHAR(200) NOT NULL,
    `Slug`            VARCHAR(220) NOT NULL,
    `Type`            VARCHAR(20) NOT NULL DEFAULT 'Custom',  -- Home | Custom
    `IsPublished`     TINYINT(1) NOT NULL DEFAULT 0,
    `MetaTitle`       VARCHAR(200) NULL,
    `MetaDescription` VARCHAR(500) NULL,
    `CreatedAt`       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`PageId`),
    UNIQUE KEY `uq_pages_tenant_slug` (`TenantId`, `Slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `PageSections` (
    `PageSectionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `PageId`        BIGINT UNSIGNED NOT NULL,
    `SectionType`   VARCHAR(30) NOT NULL,  -- Banner|FeaturedProducts|Categories|Offers|NewArrivals|BestSellers|CustomHtml
    `Title`         VARCHAR(200) NULL,
    `DisplayOrder`  INT NOT NULL DEFAULT 0,
    `IsVisible`     TINYINT(1) NOT NULL DEFAULT 1,
    `StartsAt`      DATETIME NULL,         -- scheduled visibility window
    `EndsAt`        DATETIME NULL,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`     DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`PageSectionId`),
    KEY `ix_pagesections_page` (`PageId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SectionConfigurations` (
    `SectionConfigurationId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `PageSectionId`          BIGINT UNSIGNED NOT NULL,
    `ConfigKey`              VARCHAR(100) NOT NULL,
    `ConfigValue`            TEXT NULL,
    `CreatedAt`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`              DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`SectionConfigurationId`),
    KEY `ix_sectionconfigurations_section` (`PageSectionId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '009_cms.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '009_cms.sql');
