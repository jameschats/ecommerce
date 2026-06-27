-- =====================================================================
-- 010_theme.sql  —  Theme engine domain (V1.1)
-- Tables: Themes, ThemeSettings
-- Admin-configurable look & feel (colors, logo, font, button style).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Themes` (
    `ThemeId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`  BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Name`      VARCHAR(100) NOT NULL,
    `IsActive`  TINYINT(1) NOT NULL DEFAULT 0,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ThemeId`),
    UNIQUE KEY `uq_themes_tenant_name` (`TenantId`, `Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ThemeSettings` (
    `ThemeSettingId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ThemeId`        BIGINT UNSIGNED NOT NULL,
    `SettingKey`     VARCHAR(50)  NOT NULL,   -- PrimaryColor|SecondaryColor|Logo|Font|ButtonStyle
    `SettingValue`   VARCHAR(500) NULL,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ThemeSettingId`),
    UNIQUE KEY `uq_themesettings_theme_key` (`ThemeId`, `SettingKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '010_theme.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '010_theme.sql');
