-- ---------------------------------------------------------------------------
-- 061_category_seo_descriptions.sql — SEO descriptions for the calendar-material
-- categories (design.md's Category.Description already feeds product-list's meta
-- description via product-list.component.ts's applySeo(); every row is empty today),
-- plus a starting value for the new Site.HomeMetaDescription setting.
--
-- Only sets rows/keys that are still blank, so this never overwrites a description
-- someone has since written by hand in admin.
-- ---------------------------------------------------------------------------

-- Homepage meta description (Settings → Shop & payment settings → Homepage meta
-- description). Editable there from now on; this just seeds a real value instead of
-- leaving the field blank until someone visits admin.
INSERT INTO `Settings` (`TenantId`, `SettingKey`, `SettingValue`, `Category`, `Description`, `CreatedAt`)
SELECT 1, 'Site.HomeMetaDescription',
       'Buy factory-direct Lotus daily calendar cake slips, 10x15 art mounts, and fancy cut UV glitter boards from Senthaamarai Press, Sivakasi. Wholesale rates, pan-India shipping.',
       'Site', 'Homepage meta description, shown under the title in search results.', NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Settings` WHERE `TenantId` = 1 AND `SettingKey` = 'Site.HomeMetaDescription');

UPDATE `Categories` SET `Description` =
  'Buy ready-made Lotus daily calendars — finished wall calendars for 2027, printed and bound by Senthaamarai Press, Sivakasi. Fast pan-India delivery.'
  WHERE `Slug` = 'finished-calendar' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus 10x15 Plain Art Mount (PAM) — factory-direct calendar art paper sheets from Senthaamarai Press, Sivakasi. Wholesale rates for dealers and shops.'
  WHERE `Slug` = '10x15-plain-art-mount-pam' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus 10x15 Laminated Art Mount (LAM) — gloss-laminated calendar sheets from Senthaamarai Press, Sivakasi. Durable finish, wholesale pricing.'
  WHERE `Slug` = '10x15-laminated-art-mount-lam' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '6x9 fancy cut daily calendar backing board — die-cut Lotus calendar board from Senthaamarai Press, Sivakasi. Wholesale rates for dealers.'
  WHERE `Slug` = '6x9-fancy-cut-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '10x15 fancy cut daily calendar board — die-cut Lotus backing boards from Senthaamarai Press, Sivakasi. Bulk pricing for calendar dealers.'
  WHERE `Slug` = '10x15-fancy-cut-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus 10x15 Special Gold Foil Board (SGLF) — hot-stamped gold foil calendar mount from Senthaamarai Press, Sivakasi. Premium finish, wholesale rates.'
  WHERE `Slug` = '10x15-special-gold-foil-board-sglf' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus 10x15 Special Gold Board without foil (SGL) — calendar art mount from Senthaamarai Press, Sivakasi, at factory-direct wholesale rates.'
  WHERE `Slug` = '10x15-special-gold-without-foil-board-sgl' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus 10x15 Special Gold Foil with Lamination (SGLFL) — laminated gold-foil calendar mount from Senthaamarai Press, Sivakasi. Wholesale pricing.'
  WHERE `Slug` = '10x15-special-gold-foil-with-lamination-board-sglfl' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '11x17 fancy cut daily calendar board — large-format die-cut Lotus backing board from Senthaamarai Press, Sivakasi. Bulk rates for dealers.'
  WHERE `Slug` = '11x17-fancy-cut-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '11x17 fancy cut foil daily calendar board — die-cut Lotus board with foil finish from Senthaamarai Press, Sivakasi. Wholesale pricing.'
  WHERE `Slug` = '11x17-fancy-cut-foil-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '14x24 fancy cut UV glitter daily calendar board — sparkling die-cut Lotus board from Senthaamarai Press, Sivakasi. Wholesale rates for dealers.'
  WHERE `Slug` = '14x24-fancy-cut-uv-glitter-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '14x40 fancy cut daily calendar board — large-format Lotus backing board from Senthaamarai Press, Sivakasi, at factory-direct wholesale rates.'
  WHERE `Slug` = '14x40-fancy-cut-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '20x28 fancy cut UV glitter daily calendar board — large sparkling Lotus board from Senthaamarai Press, Sivakasi. Bulk wholesale pricing.'
  WHERE `Slug` = '20x28-fancy-cut-uv-glitter-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  '23x36 fancy cut UV glitter daily calendar board — our largest Lotus glitter board, from Senthaamarai Press, Sivakasi. Wholesale rates for dealers.'
  WHERE `Slug` = '23x36-fancy-cut-uv-glitter-board' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus Gold Emboss Bed Foil Sheet — 3D deep-embossed gold foil calendar sheets with devotional designs, from Senthaamarai Press, Sivakasi. Wholesale rates.'
  WHERE `Slug` = 'gold-emboss-bed-foil-sheet' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus daily calendar cake slips — tear-off refill pads with Tamil Panchangam, from Senthaamarai Press, Sivakasi. Factory-direct wholesale rates.'
  WHERE `Slug` = 'cake-slips' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus Panchangam Sheet — daily Tamil almanac calendar inserts from Senthaamarai Press, Sivakasi. Wholesale rates for dealers and shops.'
  WHERE `Slug` = 'panchangam-sheet' AND (`Description` IS NULL OR `Description` = '');

UPDATE `Categories` SET `Description` =
  'Lotus daily calendar base boards — laminated mounting boards (LBB, LDB, smart board) from Senthaamarai Press, Sivakasi. Wholesale pricing.'
  WHERE `Slug` = 'calendar-board' AND (`Description` IS NULL OR `Description` = '');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '061_category_seo_descriptions.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '061_category_seo_descriptions.sql');
