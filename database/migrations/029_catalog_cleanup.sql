-- =====================================================================
-- 029_catalog_cleanup.sql  —  Catalog cleanup: drop seed electronics,
-- set cost prices, add + assign the CalendarShop house brand.
--
-- Forward-only and IDEMPOTENT. Keyed by name/category (NOT ids), because
-- prod ids may differ from dev. Safe on a live box:
--   * a "Dell" product that has any order/invoice/credit-note line is LEFT
--     ALONE (never hard-deleted) — only unreferenced seed rows are removed;
--   * cost prices are only filled where NULL, so admin-entered costs survive;
--   * the brand insert + assignment are guarded, so re-running is a no-op.
-- =====================================================================

-- 1) Which non-calendar seed products can be safely removed?
--    (in the 'Laptops'/'Batteries' seed categories AND unreferenced by any sale line)
DROP TEMPORARY TABLE IF EXISTS `_del_products`;
CREATE TEMPORARY TABLE `_del_products` AS
SELECT p.`ProductId`
FROM `Products` p
JOIN `Categories` c ON c.`CategoryId` = p.`CategoryId`
WHERE c.`Name` IN ('Laptops', 'Batteries')
  AND NOT EXISTS (SELECT 1 FROM `OrderItems`      oi WHERE oi.`ProductId` = p.`ProductId`)
  AND NOT EXISTS (SELECT 1 FROM `InvoiceItems`    ii WHERE ii.`ProductId` = p.`ProductId`)
  AND NOT EXISTS (SELECT 1 FROM `CreditNoteItems` ci WHERE ci.`ProductId` = p.`ProductId`);

-- child rows first (variant-linked, then product-linked), then the products
DELETE FROM `VariantOptions`
 WHERE `ProductVariantId` IN (SELECT `ProductVariantId` FROM `ProductVariants`
                              WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`));
DELETE FROM `Inventory`
 WHERE `ProductVariantId` IN (SELECT `ProductVariantId` FROM `ProductVariants`
                              WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`));
DELETE FROM `Inventory`              WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `InventoryTransactions`  WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `ProductSuppliers`       WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `CartItems`              WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `WishlistItems`          WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `Reviews`                WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `ProductVariants`        WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `ProductImages`          WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `ProductAttributeValues` WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);
DELETE FROM `Products`               WHERE `ProductId` IN (SELECT `ProductId` FROM `_del_products`);

DROP TEMPORARY TABLE IF EXISTS `_del_products`;

-- 2) Remove the now-empty seed brand + categories (only if nothing else uses them)
DELETE FROM `Categories`
 WHERE `Name` IN ('Laptops', 'Batteries')
   AND NOT EXISTS (SELECT 1 FROM `Products` p WHERE p.`CategoryId` = `Categories`.`CategoryId`);
DELETE FROM `Brands`
 WHERE `Name` = 'Dell'
   AND NOT EXISTS (SELECT 1 FROM `Products` p WHERE p.`BrandId` = `Brands`.`BrandId`);

-- 3) Fill missing cost prices (only where NULL — respects any admin-entered cost)
UPDATE `Products` SET `CostPrice` = CASE `Name`
    WHEN 'Wall Calendar 2026'            THEN 300.00
    WHEN 'Four Sheeter Wall Calendar'    THEN 360.00
    WHEN 'Premium Themed Wall Calendar'  THEN 540.00
    WHEN 'Desk Calendar 2026'            THEN 210.00
    WHEN 'Desk Calendar with Photo Frame' THEN 245.00
    WHEN 'Perpetual Desk Calendar'       THEN 240.00
    WHEN 'Tent Calendar 2026'            THEN 85.00
    WHEN 'Pocket Calendar 2026'          THEN 80.00
    WHEN 'Magnet Calendar 2026'          THEN 110.00
    WHEN 'Mouse Pad Calendar 2026'       THEN 155.00
    ELSE `CostPrice`
  END
 WHERE `CostPrice` IS NULL
   AND `Name` IN ('Wall Calendar 2026','Four Sheeter Wall Calendar','Premium Themed Wall Calendar',
                  'Desk Calendar 2026','Desk Calendar with Photo Frame','Perpetual Desk Calendar',
                  'Tent Calendar 2026','Pocket Calendar 2026','Magnet Calendar 2026','Mouse Pad Calendar 2026');

-- 4) Add the CalendarShop house brand (once)
INSERT INTO `Brands` (`TenantId`, `Name`, `Slug`, `Description`, `IsActive`)
SELECT 1, 'CalendarShop', 'calendarshop', 'CalendarShop house brand', 1
WHERE NOT EXISTS (SELECT 1 FROM `Brands` WHERE `Slug` = 'calendarshop');

-- 5) Assign the house brand to every product that has no brand yet
UPDATE `Products`
   SET `BrandId` = (SELECT `BrandId` FROM `Brands` WHERE `Slug` = 'calendarshop' LIMIT 1)
 WHERE `BrandId` IS NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '029_catalog_cleanup.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '029_catalog_cleanup.sql');
