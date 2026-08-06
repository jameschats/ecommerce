-- ---------------------------------------------------------------------------
-- 047_campaigns.sql — promotional email campaigns
--
-- Builds on 046: the audience is Contacts who opted in, plus customers who did.
-- Nothing here invents a second capture mechanism.
--
-- CampaignRecipients exists so a send is resumable and auditable. Without a row per
-- recipient, a send that dies halfway has no way to know who already received it, and
-- re-running it mails those people twice — the one mistake a promotional send cannot
-- take back.
--
-- Sending is deliberately synchronous and batched from the admin screen rather than
-- queued through a background worker. The only IHostedService in this app is the admin
-- seeder; adding a scheduler for a shop that sends occasionally would be machinery to
-- maintain for no gain. Resumability comes from the recipient rows instead.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `Campaigns` (
    `CampaignId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL DEFAULT 1,

    `Name`         VARCHAR(200) NOT NULL,
    `Subject`      VARCHAR(250) NOT NULL,
    `Body`         MEDIUMTEXT   NOT NULL,

    -- Contacts | Customers | Both — resolved to recipients when the send starts, so a
    -- campaign records who it actually went to rather than a query that drifts afterwards.
    `Audience`     VARCHAR(20)  NOT NULL DEFAULT 'Contacts',

    -- Draft | Sending | Sent | Failed
    `Status`       VARCHAR(20)  NOT NULL DEFAULT 'Draft',

    `TotalRecipients` INT NOT NULL DEFAULT 0,
    `SentCount`       INT NOT NULL DEFAULT 0,
    `FailedCount`     INT NOT NULL DEFAULT 0,

    `StartedAt`    DATETIME NULL,
    `CompletedAt`  DATETIME NULL,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (`CampaignId`),
    KEY `ix_campaigns_tenant_status` (`TenantId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `CampaignRecipients` (
    `CampaignRecipientId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `CampaignId`   BIGINT UNSIGNED NOT NULL,
    `Email`        VARCHAR(200) NOT NULL,
    `Name`         VARCHAR(150) NULL,

    -- Pending | Sent | Failed
    `Status`       VARCHAR(20)  NOT NULL DEFAULT 'Pending',
    `Error`        VARCHAR(500) NULL,
    `SentAt`       DATETIME NULL,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (`CampaignRecipientId`),
    -- One row per address per campaign. This is what makes a resumed send safe: a repeat
    -- insert cannot create a second copy, so nobody is mailed twice.
    UNIQUE KEY `uq_campaign_recipient` (`CampaignId`, `Email`),
    KEY `ix_campaign_recipients_status` (`CampaignId`, `Status`),
    CONSTRAINT `fk_campaign_recipients_campaign`
        FOREIGN KEY (`CampaignId`) REFERENCES `Campaigns` (`CampaignId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '047_campaigns.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '047_campaigns.sql');
