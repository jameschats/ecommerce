-- =====================================================================
-- 176_plan_intro_pricing.sql  —  Introductory (onboarding) pricing per plan.
-- A plan can bill a promotional price for the first N paid cycles, then revert
-- to MonthlyPrice. e.g. IntroPriceInr=20, IntroMonths=3 => "First 3 months at
-- Rs.20/mo". Eligibility is derived from the tenant's paid-charge count, so no
-- extra per-tenant tracking is needed. Additive (V2 band 170+).
-- =====================================================================

ALTER TABLE `Plans` ADD COLUMN `IntroPriceInr` DECIMAL(10,2) NULL;
ALTER TABLE `Plans` ADD COLUMN `IntroMonths`   INT           NULL;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '176_plan_intro_pricing.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '176_plan_intro_pricing.sql');
