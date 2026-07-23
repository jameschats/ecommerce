-- =====================================================================
-- 242_growth_feature_on_plans.sql  —  Turn on AI Growth for paid tiers (G1).
--
-- The generation endpoints are gated by [RequiresFeature("growth")], which
-- reads Plan.Features. Every plan shipped with Features = NULL, so without
-- this the feature is invisible to everyone. Per the pricing model
-- (documents/ai-growth/README.md), AI Text Marketing unlocks on the mid and
-- high tiers; Starter shows an upgrade card instead.
--
-- Only sets plans still on NULL, so a hand-edited Features value is preserved.
-- =====================================================================

UPDATE `Plans`
   SET `Features` = '["growth"]'
 WHERE `Slug` IN ('growth', 'pro', 'enterprise')
   AND (`Features` IS NULL OR `Features` = '');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '242_growth_feature_on_plans.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '242_growth_feature_on_plans.sql');
