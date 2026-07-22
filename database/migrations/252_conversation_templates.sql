-- =====================================================================
-- 252_conversation_templates.sql
--
-- TWO THINGS:
--
-- 1. FIXES A LIVE BUG. NotificationTemplates are per-tenant, but only tenant 1
--    was ever seeded (migration 024) and onboarding never copied them. Every
--    store created through signup therefore has ZERO templates, and
--    NotificationService silently logs "no active template" and returns false —
--    so those merchants' customers have never received an order confirmation,
--    shipping notice, cancellation or password-reset email. Verified on this
--    database: tenants 2/3/4 have 0 templates and 0 notification history.
--    This backfills every missing (Code, Channel) for every tenant, copying the
--    canonical wording from tenant 1. OnboardingService seeds new tenants from
--    the same source going forward.
--
-- 2. Adds the conversation templates (AI-Support C1). Without ConversationReply
--    an anonymous shopper is never told the merchant answered — they hold a
--    reply token nothing ever delivers.
--
-- Idempotent: UNIQUE (TenantId, Code, Channel) + INSERT IGNORE / NOT EXISTS.
-- Tokens: {{CustomerName}} {{StoreName}} {{Subject}} {{MessagePreview}}
--         {{ThreadUrl}} {{Reference}}
-- =====================================================================

-- ---- 1. Canonical conversation templates on tenant 1 ----
INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`) VALUES
(1, 'ConversationReply', 'Email', '{{StoreName}} replied to your message',
 '<p>Hi {{CustomerName}},</p><p>{{StoreName}} has replied to your message <b>{{Subject}}</b>:</p><blockquote>{{MessagePreview}}</blockquote><p><a href="{{ThreadUrl}}">Read and reply</a></p><p>Reference: {{Reference}}</p><p>— {{StoreName}}</p>', 1),

(1, 'ConversationReceived', 'Email', 'We received your message',
 '<p>Hi {{CustomerName}},</p><p>Thanks for getting in touch — we have your message <b>{{Subject}}</b> and will reply soon.</p><p><a href="{{ThreadUrl}}">View the conversation</a></p><p>Reference: {{Reference}}</p><p>— {{StoreName}}</p>', 1);

-- ---- 2. Backfill every template tenant 1 has to every other tenant ----
INSERT IGNORE INTO `NotificationTemplates` (`TenantId`, `Code`, `Channel`, `Subject`, `Body`, `IsActive`)
SELECT t.`TenantId`, s.`Code`, s.`Channel`, s.`Subject`, s.`Body`, s.`IsActive`
  FROM `Tenants` t
  CROSS JOIN `NotificationTemplates` s
 WHERE s.`TenantId` = 1
   AND t.`TenantId` <> 1
   AND NOT EXISTS (
        SELECT 1 FROM `NotificationTemplates` x
         WHERE x.`TenantId` = t.`TenantId` AND x.`Code` = s.`Code` AND x.`Channel` = s.`Channel`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '252_conversation_templates.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '252_conversation_templates.sql');
