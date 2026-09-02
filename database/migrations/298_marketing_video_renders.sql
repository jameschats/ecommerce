-- =====================================================================
-- 298_marketing_video_renders.sql — Marketing Studio MS3·c: the reel render
-- job queue. Enqueuing a reel writes a row here; a background job (FFmpeg in
-- the API) generates the voiceover, assembles the MP4 and stores it. Marketing*
-- cluster, no FKs into core commerce tables. Idempotent.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `MarketingVideoRenders` (
    `MarketingVideoRenderId` BIGINT NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT      NOT NULL,
    `Status`         VARCHAR(20) NOT NULL DEFAULT 'queued',
    `Aspect`         VARCHAR(10) NOT NULL DEFAULT '9:16',
    `Width`          INT         NOT NULL,
    `Height`         INT         NOT NULL,
    `ScenesJson`     TEXT        NOT NULL,
    `Narration`      TEXT        NULL,
    `LanguageCode`   VARCHAR(10) NOT NULL DEFAULT 'en-IN',
    `MusicMood`      VARCHAR(30) NOT NULL DEFAULT 'upbeat',
    `IncludeMusic`   TINYINT(1)  NOT NULL DEFAULT 0,
    `OutputMediaUrl` VARCHAR(500) NULL,
    `Error`          VARCHAR(1000) NULL,
    `CreatedAt`      DATETIME(6) NOT NULL,
    `UpdatedAt`      DATETIME(6) NULL,
    PRIMARY KEY (`MarketingVideoRenderId`),
    KEY `IX_MarketingVideoRenders_Tenant_Status` (`TenantId`, `Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '298_marketing_video_renders.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '298_marketing_video_renders.sql');
