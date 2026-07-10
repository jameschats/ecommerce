-- =====================================================================
-- 169_theme_engine.sql  —  V2 Storefront S1: theme-engine model skeleton.
-- Introduces the "Online Store 2.0" structure on top of the existing Themes +
-- ThemeSettings (key/value) store, which stays the source of truth for global
-- theme settings (colors/typography/buttons/logo). This adds:
--   • Themes.Status/Source/PreviewToken  → theme library + draft/publish + preview
--   • ThemeTemplates                     → one row per page-type (index/product/…)
--   • ThemeSections                      → the ordered sections that compose a template
-- Additive + idempotent (V2 band 160-169). Home content is backfilled into the
-- published theme's `index` template lazily in code (never breaks the storefront).
-- =====================================================================

SET @db := DATABASE();

-- --- Themes: library + publish + preview state ---
SET @sql := IF(NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Themes' AND COLUMN_NAME='Status'),
    'ALTER TABLE `Themes` ADD COLUMN `Status` VARCHAR(16) NOT NULL DEFAULT ''Published''', 'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Themes' AND COLUMN_NAME='Source'),
    'ALTER TABLE `Themes` ADD COLUMN `Source` VARCHAR(64) NULL', 'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(NOT EXISTS(SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Themes' AND COLUMN_NAME='PreviewToken'),
    'ALTER TABLE `Themes` ADD COLUMN `PreviewToken` VARCHAR(64) NULL', 'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Existing active theme → Published; any others → Draft (exactly one Published per tenant is enforced in code).
UPDATE `Themes` SET `Status` = IF(`IsActive` = 1, 'Published', 'Draft');

-- --- ThemeTemplates: one per page-type within a theme ---
CREATE TABLE IF NOT EXISTS `ThemeTemplates` (
    `ThemeTemplateId` BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT       NOT NULL DEFAULT 1,
    `ThemeId`         BIGINT       NOT NULL,
    `TemplateKey`     VARCHAR(32)  NOT NULL,   -- index|product|collection|list-collections|cart|search|404|password|account|header|footer|announcement
    `Name`            VARCHAR(120) NOT NULL DEFAULT 'Default',
    `CreatedAt`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME     NULL,
    PRIMARY KEY (`ThemeTemplateId`),
    UNIQUE KEY `uq_themetemplates_theme_key_name` (`ThemeId`, `TemplateKey`, `Name`),
    KEY `ix_themetemplates_tenant` (`TenantId`),
    KEY `ix_themetemplates_theme` (`ThemeId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --- ThemeSections: PageSection generalised to a template ---
CREATE TABLE IF NOT EXISTS `ThemeSections` (
    `ThemeSectionId`  BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT       NOT NULL DEFAULT 1,
    `ThemeTemplateId` BIGINT       NOT NULL,
    `SectionType`     VARCHAR(48)  NOT NULL,
    `Title`           VARCHAR(160) NULL,
    `Settings`        JSON         NULL,
    `Blocks`          JSON         NULL,
    `DisplayOrder`    INT          NOT NULL DEFAULT 0,
    `IsVisible`       TINYINT(1)   NOT NULL DEFAULT 1,
    `StartsAt`        DATETIME     NULL,
    `EndsAt`          DATETIME     NULL,
    `CreatedAt`       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`       DATETIME     NULL,
    PRIMARY KEY (`ThemeSectionId`),
    KEY `ix_themesections_tenant` (`TenantId`),
    KEY `ix_themesections_template` (`ThemeTemplateId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '169_theme_engine.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '169_theme_engine.sql');
