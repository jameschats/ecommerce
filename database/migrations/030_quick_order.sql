-- ---------------------------------------------------------------------------
-- 030_quick_order.sql — Phase 1 quick-order checkout
--
-- See documents/stages-v2/design.md §6–7.
--   1) StateMinOrderAmounts — minimum order value per delivery state
--   2) Settings rows        — packing charge %, UPI + bank details, announcement bar
--   3) Products.SortOrder   — lets the catalogue spreadsheet control row order in a band
--
-- Forward-only. Safe to re-run: every statement guards on existence.
-- ---------------------------------------------------------------------------

-- 1) Minimum order amount per state --------------------------------------------------
--
-- A single global minimum lives in Settings (`QuickOrder.MinOrderAmount`). Rows here
-- override it for a specific state. Empty table = the global value applies everywhere,
-- which is the default until the business says otherwise (design.md Q1).
CREATE TABLE IF NOT EXISTS `StateMinOrderAmounts` (
  `StateMinOrderAmountId` BIGINT       NOT NULL AUTO_INCREMENT,
  `TenantId`              BIGINT       NOT NULL DEFAULT 1,
  `StateName`             VARCHAR(100) NOT NULL,
  `MinOrderAmount`        DECIMAL(12,2) NOT NULL DEFAULT 0.00,
  `IsActive`              TINYINT(1)   NOT NULL DEFAULT 1,
  `CreatedAt`             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt`             DATETIME     NULL,
  PRIMARY KEY (`StateMinOrderAmountId`),
  UNIQUE KEY `UX_StateMinOrder_Tenant_State` (`TenantId`, `StateName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- 2) Settings ------------------------------------------------------------------------
--
-- Defaults are deliberately inert: zero minimum, zero packing charge, empty payment
-- details. Nothing here changes what a customer sees until an admin fills it in.
INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT * FROM (
  SELECT 'QuickOrder.MinOrderAmount'   AS k, '0'  AS v, NOW() AS c UNION ALL
  SELECT 'QuickOrder.PackingChargePct',      '0',      NOW()      UNION ALL
  SELECT 'QuickOrder.RoundOffEnabled',       'true',   NOW()      UNION ALL
  SELECT 'QuickOrder.AnnouncementText',      '',       NOW()      UNION ALL
  SELECT 'QuickOrder.PriceValidUpto',        '',       NOW()      UNION ALL
  SELECT 'Payment.UpiId',                    '',       NOW()      UNION ALL
  SELECT 'Payment.UpiPayeeName',             '',       NOW()      UNION ALL
  SELECT 'Payment.BankAccountName',          '',       NOW()      UNION ALL
  SELECT 'Payment.BankAccountNumber',        '',       NOW()      UNION ALL
  SELECT 'Payment.BankIfsc',                 '',       NOW()      UNION ALL
  SELECT 'Payment.BankName',                 '',       NOW()
) AS seed
WHERE NOT EXISTS (
  SELECT 1 FROM `Settings` s WHERE s.`SettingKey` = seed.k
);


-- 3) Products.SortOrder --------------------------------------------------------------
--
-- Row order within a category band. Sorted by name until the catalogue import supplies
-- explicit ordering, so 0 everywhere is a no-op.
SET @col := (
  SELECT COUNT(*) FROM information_schema.columns
   WHERE table_schema = DATABASE() AND table_name = 'Products' AND column_name = 'SortOrder'
);
SET @sql := IF(@col = 0,
  'ALTER TABLE `Products` ADD COLUMN `SortOrder` INT NOT NULL DEFAULT 0 AFTER `IsFeatured`',
  'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;


INSERT INTO `__schema_migrations` (`script_name`)
SELECT '030_quick_order.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '030_quick_order.sql');
