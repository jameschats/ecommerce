-- =====================================================================
-- 019_inventory_seed.sql  —  Stock levels for the calendar catalog (demo data)
-- Product-level inventory (ProductVariantId NULL). A few are low-stock / OOS.
-- =====================================================================

INSERT INTO `Inventory` (`TenantId`, `ProductId`, `ProductVariantId`, `AvailableQty`, `ReservedQty`, `ReorderLevel`, `CreatedAt`)
SELECT 1, p.`ProductId`, NULL, v.avail, 0, v.reorder, CURRENT_TIMESTAMP
FROM `Products` p
JOIN (
  SELECT 'WALL-2026'      AS sku, 120 AS avail, 20 AS reorder UNION ALL
  SELECT 'WALL-4SHEET',    80,  15 UNION ALL
  SELECT 'WALL-PREMIUM',   30,  10 UNION ALL
  SELECT 'DESK-2026',      95,  20 UNION ALL
  SELECT 'DESK-PHOTO',     40,  10 UNION ALL
  SELECT 'DESK-PERPETUAL', 8,   10 UNION ALL   -- low stock
  SELECT 'TENT-2026',      200, 30 UNION ALL
  SELECT 'POCKET-2026',    500, 50 UNION ALL
  SELECT 'MAGNET-2026',    5,   10 UNION ALL   -- low stock
  SELECT 'MOUSEPAD-2026',  0,   5              -- out of stock
) v ON v.sku = p.`Sku`
WHERE p.`TenantId` = 1
  AND NOT EXISTS (SELECT 1 FROM `Inventory` i WHERE i.`ProductId` = p.`ProductId` AND i.`ProductVariantId` IS NULL);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '019_inventory_seed.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '019_inventory_seed.sql');
