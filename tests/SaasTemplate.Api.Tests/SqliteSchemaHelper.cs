using Microsoft.Data.Sqlite;

namespace SaasTemplate.Api.Tests;

/// <summary>
/// Creates the minimum SQLite schema needed for integration tests,
/// bypassing EF Core's SQL Server-specific DDL generation.
/// </summary>
public static class SqliteSchemaHelper
{
    private static readonly Dictionary<string, bool> _created = new();
    private static readonly object _lock = new();

    public static void EnsureSchema(string connectionString)
    {
        lock (_lock)
        {
            if (_created.TryGetValue(connectionString, out var done) && done) return;
            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            CreateSchema(conn);
            _created[connectionString] = true;
        }
    }

    private static void CreateSchema(SqliteConnection conn)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS AspNetUsers (
                Id TEXT NOT NULL PRIMARY KEY,
                UserName TEXT,
                NormalizedUserName TEXT,
                Email TEXT,
                NormalizedEmail TEXT,
                EmailConfirmed INTEGER NOT NULL DEFAULT 0,
                PasswordHash TEXT,
                SecurityStamp TEXT,
                ConcurrencyStamp TEXT,
                PhoneNumber TEXT,
                PhoneNumberConfirmed INTEGER NOT NULL DEFAULT 0,
                TwoFactorEnabled INTEGER NOT NULL DEFAULT 0,
                LockoutEnd TEXT,
                LockoutEnabled INTEGER NOT NULL DEFAULT 0,
                AccessFailedCount INTEGER NOT NULL DEFAULT 0,
                FullName TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                MarketingConsent INTEGER NOT NULL DEFAULT 0,
                TosAcceptedAt TEXT,
                DefaultStoreUrl TEXT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UserNameIndex ON AspNetUsers (NormalizedUserName);
            CREATE INDEX IF NOT EXISTS EmailIndex ON AspNetUsers (NormalizedEmail);

            CREATE TABLE IF NOT EXISTS AspNetRoles (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT,
                NormalizedName TEXT,
                ConcurrencyStamp TEXT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS RoleNameIndex ON AspNetRoles (NormalizedName);

            CREATE TABLE IF NOT EXISTS AspNetUserRoles (
                UserId TEXT NOT NULL,
                RoleId TEXT NOT NULL,
                PRIMARY KEY (UserId, RoleId),
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
                FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AspNetUserClaims (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ClaimType TEXT,
                ClaimValue TEXT,
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AspNetUserLogins (
                LoginProvider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT,
                UserId TEXT NOT NULL,
                PRIMARY KEY (LoginProvider, ProviderKey),
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AspNetUserTokens (
                UserId TEXT NOT NULL,
                LoginProvider TEXT NOT NULL,
                Name TEXT NOT NULL,
                Value TEXT,
                PRIMARY KEY (UserId, LoginProvider, Name),
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AspNetRoleClaims (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                RoleId TEXT NOT NULL,
                ClaimType TEXT,
                ClaimValue TEXT,
                FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Scans (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT,
                Url TEXT NOT NULL,
                Email TEXT,
                Status TEXT NOT NULL DEFAULT 'queued',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                CompletedAt TEXT,
                Duration TEXT,
                Error TEXT,
                TotalViolations INTEGER NOT NULL DEFAULT 0,
                CriticalCount INTEGER NOT NULL DEFAULT 0,
                SeriousCount INTEGER NOT NULL DEFAULT 0,
                ModerateCount INTEGER NOT NULL DEFAULT 0,
                MinorCount INTEGER NOT NULL DEFAULT 0,
                PagesScanned TEXT NOT NULL DEFAULT '[]',
                IsAutoScan INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Scans_UserId ON Scans (UserId);
            CREATE INDEX IF NOT EXISTS IX_Scans_UserId_CreatedAt ON Scans (UserId, CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_Scans_Email ON Scans (Email);
            CREATE INDEX IF NOT EXISTS IX_Scans_Status ON Scans (Status);

            CREATE TABLE IF NOT EXISTS Violations (
                Id TEXT NOT NULL PRIMARY KEY,
                ScanId TEXT NOT NULL,
                AxeRuleId TEXT NOT NULL,
                Impact TEXT NOT NULL,
                Description TEXT NOT NULL,
                Help TEXT NOT NULL,
                HelpUrl TEXT NOT NULL,
                Tags TEXT NOT NULL DEFAULT '[]',
                FOREIGN KEY (ScanId) REFERENCES Scans(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Violations_ScanId ON Violations (ScanId);

            CREATE TABLE IF NOT EXISTS ViolationNodes (
                Id TEXT NOT NULL PRIMARY KEY,
                ViolationId TEXT NOT NULL,
                Html TEXT NOT NULL,
                Target TEXT NOT NULL,
                FailureSummary TEXT NOT NULL,
                FOREIGN KEY (ViolationId) REFERENCES Violations(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Fixes (
                Id TEXT NOT NULL PRIMARY KEY,
                ViolationId TEXT NOT NULL UNIQUE,
                OriginalHtml TEXT NOT NULL,
                FixedHtml TEXT NOT NULL,
                Explanation TEXT NOT NULL,
                WcagCriterion TEXT NOT NULL,
                IsApplicable INTEGER NOT NULL DEFAULT 1,
                Status TEXT NOT NULL DEFAULT 'pending',
                DismissReason TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                ResolvedAt TEXT,
                FOREIGN KEY (ViolationId) REFERENCES Violations(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Subscriptions (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT NOT NULL UNIQUE,
                StripeCustomerId TEXT NOT NULL,
                StripeSubscriptionId TEXT NOT NULL UNIQUE,
                StripePriceId TEXT NOT NULL,
                Tier TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'active',
                CurrentPeriodEnd TEXT,
                LastRescanAt TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                CancelledAt TEXT,
                OnboardingStage INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Subscriptions_UserId ON Subscriptions (UserId);
            CREATE INDEX IF NOT EXISTS IX_Subscriptions_StripeCustomerId ON Subscriptions (StripeCustomerId);

            CREATE TABLE IF NOT EXISTS CustomRulesSettings (
                Id TEXT NOT NULL PRIMARY KEY,
                UserId TEXT NOT NULL UNIQUE,
                DisabledRules TEXT NOT NULL DEFAULT '[]',
                SeverityOverrides TEXT NOT NULL DEFAULT '{}',
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_CustomRulesSettings_UserId ON CustomRulesSettings (UserId);

            CREATE TABLE IF NOT EXISTS DataProtectionKeys (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                FriendlyName TEXT,
                Xml TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditEvents (
                Id TEXT NOT NULL PRIMARY KEY,
                Timestamp TEXT NOT NULL DEFAULT (datetime('now')),
                UserId TEXT,
                Email TEXT,
                Action TEXT NOT NULL,
                TargetType TEXT,
                TargetId TEXT,
                IpAddress TEXT,
                UserAgent TEXT,
                Metadata TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_AuditEvents_Timestamp ON AuditEvents (Timestamp);
            CREATE INDEX IF NOT EXISTS IX_AuditEvents_UserId ON AuditEvents (UserId);
            CREATE INDEX IF NOT EXISTS IX_AuditEvents_Action ON AuditEvents (Action);
            """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
