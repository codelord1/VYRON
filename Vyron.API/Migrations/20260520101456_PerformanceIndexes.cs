using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vyron.API.Migrations
{
    /// <summary>
    /// Idempotent migration — safe to run on any DB state.
    /// Catches up any schema columns/tables not yet present (from earlier migrations that used
    /// raw SQL and may not have been reflected in the snapshot), then adds the new
    /// composite performance indexes for the CustomerApp mobile endpoints.
    /// All operations guarded with IF NOT EXISTS / IF EXISTS.
    /// </summary>
    public partial class PerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ══════════════════════════════════════════════════════════════════
--  STORES — structured opening hours (20260518000001)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'IsManuallyClosed')
    ALTER TABLE Stores ADD IsManuallyClosed bit NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningTime')
    ALTER TABLE Stores ADD OpeningTime time NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'ClosingTime')
    ALTER TABLE Stores ADD ClosingTime time NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningDays')
    ALTER TABLE Stores ADD OpeningDays nvarchar(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastOpenedAt')
    ALTER TABLE Stores ADD LastOpenedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastClosedAt')
    ALTER TABLE Stores ADD LastClosedAt datetime2 NULL;

-- ══════════════════════════════════════════════════════════════════
--  ORDERS — delivery rider + SLA delay columns (20260518000002)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'DeliveryRiderId')
    ALTER TABLE Orders ADD DeliveryRiderId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PickupDelayNotifiedAt')
    ALTER TABLE Orders ADD PickupDelayNotifiedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'SlaBreachNotifiedAt')
    ALTER TABLE Orders ADD SlaBreachNotifiedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Orders_Riders_DeliveryRiderId')
    ALTER TABLE Orders ADD CONSTRAINT FK_Orders_Riders_DeliveryRiderId
        FOREIGN KEY (DeliveryRiderId) REFERENCES Riders(Id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_DeliveryRiderId')
    CREATE INDEX IX_Orders_DeliveryRiderId ON Orders(DeliveryRiderId);

-- ══════════════════════════════════════════════════════════════════
--  SERVICE TYPES + ServiceOfferings FK (20260516081749)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ServiceTypes') AND type = 'U')
    CREATE TABLE ServiceTypes (
        Id          uniqueidentifier NOT NULL PRIMARY KEY,
        Name        nvarchar(100)    NOT NULL,
        Description nvarchar(500)    NULL,
        SortOrder   int              NOT NULL DEFAULT 0,
        IsActive    bit              NOT NULL DEFAULT 1,
        CreatedAt   datetime2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   datetime2        NOT NULL DEFAULT GETUTCDATE()
    );

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ServiceOfferings') AND name = 'ServiceTypeEntityId')
    ALTER TABLE ServiceOfferings ADD ServiceTypeEntityId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId')
    ALTER TABLE ServiceOfferings ADD CONSTRAINT FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId
        FOREIGN KEY (ServiceTypeEntityId) REFERENCES ServiceTypes(Id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ServiceOfferings') AND name = 'IX_ServiceOfferings_ServiceTypeEntityId')
    CREATE INDEX IX_ServiceOfferings_ServiceTypeEntityId ON ServiceOfferings(ServiceTypeEntityId);

-- ══════════════════════════════════════════════════════════════════
--  COMMUNICATION LOGS table + columns (20260516081749)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CommunicationLogs') AND type = 'U')
    CREATE TABLE CommunicationLogs (
        Id                uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Channel           nvarchar(20)     NOT NULL,
        Status            nvarchar(20)     NOT NULL,
        RecipientUserId   uniqueidentifier NULL,
        RecipientName     nvarchar(120)    NULL,
        RecipientPhone    nvarchar(30)     NULL,
        RecipientEmail    nvarchar(200)    NULL,
        Subject           nvarchar(300)    NOT NULL,
        Body              nvarchar(4000)   NOT NULL,
        RelatedEntityType nvarchar(50)     NULL,
        RelatedEntityId   uniqueidentifier NULL,
        SentByAdminId     uniqueidentifier NULL,
        Provider          nvarchar(50)     NULL,
        ProviderReference nvarchar(200)    NULL,
        ErrorMessage      nvarchar(500)    NULL,
        SentAt            datetime2        NULL,
        CreatedAt         datetime2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CommunicationLogs_Users_RecipientUserId
            FOREIGN KEY (RecipientUserId) REFERENCES Users(Id),
        CONSTRAINT FK_CommunicationLogs_Users_SentByAdminId
            FOREIGN KEY (SentByAdminId) REFERENCES Users(Id)
    );

-- CommunicationLogs columns added after table creation
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'Provider')
    ALTER TABLE CommunicationLogs ADD Provider nvarchar(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'ProviderReference')
    ALTER TABLE CommunicationLogs ADD ProviderReference nvarchar(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'IX_CommunicationLogs_Channel')
    CREATE INDEX IX_CommunicationLogs_Channel ON CommunicationLogs(Channel);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'IX_CommunicationLogs_CreatedAt')
    CREATE INDEX IX_CommunicationLogs_CreatedAt ON CommunicationLogs(CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'IX_CommunicationLogs_RecipientUserId')
    CREATE INDEX IX_CommunicationLogs_RecipientUserId ON CommunicationLogs(RecipientUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'IX_CommunicationLogs_SentByAdminId')
    CREATE INDEX IX_CommunicationLogs_SentByAdminId ON CommunicationLogs(SentByAdminId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CommunicationLogs') AND name = 'IX_CommunicationLogs_Status')
    CREATE INDEX IX_CommunicationLogs_Status ON CommunicationLogs(Status);

-- ══════════════════════════════════════════════════════════════════
--  RIDERS — approval workflow columns (20260518000003)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'ApprovalStatus')
    ALTER TABLE Riders ADD ApprovalStatus int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'CreatedByUserId')
    ALTER TABLE Riders ADD CreatedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedAt')
    ALTER TABLE Riders ADD RejectedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectedByUserId')
    ALTER TABLE Riders ADD RejectedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Riders') AND name = 'RejectionReason')
    ALTER TABLE Riders ADD RejectionReason nvarchar(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Riders') AND name = 'IX_Riders_ApprovalStatus')
    CREATE INDEX IX_Riders_ApprovalStatus ON Riders(ApprovalStatus);

-- ══════════════════════════════════════════════════════════════════
--  USERS — registration approval columns (20260518000003)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedByUserId')
    ALTER TABLE Users ADD ApprovedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovalStatus')
    ALTER TABLE Users ADD RegistrationApprovalStatus int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationApprovedAt')
    ALTER TABLE Users ADD RegistrationApprovedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RejectedByUserId')
    ALTER TABLE Users ADD RejectedByUserId uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectedAt')
    ALTER TABLE Users ADD RegistrationRejectedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RegistrationRejectionReason')
    ALTER TABLE Users ADD RegistrationRejectionReason nvarchar(500) NULL;

-- ══════════════════════════════════════════════════════════════════
--  NOTIFICATIONS — new columns (20260518000003)
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
--  STORE USER ASSIGNMENTS table (20260518000003)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('StoreUserAssignments') AND type = 'U')
BEGIN
    CREATE TABLE StoreUserAssignments (
        Id               uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId           uniqueidentifier NOT NULL,
        StoreId          uniqueidentifier NOT NULL,
        StaffRole        nvarchar(30)     NOT NULL DEFAULT 'StoreStaff',
        IsActive         bit              NOT NULL DEFAULT 1,
        AssignedByUserId uniqueidentifier NOT NULL,
        AssignedAt       datetime2        NOT NULL DEFAULT GETUTCDATE(),
        RevokedAt        datetime2        NULL,
        RevokedByUserId  uniqueidentifier NULL,
        CONSTRAINT FK_StoreUserAssignments_Users_UserId
            FOREIGN KEY (UserId)           REFERENCES Users(Id),
        CONSTRAINT FK_StoreUserAssignments_Stores_StoreId
            FOREIGN KEY (StoreId)          REFERENCES Stores(Id),
        CONSTRAINT FK_StoreUserAssignments_Users_AssignedBy
            FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StoreUserAssignments') AND name = 'IX_StoreUserAssignments_UserId')
    CREATE INDEX IX_StoreUserAssignments_UserId ON StoreUserAssignments(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StoreUserAssignments') AND name = 'IX_StoreUserAssignments_StoreId')
    CREATE INDEX IX_StoreUserAssignments_StoreId ON StoreUserAssignments(StoreId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StoreUserAssignments') AND name = 'IX_StoreUserAssignments_UserId_StoreId_IsActive')
    CREATE INDEX IX_StoreUserAssignments_UserId_StoreId_IsActive ON StoreUserAssignments(UserId, StoreId, IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('StoreUserAssignments') AND name = 'IX_StoreUserAssignments_AssignedByUserId')
    CREATE INDEX IX_StoreUserAssignments_AssignedByUserId ON StoreUserAssignments(AssignedByUserId);

-- ══════════════════════════════════════════════════════════════════
--  ACTIVITY LOGS table (20260518000003)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ActivityLogs') AND type = 'U')
BEGIN
    CREATE TABLE ActivityLogs (
        Id           uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserId       uniqueidentifier NULL,
        ActivityType nvarchar(100)    NOT NULL,
        Description  nvarchar(1000)   NULL,
        EntityType   nvarchar(50)     NULL,
        EntityId     uniqueidentifier NULL,
        IpAddress    nvarchar(50)     NULL,
        UserAgent    nvarchar(300)    NULL,
        CreatedAt    datetime2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ActivityLogs_Users_UserId
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ActivityLogs') AND name = 'IX_ActivityLogs_UserId')
    CREATE INDEX IX_ActivityLogs_UserId ON ActivityLogs(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ActivityLogs') AND name = 'IX_ActivityLogs_CreatedAt')
    CREATE INDEX IX_ActivityLogs_CreatedAt ON ActivityLogs(CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ActivityLogs') AND name = 'IX_ActivityLogs_EntityType_EntityId')
    CREATE INDEX IX_ActivityLogs_EntityType_EntityId ON ActivityLogs(EntityType, EntityId);

-- ══════════════════════════════════════════════════════════════════
--  SEED: AdminUser / StoreManager / StoreStaff roles (20260518000003)
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000006')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000006', 'AdminUser', 'ADMINUSER',
            'Admin-level user created by SuperAdmin; cannot manage other admins', 1, '2024-01-01');

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000007')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000007', 'StoreManager', 'STOREMANAGER',
            'Store manager scoped to assigned stores', 1, '2024-01-01');

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 'eeeeeeee-0000-0000-0000-000000000008')
    INSERT INTO Roles (Id, Name, NormalizedName, Description, IsActive, CreatedAt)
    VALUES ('eeeeeeee-0000-0000-0000-000000000008', 'StoreStaff', 'STORESTAFF',
            'Store staff scoped to assigned stores', 1, '2024-01-01');

-- ══════════════════════════════════════════════════════════════════
--  NEW: Composite performance indexes for CustomerApp mobile queries
-- ══════════════════════════════════════════════════════════════════
-- GetCustomerOrdersAsync: WHERE CustomerId = X ORDER BY CreatedAt DESC
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_CustomerId_CreatedAt')
    CREATE INDEX IX_Orders_CustomerId_CreatedAt ON Orders(CustomerId, CreatedAt);

-- GetStoreOrdersAsync: WHERE StoreId = X ORDER BY CreatedAt DESC
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_StoreId_CreatedAt')
    CREATE INDEX IX_Orders_StoreId_CreatedAt ON Orders(StoreId, CreatedAt);

-- GetMyNotifications: WHERE UserId = X ORDER BY CreatedAt DESC TAKE 50
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_UserId_CreatedAt')
    CREATE INDEX IX_Notifications_UserId_CreatedAt ON Notifications(UserId, CreatedAt);

-- UnreadCount: WHERE UserId = X AND IsRead = 0
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_UserId_IsRead')
    CREATE INDEX IX_Notifications_UserId_IsRead ON Notifications(UserId, IsRead);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Only drop the new composite indexes added by this migration.
-- Schema columns/tables from earlier migrations are managed by their own Down() methods.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_UserId_IsRead')
    DROP INDEX IX_Notifications_UserId_IsRead ON Notifications;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Notifications') AND name = 'IX_Notifications_UserId_CreatedAt')
    DROP INDEX IX_Notifications_UserId_CreatedAt ON Notifications;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_StoreId_CreatedAt')
    DROP INDEX IX_Orders_StoreId_CreatedAt ON Orders;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_CustomerId_CreatedAt')
    DROP INDEX IX_Orders_CustomerId_CreatedAt ON Orders;
");
        }
    }
}
