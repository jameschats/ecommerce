-- =====================================================================
-- 270_dynamic_pricing_feature_on_plans.sql — Turn on Dynamic Pricing
-- (v4 Phase 5) for Pro+ tiers, per the design doc's own "Dynamic
-- Pricing is explicitly a Pro+-tier feature" call. Gated by
-- [RequiresFeature("dynamic-pricing")], reading Plan.Features — same
-- established mechanism migration 242 used to turn on "growth", not a
-- new one. Handles both a NULL/empty Features column and an existing
-- populated array (idempotent either way, preserves anything already there).
-- =====================================================================

UPDATE `Plans`
   SET `Features` = JSON_ARRAY_APPEND(`Features`, '$', 'dynamic-pricing')
 WHERE `Slug` IN ('pro', 'enterprise')
   AND `Features` IS NOT NULL AND `Features` != ''
   AND NOT JSON_CONTAINS(`Features`, '"dynamic-pricing"', '$');

UPDATE `Plans`
   SET `Features` = '["dynamic-pricing"]'
 WHERE `Slug` IN ('pro', 'enterprise')
   AND (`Features` IS NULL OR `Features` = '');

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '270_dynamic_pricing_feature_on_plans.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '270_dynamic_pricing_feature_on_plans.sql');
