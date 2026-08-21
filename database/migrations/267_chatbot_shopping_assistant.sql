-- =====================================================================
-- 267_chatbot_shopping_assistant.sql — v4 Phase 3, Track A: Shopping
-- Assistant capability set on the existing chatbot. LastMentionedProductId
-- lets "add that to my cart" resolve correctly across turns without a
-- new conversation-memory mechanism — the existing per-conversation
-- ChatbotConversationStates row already tracks state, this just adds
-- one more field to it.
-- =====================================================================

SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ChatbotConversationStates' AND COLUMN_NAME = 'LastMentionedProductId');
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `ChatbotConversationStates`
     ADD COLUMN `LastMentionedProductId` BIGINT UNSIGNED NULL AFTER `UnresolvedExchangeCount`',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '267_chatbot_shopping_assistant.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '267_chatbot_shopping_assistant.sql');
