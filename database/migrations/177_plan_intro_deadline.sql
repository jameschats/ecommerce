-- =====================================================================
-- 177_plan_intro_deadline.sql  —  Campaign deadline for introductory pricing.
-- IntroEndsAt closes the offer to NEW joiners after that date. Merchants who
-- already started the offer (>=1 paid cycle) keep it for their remaining
-- IntroMonths — we don't yank a promised price out from under them.
-- Additive (V2 band 170+).
-- =====================================================================

ALTER TABLE `Plans` ADD COLUMN `IntroEndsAt` DATETIME NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '177_plan_intro_deadline.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '177_plan_intro_deadline.sql');
