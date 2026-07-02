-- =====================================================================
-- 027_notifications.sql  —  In-app notification feed (bell icon)
-- Distinct from NotificationHistory (outbound email/SMS log). Per-user for
-- customers (UserId set), shared for admins (UserId NULL, Audience=Admin).
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Notifications` (
    `NotificationId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`         BIGINT UNSIGNED NULL,             -- NULL = admin/role feed
    `Audience`       VARCHAR(20) NOT NULL,             -- Customer | Admin
    `Type`           VARCHAR(50) NOT NULL,             -- OrderUpdate | NewOrder | PendingReview | LowStock
    `Title`          VARCHAR(200) NOT NULL,
    `Message`        VARCHAR(500) NULL,
    `LinkUrl`        VARCHAR(300) NULL,
    `IsRead`         TINYINT(1) NOT NULL DEFAULT 0,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`NotificationId`),
    KEY `ix_notifications_user` (`TenantId`, `UserId`, `IsRead`),
    KEY `ix_notifications_audience` (`TenantId`, `Audience`, `IsRead`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '027_notifications.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '027_notifications.sql');
