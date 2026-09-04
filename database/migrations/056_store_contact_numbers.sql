-- =====================================================================
-- 056_store_contact_numbers.sql  —  Split Store.Phone into two mobiles + two landlines
-- Settings is an EAV key/value table, so this needs no ALTER TABLE — just a one-time copy
-- of whatever is in the old Store.Phone key into the new Store.Mobile1, so a number already
-- configured survives the switch instead of appearing to vanish. Store.Phone itself is left
-- in place, just unused from here on. Forward-only; idempotent.
-- =====================================================================

INSERT INTO `Settings` (`SettingKey`, `SettingValue`, `CreatedAt`)
SELECT 'Store.Mobile1', `SettingValue`, NOW()
FROM `Settings`
WHERE `SettingKey` = 'Store.Phone' AND `SettingValue` IS NOT NULL AND `SettingValue` <> ''
  AND NOT EXISTS (SELECT 1 FROM `Settings` WHERE `SettingKey` = 'Store.Mobile1');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '056_store_contact_numbers.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '056_store_contact_numbers.sql');
