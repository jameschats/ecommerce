-- =====================================================================
-- 254_faqs.sql  —  Per-tenant, merchant-editable FAQs (AI-Support C2).
--
-- The /faq page shipped with seven Q&As hardcoded into the Angular component:
-- identical for every store, uneditable by the merchant, and sometimes wrong
-- (it promises "UPI, cards, net-banking and wallets" to stores that are
-- COD-only, and tells shoppers to sign in to track an order now that /track
-- needs no login).
--
-- FAQs are also the bot's primary retrieval corpus in C4 — the only alternative
-- grounding today is six per-tenant policy blobs that are frequently empty.
-- Free text a merchant maintains is far better material than boilerplate.
--
-- Seeds the existing seven for every tenant so nothing regresses, with the
-- tracking answer corrected.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Faqs` (
    `FaqId`        BIGINT       NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT       NOT NULL DEFAULT 1,
    `Question`     VARCHAR(300) NOT NULL,
    `Answer`       TEXT         NOT NULL,
    `Category`     VARCHAR(60)  NULL,
    `DisplayOrder` INT          NOT NULL DEFAULT 0,
    `IsPublished`  TINYINT(1)   NOT NULL DEFAULT 1,
    `CreatedAt`    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME     NULL,
    PRIMARY KEY (`FaqId`),
    KEY `IX_Faqs_Tenant_Published` (`TenantId`, `IsPublished`, `DisplayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed the previously-hardcoded set for every tenant that has none.
INSERT INTO `Faqs` (`TenantId`, `Question`, `Answer`, `Category`, `DisplayOrder`, `IsPublished`)
SELECT t.`TenantId`, d.`Question`, d.`Answer`, d.`Category`, d.`DisplayOrder`, 1
  FROM `Tenants` t
  CROSS JOIN (
      SELECT 'How do I place an order?' AS Question,
             'Browse our products, add what you like to your cart, then check out with your delivery address and a payment method. You will get an order confirmation right away.' AS Answer,
             'Ordering' AS Category, 1 AS DisplayOrder
      UNION ALL SELECT 'How long does delivery take?',
             'Most orders are dispatched within a few business days. The exact delivery estimate for your location is shown at checkout and in your order confirmation.',
             'Delivery', 2
      UNION ALL SELECT 'What are the shipping charges?',
             'Shipping is calculated at checkout based on your pincode, and many orders qualify for free shipping above a threshold.',
             'Delivery', 3
      UNION ALL SELECT 'What payment methods do you accept?',
             'The payment options available to you are shown at checkout.',
             'Payment', 4
      UNION ALL SELECT 'How do I track my order?',
             'Use the Track Order page with your order number and the email you ordered with — no sign-in needed. You can also see live status under Orders in your account.',
             'Delivery', 5
      UNION ALL SELECT 'Can I return or replace an item?',
             'If an item arrives damaged or defective, or is eligible under our return policy, get in touch within the returns window and we will help with a replacement or refund.',
             'Returns', 6
      UNION ALL SELECT 'How do I contact support?',
             'Head to the Contact page and send us a message — we typically reply within one business day.',
             'Support', 7
  ) d
 WHERE NOT EXISTS (SELECT 1 FROM `Faqs` f WHERE f.`TenantId` = t.`TenantId`);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '254_faqs.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '254_faqs.sql');
