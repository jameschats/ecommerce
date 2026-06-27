-- =====================================================================
-- 001_init_schema_history.sql
-- Baseline migration. Creates the table that records which migration
-- scripts have been applied, so the migration runner is idempotent.
--
-- Convention: scripts are named NNN_description.sql and applied in
-- strict numeric order. Forward-only. Record each applied script here.
-- =====================================================================

CREATE TABLE IF NOT EXISTS `__schema_migrations` (
    `id`          INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `script_name` VARCHAR(255) NOT NULL,
    `applied_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `checksum`    CHAR(64)     NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_schema_migrations_script` (`script_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '001_init_schema_history.sql'
WHERE NOT EXISTS (
    SELECT 1 FROM `__schema_migrations`
    WHERE `script_name` = '001_init_schema_history.sql'
);
