-- =====================================================================
-- 011_platform.sql  —  Platform / cross-cutting domain
-- Tables: Settings, MediaFolders, MediaFiles, NotificationTemplates,
--         NotificationHistory, AuditLogs, SearchLogs, PopularSearches,
--         ImportJobs, ImportJobItems
-- These are foundation services wired from Day 1 (Stage 0).
-- =====================================================================

CREATE TABLE IF NOT EXISTS `Settings` (
    `SettingId`    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `SettingKey`   VARCHAR(100) NOT NULL,
    `SettingValue` TEXT NULL,
    `DataType`     VARCHAR(20) NOT NULL DEFAULT 'string',  -- string|int|bool|json
    `Category`     VARCHAR(50) NULL,
    `Description`  VARCHAR(255) NULL,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`    DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`SettingId`),
    UNIQUE KEY `uq_settings_tenant_key` (`TenantId`, `SettingKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `MediaFolders` (
    `MediaFolderId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`       BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `ParentFolderId` BIGINT UNSIGNED NULL,
    `Name`           VARCHAR(150) NOT NULL,
    `Path`           VARCHAR(500) NULL,
    `CreatedAt`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`      DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`MediaFolderId`),
    KEY `ix_mediafolders_parent` (`ParentFolderId`),
    CONSTRAINT `fk_mediafolders_parent` FOREIGN KEY (`ParentFolderId`) REFERENCES `MediaFolders` (`MediaFolderId`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `MediaFiles` (
    `MediaFileId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`      BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `MediaFolderId` BIGINT UNSIGNED NULL,
    `FileName`      VARCHAR(255) NOT NULL,
    `OriginalName`  VARCHAR(255) NULL,
    `MimeType`      VARCHAR(100) NULL,
    `SizeBytes`     BIGINT UNSIGNED NULL,
    `Url`           VARCHAR(500) NOT NULL,
    `Width`         INT NULL,
    `Height`        INT NULL,
    `CreatedBy`     BIGINT UNSIGNED NULL,
    `CreatedAt`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`MediaFileId`),
    KEY `ix_mediafiles_folder` (`MediaFolderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `NotificationTemplates` (
    `NotificationTemplateId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`               BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Code`                   VARCHAR(100) NOT NULL,
    `Channel`                VARCHAR(20) NOT NULL,   -- Email | SMS | WhatsApp
    `Subject`                VARCHAR(255) NULL,
    `Body`                   TEXT NULL,
    `IsActive`               TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`              DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`NotificationTemplateId`),
    UNIQUE KEY `uq_notiftemplates_tenant_code_channel` (`TenantId`, `Code`, `Channel`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `NotificationHistory` (
    `NotificationHistoryId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`              BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `TemplateId`            BIGINT UNSIGNED NULL,
    `Channel`               VARCHAR(20) NOT NULL,
    `Recipient`             VARCHAR(255) NOT NULL,
    `Subject`               VARCHAR(255) NULL,
    `Body`                  TEXT NULL,
    `Status`                VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending | Sent | Failed
    `Error`                 VARCHAR(500) NULL,
    `SentAt`                DATETIME NULL,
    `CreatedAt`             DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`NotificationHistoryId`),
    KEY `ix_notifhistory_template` (`TemplateId`),
    KEY `ix_notifhistory_recipient` (`Recipient`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AuditLogs` (
    `AuditLogId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`   BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`     BIGINT UNSIGNED NULL,
    `Action`     VARCHAR(100) NOT NULL,    -- Created | Updated | Deleted | LoggedIn ...
    `EntityName` VARCHAR(100) NULL,
    `EntityId`   VARCHAR(64) NULL,
    `OldValues`  JSON NULL,
    `NewValues`  JSON NULL,
    `IpAddress`  VARCHAR(45) NULL,
    `UserAgent`  VARCHAR(255) NULL,
    `CreatedAt`  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`AuditLogId`),
    KEY `ix_auditlogs_entity` (`EntityName`, `EntityId`),
    KEY `ix_auditlogs_user` (`UserId`),
    KEY `ix_auditlogs_created` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `SearchLogs` (
    `SearchLogId`  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`     BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `UserId`       BIGINT UNSIGNED NULL,
    `QueryText`    VARCHAR(255) NOT NULL,
    `ResultsCount` INT NOT NULL DEFAULT 0,
    `CreatedAt`    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`SearchLogId`),
    KEY `ix_searchlogs_query` (`QueryText`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `PopularSearches` (
    `PopularSearchId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`        BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Term`            VARCHAR(255) NOT NULL,
    `SearchCount`     BIGINT UNSIGNED NOT NULL DEFAULT 0,
    `LastSearchedAt`  DATETIME NULL,
    PRIMARY KEY (`PopularSearchId`),
    UNIQUE KEY `uq_popularsearches_tenant_term` (`TenantId`, `Term`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ImportJobs` (
    `ImportJobId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`    BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `JobType`     VARCHAR(30) NOT NULL,     -- Products | Inventory | ...
    `FileName`    VARCHAR(255) NULL,
    `FileUrl`     VARCHAR(500) NULL,
    `Status`      VARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending|Processing|Completed|Failed|PartiallyCompleted
    `TotalRows`   INT NOT NULL DEFAULT 0,
    `SuccessRows` INT NOT NULL DEFAULT 0,
    `FailedRows`  INT NOT NULL DEFAULT 0,
    `StartedAt`   DATETIME NULL,
    `CompletedAt` DATETIME NULL,
    `CreatedBy`   BIGINT UNSIGNED NULL,
    `CreatedAt`   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ImportJobId`),
    KEY `ix_importjobs_type` (`JobType`),
    KEY `ix_importjobs_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ImportJobItems` (
    `ImportJobItemId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `ImportJobId`     BIGINT UNSIGNED NOT NULL,
    `RowNumber`       INT NOT NULL,
    `Status`          VARCHAR(20) NOT NULL,   -- Success | Failed
    `ErrorMessage`    VARCHAR(500) NULL,
    `RawData`         JSON NULL,
    `CreatedAt`       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`ImportJobItemId`),
    KEY `ix_importjobitems_job` (`ImportJobId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '011_platform.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '011_platform.sql');
