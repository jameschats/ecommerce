-- =====================================================================
-- 028_orderitem_unitcost.sql  —  Analytics: cost snapshot at sale time
-- Cost prices change over time, so historical margin must use the cost as it
-- was when the order was placed (just like we snapshot UnitPrice). Forward-only.
-- =====================================================================

ALTER TABLE `OrderItems`
    ADD COLUMN `UnitCost` DECIMAL(12,2) NULL AFTER `UnitPrice`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '028_orderitem_unitcost.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '028_orderitem_unitcost.sql');
