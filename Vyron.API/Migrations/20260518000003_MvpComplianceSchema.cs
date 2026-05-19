using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{
    /// <summary>
    /// MVP Compliance schema:
    ///   - New roles: AdminUser, StoreManager, StoreStaff (seeded into Roles table)
    ///   - Rider: ApprovalStatus, CreatedByUserId, RejectedByUserId, RejectedAt, RejectionReason
    ///   - User:  RegistrationApprovalStatus, ApprovedByUserId, RegistrationApprovedAt,
    ///            RejectedByUserId, RegistrationRejectedAt, RegistrationRejectionReason
    ///   - Notification: RelatedEntityType, RelatedEntityId, ReadAt
    ///   - CommunicationLog: Provider, ProviderReference
    ///   - Order: PickupDelayNotifiedAt, SlaBreachNotifiedAt
    ///   - New table: StoreUserAssignments
    ///   - New table: ActivityLogs
    /// Fully idempotent — safe to re-run.
    /// </summary>
    [DbContext(typeof(VyronDbContext))]
    [Migration("20260518000003_MvpComplianceSchema")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class MvpComplianceSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ══════════════════════════════════════════════════════════════════
--  RIDERS — approval workflow columns
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'ApprovalStatus')
    ALTER TABLE Riders ADD ApprovalStatus int NOT NULL DEFAULT 0;   -- 0=Pending,1=Approved,2=Rejected

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'CreatedByUserId')
    ALTER TABLE Riders ADD CreatedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedByUserId')
    ALTER TABLE Riders ADD RejectedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedAt')
    ALTER TABLE Riders ADD RejectedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectionReason')
    ALTER TABLE Riders ADD RejectionReason nvarchar(500) NULL;

-- Sync legacy IsApproved → ApprovalStatus for existing approved riders
-- (must use dynamic SQL to avoid parse-time column-not-exists error)
EXEC sp_executesql N'UPDATE Riders SET ApprovalStatus = 1 WHERE IsApproved = 1 AND ApprovalStatus = 0';

-- Index for pending-approval queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Riders') AND name = 'IX_Riders_ApprovalStatus')
    CREATE INDEX IX_Riders_ApprovalStatus ON Riders(ApprovalStatus);

-- ══════════════════════════════════════════════════════════════════
--  USERS — StoreOwner registration approval columns
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovalStatus')
    ALTER TABLE Users ADD RegistrationApprovalStatus int NULL;   -- NULL=not applicable, 0=Pending, 1=Approved, 2=Rejected

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedByUserId')
    ALTER TABLE Users ADD ApprovedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovedAt')
    ALTER TABLE Users ADD RegistrationApprovedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RejectedByUserId')
    ALTER TABLE Users ADD RejectedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectedAt')
    ALTER TABLE Users ADD RegistrationRejectedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectionReason')
    ALTER TABLE Users ADD RegistrationRejectionReason nvarchar(500) NULL;

-- Mark existing StoreOwner users as already approved (they were manually created)
EXEC sp_executesql N'UPDATE Users SET RegistrationApprovalStatus = 1, RegistrationApprovedAt = GETUTCDATE() WHERE Role = 2 AND RegistrationApprovalStatus IS NULL';

-- ══════════════════════════════════════════════════════════════════
--  NOTIFICATIONS — new columns
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'ReadAt')
    ALTER TABLE Notifications ADD ReadAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'RelatedEntityType')
    ALTER TABLE Notifications ADD RelatedEntityType nvarchar(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'RelatedEntityId')
    ALTER TABLE Notifications ADD RelatedEntityId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_CreatedAt')
    CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt);

-- ══════════════════════════════════════════════════════════════════
--  COMMUNICATION LOGS — provider tracking columns
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'Provider')
    ALTER TABLE CommunicationLogs ADD Provider nvarchar(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'ProviderReference')
    ALTER TABLE CommunicationLogs ADD ProviderReference nvarchar(200) NULL;

-- ══════════════════════════════════════════════════════════════════
--  ORDERS — SLA delay notification tracking
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PickupDelayNotifiedAt')
    ALTER TABLE Orders ADD PickupDelayNotifiedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'SlaBreachNotifiedAt')
    ALTER TABLE Orders ADD SlaBreachNotifiedAt datetime2 NULL;

-- ══════════════════════════════════════════════════════════════════
--  NEW TABLE: StoreUserAssignments
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('StoreUserAssignments') AND type = 'U')
BEGIN
    CREATE TABLE StoreUserAssignments (
        Id               uniqueidentifier NOT NULL DEFAULT NEWID(),
        UserId           uniqueidentifier NOT NULL,
        StoreId          uniqueidentifier NOT NULL,
        StaffRole        nvarchar(30)     NOT NULL DEFAULT 'StoreStaff',
        IsActive         bit              NOT NULL DEFAULT 1,
        AssignedByUserId uniqueidentifier NOT NULL,
        AssignedAt       datetime2        NOT NULL DEFAULT GETUTCDATE(),
        RevokedAt        datetime2            NULL,
        RevokedByUserId  uniqueidentifier     NULL,

        CONSTRAINT PK_StoreUserAssignments PRIMARY KEY (Id),
        CONSTRAINT FK_StoreUserAssignments_Users_UserId
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_StoreUserAssignments_Stores_StoreId
            FOREIGN KEY (StoreId) REFERENCES Stores(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_StoreUserAssignments_Users_AssignedBy
            FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_StoreUserAssignments_UserId  ON StoreUserAssignments(UserId);
    CREATE INDEX IX_StoreUserAssignments_StoreId ON StoreUserAssignments(StoreId);
    CREATE INDEX IX_StoreUserAssignments_Active  ON StoreUserAssignments(UserId, StoreId, IsActive);
END

-- ══════════════════════════════════════════════════════════════════
--  NEW TABLE: ActivityLogs
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ActivityLogs') AND type = 'U')
BEGIN
    CREATE TABLE ActivityLogs (
        Id           uniqueidentifier NOT NULL DEFAULT NEWID(),
        UserId       uniqueidentifier     NULL,
        ActivityType nvarchar(100)    NOT NULL,
        Description  nvarchar(1000)       NULL,
        EntityType   nvarchar(50)         NULL,
        EntityId     uniqueidentifier     NULL,
        IpAddress    nvarchar(50)         NULL,
        UserAgent    nvarchar(300)        NULL,
        CreatedAt    datetime2        NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT PK_ActivityLogs PRIMARY KEY (Id),
        CONSTRAINT FK_ActivityLogs_Users_UserId
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_ActivityLogs_UserId    ON ActivityLogs(UserId);
    CREATE INDEX IX_ActivityLogs_CreatedAt ON ActivityLogs(CreatedAt);
    CREATE INDEX IX_ActivityLogs_Entity    ON ActivityLogs(EntityType, EntityId);
END

-- ══════════════════════════════════════════════════════════════════
--  SEED NEW ROLES (idempotent)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000006')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000006', 'AdminUser', 'ADMINUSER',
            'Admin-level user created by SuperAdmin; cannot manage other admins', 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000007')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000007', 'StoreManager', 'STOREMANAGER',
            'Store manager scoped to assigned stores', 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000008')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000008', 'StoreStaff', 'STORESTAFF',
            'Store staff scoped to assigned stores', 1, GETUTCDATE());
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Remove new role seeds
DELETE FROM Roles WHERE Id IN (
    'eeeeeeee-0000-0000-0000-000000000006',
    'eeeeeeee-0000-0000-0000-000000000007',
    'eeeeeeee-0000-0000-0000-000000000008'
);

-- Drop new tables
IF OBJECT_ID('ActivityLogs') IS NOT NULL DROP TABLE ActivityLogs;
IF OBJECT_ID('StoreUserAssignments') IS NOT NULL DROP TABLE StoreUserAssignments;

-- Drop Order SLA columns
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'SlaBreachNotifiedAt')
    ALTER TABLE Orders DROP COLUMN SlaBreachNotifiedAt;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PickupDelayNotifiedAt')
    ALTER TABLE Orders DROP COLUMN PickupDelayNotifiedAt;

-- Drop CommunicationLog columns
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'ProviderReference')
    ALTER TABLE CommunicationLogs DROP COLUMN ProviderReference;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'Provider')
    ALTER TABLE CommunicationLogs DROP COLUMN Provider;

-- Drop Notification columns
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_CreatedAt')
    DROP INDEX IX_Notifications_CreatedAt ON Notifications;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'RelatedEntityId')
    ALTER TABLE Notifications DROP COLUMN RelatedEntityId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'RelatedEntityType')
    ALTER TABLE Notifications DROP COLUMN RelatedEntityType;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Notifications') AND name = 'ReadAt')
    ALTER TABLE Notifications DROP COLUMN ReadAt;

-- Drop User approval columns
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectionReason')
    ALTER TABLE Users DROP COLUMN RegistrationRejectionReason;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectedAt')
    ALTER TABLE Users DROP COLUMN RegistrationRejectedAt;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RejectedByUserId')
    ALTER TABLE Users DROP COLUMN RejectedByUserId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovedAt')
    ALTER TABLE Users DROP COLUMN RegistrationApprovedAt;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedByUserId')
    ALTER TABLE Users DROP COLUMN ApprovedByUserId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovalStatus')
    ALTER TABLE Users DROP COLUMN RegistrationApprovalStatus;

-- Drop Rider approval columns
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Riders') AND name = 'IX_Riders_ApprovalStatus')
    DROP INDEX IX_Riders_ApprovalStatus ON Riders;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectionReason')
    ALTER TABLE Riders DROP COLUMN RejectionReason;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedAt')
    ALTER TABLE Riders DROP COLUMN RejectedAt;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedByUserId')
    ALTER TABLE Riders DROP COLUMN RejectedByUserId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'CreatedByUserId')
    ALTER TABLE Riders DROP COLUMN CreatedByUserId;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'ApprovalStatus')
    ALTER TABLE Riders DROP COLUMN ApprovalStatus;
");
        }
    }
}
