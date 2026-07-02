-- =====================================================================
-- 025_wishlist.sql  —  Stage 7: customer wishlist / save-for-later
-- One row per (user, product). Product-level (variant-agnostic) for V1.
-- Forward-only; idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `WishlistItems` (
    `WishlistItemId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`         BIGINT UNSIGNED NOT NULL,
    `ProductId`      BIGINT UNSIGNED NOT NULL,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`WishlistItemId`),
    UNIQUE KEY `uq_wishlist_user_product` (`TenantId`, `UserId`, `ProductId`),
    KEY `ix_wishlist_user` (`UserId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '025_wishlist.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '025_wishlist.sql');
