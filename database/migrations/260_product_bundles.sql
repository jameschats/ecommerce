-- =====================================================================
-- 260_product_bundles.sql  —  Bundles/combos as merchandised SKUs (Phase H)
--
-- A bundle is just a Product (IsBundle=1) — it reuses the whole catalog/PDP/
-- cart/order pipeline for free (own price, images, slug, description). What
-- makes it a bundle is BundleItems: the real products+quantities that get
-- reserved/committed/released from inventory when the bundle sells, instead
-- of the bundle "product" having its own stock. Fixed composition only (no
-- per-slot choice) — see the Phase H design note in the roadmap doc.
-- =====================================================================

ALTER TABLE `Products`
  ADD COLUMN `IsBundle` TINYINT(1) NOT NULL DEFAULT 0 AFTER `ProductType`;

CREATE TABLE `BundleItems` (
  `BundleItemId` BIGINT NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT NOT NULL DEFAULT 1,
  `BundleProductId` BIGINT NOT NULL,
  `ComponentProductId` BIGINT NOT NULL,
  `ComponentVariantId` BIGINT NULL,
  `Quantity` INT NOT NULL DEFAULT 1,
  PRIMARY KEY (`BundleItemId`),
  KEY `IX_BundleItems_BundleProductId` (`BundleProductId`),
  CONSTRAINT `FK_BundleItems_Bundle` FOREIGN KEY (`BundleProductId`) REFERENCES `Products` (`ProductId`) ON DELETE CASCADE,
  CONSTRAINT `FK_BundleItems_Component` FOREIGN KEY (`ComponentProductId`) REFERENCES `Products` (`ProductId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '260_product_bundles.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '260_product_bundles.sql');
