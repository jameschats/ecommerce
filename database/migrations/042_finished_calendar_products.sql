-- ---------------------------------------------------------------------------
-- 042_finished_calendar_products.sql — two starter products for Finished Calendar
--
-- 041 created the category and its page, but nothing sits in it, so the page shows
-- its empty state. These two give it content. They are placeholders in every respect
-- that matters commercially — names, design numbers, prices and stock are meant to be
-- edited or replaced in admin.
--
-- The category is looked up by slug, never by id: it is CategoryId 16 on the local
-- database and 24 in production, so a hardcoded number would silently file these
-- under whatever category happened to hold that id.
--
-- Inventory rows are part of the seed on purpose. The price list derives InStock from
-- SUM(AvailableQty), so a product with no inventory row renders with an "Out of stock"
-- badge — which would look like a bug rather than a fresh catalogue.
-- ---------------------------------------------------------------------------

-- 1) The products ---------------------------------------------------------------------
INSERT INTO `Products`
  (`TenantId`, `CategoryId`, `Sku`, `DesignNo`, `Name`, `Slug`,
   `Price`, `CompareAtPrice`, `HsnCode`, `Status`, `IsActive`, `IsDeleted`, `CreatedAt`)
SELECT 1, c.`CategoryId`, seed.sku, seed.sku, seed.nm, seed.sl,
       seed.price, seed.mrp, '4910', 'Active', 1, 0, NOW()
FROM (
  SELECT '901' AS sku, 'Finished Wall Calendar 2027 - 12 Sheet' AS nm,
         'finished-wall-calendar-2027-12-sheet' AS sl, 95.00 AS price, 120.00 AS mrp
  UNION ALL
  SELECT '902', 'Finished Desk Calendar 2027 - Tent',
         'finished-desk-calendar-2027-tent',      55.00,       70.00
) AS seed
JOIN `Categories` c ON c.`Slug` = 'finished-calendar'
WHERE NOT EXISTS (
  SELECT 1 FROM `Products` p WHERE p.`TenantId` = 1 AND p.`Sku` = seed.sku
);

-- 2) Stock, so neither row reads "Out of stock" ----------------------------------------
INSERT INTO `Inventory` (`TenantId`, `ProductId`, `AvailableQty`, `ReservedQty`, `ReorderLevel`, `CreatedAt`)
SELECT 1, p.`ProductId`, 100, 0, 10, NOW()
FROM `Products` p
WHERE p.`TenantId` = 1
  AND p.`Sku` IN ('901', '902')
  AND NOT EXISTS (SELECT 1 FROM `Inventory` i WHERE i.`ProductId` = p.`ProductId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '042_finished_calendar_products.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '042_finished_calendar_products.sql');
