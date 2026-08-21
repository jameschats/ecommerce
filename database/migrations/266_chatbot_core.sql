-- =====================================================================
-- 266_chatbot_core.sql — v4 Phase 2: chatbot orchestration core. The
-- conversation itself already lives in SupportTickets/SupportMessages
-- (ConversationAxis.ShopperMerchant) — a livechat session IS that
-- conversation from message one, nothing new there. This adds only
-- what's genuinely new: per-conversation bot-active/escalated state,
-- and a log of questions the bot couldn't answer (feeds the merchant
-- "what the bot couldn't answer" report).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `ChatbotConversationStates` (
  `ChatbotConversationStateId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `SupportTicketId` BIGINT UNSIGNED NOT NULL,
  `IsBotActive` TINYINT(1) NOT NULL DEFAULT 1,
  `UnresolvedExchangeCount` INT NOT NULL DEFAULT 0,
  `EscalatedAt` DATETIME NULL,
  `EscalationReason` VARCHAR(30) NULL,
  `CreatedAt` DATETIME NOT NULL,
  `UpdatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`ChatbotConversationStateId`),
  UNIQUE KEY `UX_ChatbotConversationStates_Ticket` (`SupportTicketId`),
  CONSTRAINT `FK_ChatbotConversationStates_Tickets` FOREIGN KEY (`SupportTicketId`) REFERENCES `SupportTickets` (`SupportTicketId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ChatbotUnansweredQuestions` (
  `ChatbotUnansweredQuestionId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `TenantId` BIGINT UNSIGNED NOT NULL DEFAULT 1,
  `SupportTicketId` BIGINT UNSIGNED NULL,
  `Question` TEXT NOT NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`ChatbotUnansweredQuestionId`),
  KEY `IX_ChatbotUnansweredQuestions_Tenant` (`TenantId`),
  CONSTRAINT `FK_ChatbotUnansweredQuestions_Tickets` FOREIGN KEY (`SupportTicketId`) REFERENCES `SupportTickets` (`SupportTicketId`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '266_chatbot_core.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '266_chatbot_core.sql');
