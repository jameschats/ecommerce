-- =====================================================================
-- 015_auth.sql  —  Multi-provider authentication & identity
-- Forward-only ALTERs to Users + new auth tables.
-- Supports: Email+Password, Mobile OTP (passwordless), Google sign-in.
-- Providers are admin-toggleable; all flows converge on one JWT.
-- =====================================================================

-- Users: loosen for password-less / email-less identities --------------
ALTER TABLE `Users`
    MODIFY COLUMN `Email`           VARCHAR(256) NULL,
    MODIFY COLUMN `NormalizedEmail` VARCHAR(256) NULL,
    MODIFY COLUMN `PasswordHash`    VARCHAR(255) NULL,
    ADD COLUMN `IsPhoneVerified` TINYINT(1) NOT NULL DEFAULT 0 AFTER `PhoneNumber`,
    ADD COLUMN `PhoneVerifiedAt` DATETIME NULL AFTER `IsPhoneVerified`,
    ADD UNIQUE KEY `uq_users_tenant_phone` (`TenantId`, `PhoneNumber`);

-- Admin-controlled auth providers -------------------------------------
CREATE TABLE IF NOT EXISTS `AuthProviders` (
    `AuthProviderId`    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Provider`          VARCHAR(30) NOT NULL,        -- EmailPassword | MobileOtp | Google
    `IsEnabled`         TINYINT(1) NOT NULL DEFAULT 1,
    `AllowRegistration` TINYINT(1) NOT NULL DEFAULT 1,
    `DisplayName`       VARCHAR(100) NULL,
    `DisplayOrder`      INT NOT NULL DEFAULT 0,
    `ConfigJson`        JSON NULL,                   -- public config only (e.g. Google client-id)
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`         DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`AuthProviderId`),
    UNIQUE KEY `uq_authproviders_tenant_provider` (`TenantId`, `Provider`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- External / linked logins (Google, phone, …) -------------------------
CREATE TABLE IF NOT EXISTS `UserExternalLogins` (
    `UserExternalLoginId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `UserId`              BIGINT UNSIGNED NOT NULL,
    `Provider`            VARCHAR(30)  NOT NULL,     -- Google | MobileOtp
    `ProviderUserId`      VARCHAR(255) NOT NULL,     -- Google `sub` / phone number
    `Email`               VARCHAR(256) NULL,
    `CreatedAt`           DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`UserExternalLoginId`),
    UNIQUE KEY `uq_userexternallogins_provider_key` (`Provider`, `ProviderUserId`),
    KEY `ix_userexternallogins_user` (`UserId`),
    CONSTRAINT `fk_userexternallogins_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- OTP codes (mobile login, email verification, password reset) --------
CREATE TABLE IF NOT EXISTS `OtpVerifications` (
    `OtpVerificationId` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `TenantId`          BIGINT UNSIGNED NOT NULL DEFAULT 1,
    `Identifier`        VARCHAR(256) NOT NULL,       -- phone or email
    `Channel`           VARCHAR(20)  NOT NULL,       -- SMS | Email
    `Purpose`           VARCHAR(30)  NOT NULL,       -- Login | Register | ResetPassword | VerifyEmail
    `CodeHash`          VARCHAR(255) NOT NULL,       -- OTP hashed at rest
    `ExpiresAt`         DATETIME NOT NULL,
    `AttemptCount`      INT NOT NULL DEFAULT 0,
    `MaxAttempts`       INT NOT NULL DEFAULT 5,
    `ConsumedAt`        DATETIME NULL,
    `CreatedAt`         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`OtpVerificationId`),
    KEY `ix_otp_identifier_purpose` (`Identifier`, `Purpose`),
    KEY `ix_otp_expires` (`ExpiresAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- JWT refresh tokens (hashed, rotating) -------------------------------
CREATE TABLE IF NOT EXISTS `RefreshTokens` (
    `RefreshTokenId`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `UserId`           BIGINT UNSIGNED NOT NULL,
    `TokenHash`        VARCHAR(255) NOT NULL,
    `ExpiresAt`        DATETIME NOT NULL,
    `RevokedAt`        DATETIME NULL,
    `ReplacedByHash`   VARCHAR(255) NULL,
    `CreatedByIp`      VARCHAR(45) NULL,
    `CreatedAt`        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`RefreshTokenId`),
    UNIQUE KEY `uq_refreshtokens_hash` (`TokenHash`),
    KEY `ix_refreshtokens_user` (`UserId`),
    CONSTRAINT `fk_refreshtokens_user` FOREIGN KEY (`UserId`) REFERENCES `Users` (`UserId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed default providers (Google off until a client-id is configured) --
INSERT IGNORE INTO `AuthProviders` (`TenantId`, `Provider`, `IsEnabled`, `AllowRegistration`, `DisplayName`, `DisplayOrder`) VALUES
    (1, 'EmailPassword', 1, 1, 'Email & Password', 1),
    (1, 'MobileOtp',     1, 1, 'Mobile OTP',       2),
    (1, 'Google',        0, 1, 'Google',           3);

INSERT INTO `__schema_migrations` (`script_name`)
SELECT '015_auth.sql'
WHERE NOT EXISTS (SELECT 1 FROM `__schema_migrations` WHERE `script_name` = '015_auth.sql');
