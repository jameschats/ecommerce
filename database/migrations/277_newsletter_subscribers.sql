-- =====================================================================
-- 277_newsletter_subscribers.sql  —  Storefront newsletter signup capture.
-- One row per email per tenant. Tenant-scoped. Additive.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `NewsletterSubscribers` (
    `NewsletterSubscriberId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`  BIGINT UNSIGNED NOT NULL,
    `Email`     VARCHAR(256) NOT NULL,
    `Source`    VARCHAR(50) NULL,
    `CreatedAt` DATETIME NOT NULL,
    PRIMARY KEY (`NewsletterSubscriberId`),
    UNIQUE KEY `uq_newsletter_tenant_email` (`TenantId`, `Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '277_newsletter_subscribers.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '277_newsletter_subscribers.sql');
