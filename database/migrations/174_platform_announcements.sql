-- =====================================================================
-- 174_platform_announcements.sql  —  Super-admin SA9: platform broadcasts.
-- A platform owner posts an announcement; every merchant sees it as a banner
-- in their admin until dismissed / deactivated / expired. Platform-level
-- (NOT tenant-scoped, like PlatformAccessLog). Additive (V2 band 170+).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `PlatformAnnouncements` (
    `PlatformAnnouncementId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `Title`     VARCHAR(200)  NOT NULL,
    `Body`      VARCHAR(2000) NOT NULL,
    `Level`     VARCHAR(20)   NOT NULL DEFAULT 'info',   -- info | warning | critical
    `IsActive`  TINYINT(1)    NOT NULL DEFAULT 1,
    `StartsAt`  DATETIME      NULL,
    `EndsAt`    DATETIME      NULL,
    `CreatedAt` DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME      NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`PlatformAnnouncementId`),
    KEY `ix_announcement_active` (`IsActive`, `PlatformAnnouncementId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '174_platform_announcements.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '174_platform_announcements.sql');
