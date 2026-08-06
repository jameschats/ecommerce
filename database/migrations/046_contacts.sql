-- ---------------------------------------------------------------------------
-- 046_contacts.sql — visitor enquiries, kept
--
-- The contact page has always been a stub. Its own template admitted it:
-- "This is a demo form — submissions aren't stored yet." submit() set a signal
-- and cleared the boxes. Every enquiry anyone has ever sent through the site is
-- gone, with no record that it happened.
--
-- This is also the list a campaign later sends to, so it holds a consent flag from
-- the start rather than having one bolted on once a mailing has already gone out.
--
-- Status is a plain string, matching how Orders and Payments already do it, rather
-- than a lookup table for four values.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `Contacts` (
    `ContactId`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,

    `Name`           VARCHAR(150) NOT NULL,
    `Email`          VARCHAR(200) NULL,
    `Phone`          VARCHAR(20)  NULL,
    `Subject`        VARCHAR(250) NULL,
    `Message`        TEXT         NULL,

    -- Where it came from: 'ContactForm' today, 'Order' or 'Import' later. Kept so a
    -- campaign can address enquirers and buyers differently.
    `Source`         VARCHAR(50)  NOT NULL DEFAULT 'ContactForm',
    `SourcePage`     VARCHAR(200) NULL,

    -- New | Open | Closed | Spam
    `Status`         VARCHAR(20)  NOT NULL DEFAULT 'New',
    `AdminNotes`     TEXT         NULL,

    -- Set when a signed-in visitor writes in, so their enquiries and their orders
    -- can be seen together. Null for anonymous senders, which is most of them.
    `UserId`         BIGINT UNSIGNED NULL,

    -- Marketing consent. Defaults to 0: someone asking a question has not asked to
    -- be mailed, and assuming otherwise is how a shop ends up sending spam.
    `SubscribedToEmails` TINYINT(1) NOT NULL DEFAULT 0,

    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (`ContactId`),
    KEY `ix_contacts_tenant_status` (`TenantId`, `Status`),
    KEY `ix_contacts_created` (`CreatedAt`),
    KEY `ix_contacts_email` (`Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '046_contacts.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '046_contacts.sql');
