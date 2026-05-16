using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{
    // Designer attributes required for EF Core migration discovery
    [DbContext(typeof(VyronDbContext))]
    [Migration("20260516081749_AddServiceTypesAndCommunicationLogs")]
    /// <inheritdoc />
    public partial class AddServiceTypesAndCommunicationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── All DDL is idempotent raw SQL so this migration is safe to re-run
            // ── if partially applied.  Cascade fix: use NO ACTION instead of SET NULL
            // ── on CommunicationLogs FKs to avoid SQL Server multi-cascade-path error.

            migrationBuilder.Sql(@"
-- ── ServiceTypes table ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ServiceTypes' AND type = 'U')
BEGIN
    CREATE TABLE [ServiceTypes] (
        [Id]          uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Name]        nvarchar(100)    NOT NULL,
        [Description] nvarchar(500)    NULL,
        [SortOrder]   int              NOT NULL DEFAULT 0,
        [IsActive]    bit              NOT NULL DEFAULT 1,
        [CreatedAt]   datetime2        NOT NULL,
        [UpdatedAt]   datetime2        NOT NULL,
        CONSTRAINT [PK_ServiceTypes] PRIMARY KEY ([Id])
    );
END

-- ── Seed built-in service types (idempotent) ───────────────────────
IF NOT EXISTS (SELECT 1 FROM [ServiceTypes] WHERE [Id] = 'f0000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO [ServiceTypes] ([Id],[Name],[Description],[SortOrder],[IsActive],[CreatedAt],[UpdatedAt])
    VALUES
      ('f0000000-0000-0000-0000-000000000001','Wash Only',       'Standard wash without ironing',                1,1,'2024-01-01','2024-01-01'),
      ('f0000000-0000-0000-0000-000000000002','Wash & Iron',     'Full wash and iron service',                   2,1,'2024-01-01','2024-01-01'),
      ('f0000000-0000-0000-0000-000000000003','Iron Only',       'Ironing without washing',                      3,1,'2024-01-01','2024-01-01'),
      ('f0000000-0000-0000-0000-000000000004','Dry Cleaning',    'Professional dry-cleaning for delicate items', 4,1,'2024-01-01','2024-01-01'),
      ('f0000000-0000-0000-0000-000000000005','Wash & Fold',     'Wash, dry, and fold - no ironing',             5,1,'2024-01-01','2024-01-01'),
      ('f0000000-0000-0000-0000-000000000006','Special Care',    'Hand-wash and special treatment for delicates',6,1,'2024-01-01','2024-01-01');
END

-- ── ServiceTypeEntityId column on ServiceOfferings ──────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'ServiceOfferings') AND name = N'ServiceTypeEntityId')
BEGIN
    ALTER TABLE [ServiceOfferings] ADD [ServiceTypeEntityId] uniqueidentifier NULL;
END

-- ── Index on ServiceTypeEntityId ────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ServiceOfferings_ServiceTypeEntityId'
      AND object_id = OBJECT_ID(N'ServiceOfferings'))
BEGIN
    CREATE INDEX [IX_ServiceOfferings_ServiceTypeEntityId]
        ON [ServiceOfferings] ([ServiceTypeEntityId]);
END

-- ── FK: ServiceOfferings → ServiceTypes ────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId')
BEGIN
    ALTER TABLE [ServiceOfferings]
        ADD CONSTRAINT [FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId]
        FOREIGN KEY ([ServiceTypeEntityId])
        REFERENCES [ServiceTypes] ([Id])
        ON DELETE SET NULL;
END

-- ── CommunicationLogs table ─────────────────────────────────────────
-- Uses ON DELETE NO ACTION on both User FKs to avoid SQL Server
-- multi-cascade-path error (Users table already has cascade deletes).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CommunicationLogs' AND type = 'U')
BEGIN
    CREATE TABLE [CommunicationLogs] (
        [Id]                uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Channel]           nvarchar(20)     NOT NULL,
        [Status]            nvarchar(20)     NOT NULL,
        [RecipientUserId]   uniqueidentifier NULL,
        [RecipientName]     nvarchar(120)    NULL,
        [RecipientPhone]    nvarchar(30)     NULL,
        [RecipientEmail]    nvarchar(200)    NULL,
        [Subject]           nvarchar(300)    NOT NULL,
        [Body]              nvarchar(4000)   NOT NULL,
        [RelatedEntityType] nvarchar(50)     NULL,
        [RelatedEntityId]   uniqueidentifier NULL,
        [SentByAdminId]     uniqueidentifier NULL,
        [ErrorMessage]      nvarchar(500)    NULL,
        [SentAt]            datetime2        NULL,
        [CreatedAt]         datetime2        NOT NULL,
        CONSTRAINT [PK_CommunicationLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommunicationLogs_Users_RecipientUserId]
            FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CommunicationLogs_Users_SentByAdminId]
            FOREIGN KEY ([SentByAdminId])   REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END

-- ── Indexes on CommunicationLogs ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationLogs_Channel'         AND object_id = OBJECT_ID(N'CommunicationLogs'))
    CREATE INDEX [IX_CommunicationLogs_Channel]          ON [CommunicationLogs] ([Channel]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationLogs_CreatedAt'       AND object_id = OBJECT_ID(N'CommunicationLogs'))
    CREATE INDEX [IX_CommunicationLogs_CreatedAt]        ON [CommunicationLogs] ([CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationLogs_RecipientUserId' AND object_id = OBJECT_ID(N'CommunicationLogs'))
    CREATE INDEX [IX_CommunicationLogs_RecipientUserId]  ON [CommunicationLogs] ([RecipientUserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationLogs_SentByAdminId'   AND object_id = OBJECT_ID(N'CommunicationLogs'))
    CREATE INDEX [IX_CommunicationLogs_SentByAdminId]    ON [CommunicationLogs] ([SentByAdminId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommunicationLogs_Status'          AND object_id = OBJECT_ID(N'CommunicationLogs'))
    CREATE INDEX [IX_CommunicationLogs_Status]           ON [CommunicationLogs] ([Status]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId')
    ALTER TABLE [ServiceOfferings] DROP CONSTRAINT [FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServiceOfferings_ServiceTypeEntityId' AND object_id = OBJECT_ID(N'ServiceOfferings'))
    DROP INDEX [IX_ServiceOfferings_ServiceTypeEntityId] ON [ServiceOfferings];

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ServiceOfferings') AND name = N'ServiceTypeEntityId')
    ALTER TABLE [ServiceOfferings] DROP COLUMN [ServiceTypeEntityId];

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ServiceTypes' AND type = 'U')
    DROP TABLE [ServiceTypes];

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CommunicationLogs' AND type = 'U')
    DROP TABLE [CommunicationLogs];
");
        }
    }
}
