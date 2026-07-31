-- =====================================================================
-- 257_plan_storage_and_labels.sql  —  Add Storage as a real enforced plan
-- limit, plus free-text pricing-page comparison labels for Marketing
-- Engine / Live Chat / Helpdesk (none of these are built features yet —
-- the labels are for plan comparison only, no functional gating).
--
-- MaxStorageMb mirrors MaxProducts/MaxOrders: NULL = unlimited. Existing
-- MonthlyPrice/MaxProducts/MaxOrders/AiCredits are untouched — only these
-- four new columns are added and seeded.
-- =====================================================================

ALTER TABLE `Plans`
  ADD COLUMN `MaxStorageMb` INT NULL AFTER `MaxOrders`,
  ADD COLUMN `MarketingEngineLevel` VARCHAR(20) NULL AFTER `Features`,
  ADD COLUMN `LiveChatLevel` VARCHAR(20) NULL AFTER `MarketingEngineLevel`,
  ADD COLUMN `HelpdeskLevel` VARCHAR(20) NULL AFTER `LiveChatLevel`;

UPDATE `Plans` SET MaxStorageMb = 5120,   MarketingEngineLevel = 'No',       LiveChatLevel = 'No',  HelpdeskLevel = 'No'       WHERE Slug = 'starter';
UPDATE `Plans` SET MaxStorageMb = 25600,  MarketingEngineLevel = 'Yes',      LiveChatLevel = 'Yes', HelpdeskLevel = 'Basic'    WHERE Slug = 'growth';
UPDATE `Plans` SET MaxStorageMb = 102400, MarketingEngineLevel = 'Advanced', LiveChatLevel = 'Yes', HelpdeskLevel = 'Advanced' WHERE Slug = 'pro';
UPDATE `Plans` SET MaxStorageMb = NULL,   MarketingEngineLevel = 'Advanced', LiveChatLevel = 'Yes', HelpdeskLevel = 'Advanced' WHERE Slug = 'enterprise';

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '257_plan_storage_and_labels.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '257_plan_storage_and_labels.sql');
