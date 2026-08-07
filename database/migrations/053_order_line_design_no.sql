-- ---------------------------------------------------------------------------
-- 053_order_line_design_no.sql — design number on the order and invoice line
--
-- The invoice prints a Design No. column in place of HSN. The design number lives
-- only on Products, and reading it at print time would be wrong for the same reason
-- the profit reports could not read CostPrice at report time: this catalogue was
-- edited in place from its demo seed, so a ProductId from July points at a different
-- item today. Product 1 was "Wall Calendar 2026" at ₹660 and is now
-- "10 x 15 Art Mount Lamination" at ₹4. Reprinting an old invoice would put a design
-- number against goods that were never sold under it.
--
-- So the line records it at sale time, like UnitCost (028).
--
-- The backfill is deliberately narrow: it fills only rows where the line's stored
-- ProductName still equals the product's current name. That match is the evidence the
-- item was not repurposed. Everything else stays NULL and prints blank, which is
-- honest — a blank column asks a question, a wrong number answers one incorrectly.
-- ---------------------------------------------------------------------------

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderItems' AND COLUMN_NAME = 'DesignNo');
SET @sql := IF(@c = 0,
  'ALTER TABLE `OrderItems` ADD COLUMN `DesignNo` VARCHAR(50) NULL AFTER `ProductName`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'DesignNo');
SET @sql := IF(@c = 0,
  'ALTER TABLE `InvoiceItems` ADD COLUMN `DesignNo` VARCHAR(50) NULL AFTER `ProductName`',
  'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- Only where the product is demonstrably still the same product.
UPDATE `OrderItems` `oi`
  JOIN `Products` `p` ON `p`.`ProductId` = `oi`.`ProductId`
   SET `oi`.`DesignNo` = `p`.`DesignNo`
 WHERE `oi`.`DesignNo` IS NULL
   AND `p`.`DesignNo` IS NOT NULL
   AND `oi`.`ProductName` = `p`.`Name`;

UPDATE `InvoiceItems` `ii`
  JOIN `Products` `p` ON `p`.`ProductId` = `ii`.`ProductId`
   SET `ii`.`DesignNo` = `p`.`DesignNo`
 WHERE `ii`.`DesignNo` IS NULL
   AND `p`.`DesignNo` IS NOT NULL
   AND `ii`.`ProductName` = `p`.`Name`;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '053_order_line_design_no.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '053_order_line_design_no.sql');
