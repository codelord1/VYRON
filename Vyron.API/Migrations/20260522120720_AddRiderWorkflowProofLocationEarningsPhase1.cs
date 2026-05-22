using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{

/// <summary>
/// Phase 1 — Rider MVP schema additions (additive only).
/// Adds columns to Orders and Riders; creates RiderEarnings and RiderPayouts tables.
/// All operations are idempotent (IF NOT EXISTS / COL_LENGTH guards).
/// Does NOT recreate, rename, or drop any pre-existing tables or columns.
/// </summary>
[DbContext(typeof(VyronDbContext))]
[Migration("20260522120720_AddRiderWorkflowProofLocationEarningsPhase1")]
public partial class AddRiderWorkflowProofLocationEarningsPhase1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Orders: proof capture columns ────────────────────────────────────────
        migrationBuilder.Sql(@"
IF COL_LENGTH('Orders', 'PickupProofUrl') IS NULL
    ALTER TABLE Orders ADD PickupProofUrl NVARCHAR(500) NULL;
IF COL_LENGTH('Orders', 'PickupProofUploadedAt') IS NULL
    ALTER TABLE Orders ADD PickupProofUploadedAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'DeliveryProofUrl') IS NULL
    ALTER TABLE Orders ADD DeliveryProofUrl NVARCHAR(500) NULL;
IF COL_LENGTH('Orders', 'DeliveryProofUploadedAt') IS NULL
    ALTER TABLE Orders ADD DeliveryProofUploadedAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'PickupNotes') IS NULL
    ALTER TABLE Orders ADD PickupNotes NVARCHAR(500) NULL;
IF COL_LENGTH('Orders', 'DeliveryNotes') IS NULL
    ALTER TABLE Orders ADD DeliveryNotes NVARCHAR(500) NULL;
IF COL_LENGTH('Orders', 'BagCount') IS NULL
    ALTER TABLE Orders ADD BagCount INT NULL;
");

        // ── Orders: customer confirmation ─────────────────────────────────────────
        migrationBuilder.Sql(@"
IF COL_LENGTH('Orders', 'CustomerConfirmed') IS NULL
    ALTER TABLE Orders ADD CustomerConfirmed BIT NOT NULL
        CONSTRAINT DF_Orders_CustomerConfirmed DEFAULT 0;
IF COL_LENGTH('Orders', 'CustomerConfirmedAt') IS NULL
    ALTER TABLE Orders ADD CustomerConfirmedAt DATETIME2 NULL;
");

        // ── Orders: earnings + rider workflow timestamps ───────────────────────────
        migrationBuilder.Sql(@"
IF COL_LENGTH('Orders', 'RiderEarningsAmount') IS NULL
    ALTER TABLE Orders ADD RiderEarningsAmount DECIMAL(18,2) NULL;
IF COL_LENGTH('Orders', 'RiderAcceptedAt') IS NULL
    ALTER TABLE Orders ADD RiderAcceptedAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'EnRouteToCustomerAt') IS NULL
    ALTER TABLE Orders ADD EnRouteToCustomerAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'AtCustomerAt') IS NULL
    ALTER TABLE Orders ADD AtCustomerAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'DroppedAtStoreAt') IS NULL
    ALTER TABLE Orders ADD DroppedAtStoreAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'PickedUpFromStoreAt') IS NULL
    ALTER TABLE Orders ADD PickedUpFromStoreAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'EnRouteToDeliveryAt') IS NULL
    ALTER TABLE Orders ADD EnRouteToDeliveryAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'FailedAt') IS NULL
    ALTER TABLE Orders ADD FailedAt DATETIME2 NULL;
IF COL_LENGTH('Orders', 'FailReason') IS NULL
    ALTER TABLE Orders ADD FailReason NVARCHAR(500) NULL;
");

        // ── Riders: location presence ─────────────────────────────────────────────
        migrationBuilder.Sql(@"
IF COL_LENGTH('Riders', 'LastSeenAt') IS NULL
    ALTER TABLE Riders ADD LastSeenAt DATETIME2 NULL;
IF COL_LENGTH('Riders', 'LocationUpdatedAt') IS NULL
    ALTER TABLE Riders ADD LocationUpdatedAt DATETIME2 NULL;
IF COL_LENGTH('Riders', 'IsOnlineOverride') IS NULL
    ALTER TABLE Riders ADD IsOnlineOverride BIT NOT NULL
        CONSTRAINT DF_Riders_IsOnlineOverride DEFAULT 0;
");

        // ── RiderEarnings table ───────────────────────────────────────────────────
        migrationBuilder.Sql(@"
IF OBJECT_ID('RiderEarnings', 'U') IS NULL
BEGIN
    CREATE TABLE RiderEarnings (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RiderEarnings_Id DEFAULT NEWID(),
        RiderId        UNIQUEIDENTIFIER NOT NULL,
        OrderId        UNIQUEIDENTIFIER NOT NULL,
        EarningsType   NVARCHAR(20)     NOT NULL,
        Amount         DECIMAL(18,2)    NOT NULL,
        Status         INT              NOT NULL CONSTRAINT DF_RiderEarnings_Status DEFAULT 0,
        PaidAt         DATETIME2        NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_RiderEarnings_CreatedAt DEFAULT GETUTCDATE(),
        CONSTRAINT PK_RiderEarnings PRIMARY KEY (Id),
        CONSTRAINT FK_RiderEarnings_Riders_RiderId
            FOREIGN KEY (RiderId) REFERENCES Riders(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_RiderEarnings_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE NO ACTION
    );
    CREATE INDEX IX_RiderEarnings_RiderId        ON RiderEarnings (RiderId);
    CREATE INDEX IX_RiderEarnings_OrderId        ON RiderEarnings (OrderId);
    CREATE INDEX IX_RiderEarnings_RiderId_Status ON RiderEarnings (RiderId, Status);
END
");

        // ── RiderPayouts table ────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
IF OBJECT_ID('RiderPayouts', 'U') IS NULL
BEGIN
    CREATE TABLE RiderPayouts (
        Id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RiderPayouts_Id DEFAULT NEWID(),
        RiderId              UNIQUEIDENTIFIER NOT NULL,
        Amount               DECIMAL(18,2)    NOT NULL,
        Status               INT              NOT NULL CONSTRAINT DF_RiderPayouts_Status DEFAULT 0,
        PaymentRef           NVARCHAR(100)    NULL,
        ProcessedAt          DATETIME2        NULL,
        ProcessedByAdminId   UNIQUEIDENTIFIER NULL,
        Notes                NVARCHAR(500)    NULL,
        CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_RiderPayouts_CreatedAt DEFAULT GETUTCDATE(),
        CONSTRAINT PK_RiderPayouts PRIMARY KEY (Id),
        CONSTRAINT FK_RiderPayouts_Riders_RiderId
            FOREIGN KEY (RiderId) REFERENCES Riders(Id) ON DELETE NO ACTION
    );
    CREATE INDEX IX_RiderPayouts_RiderId        ON RiderPayouts (RiderId);
    CREATE INDEX IX_RiderPayouts_RiderId_Status ON RiderPayouts (RiderId, Status);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop new tables first (removes FK dependencies on Orders/Riders)
        migrationBuilder.Sql(@"
IF OBJECT_ID('RiderEarnings', 'U') IS NOT NULL DROP TABLE RiderEarnings;
IF OBJECT_ID('RiderPayouts',  'U') IS NOT NULL DROP TABLE RiderPayouts;
");

        // Drop Riders columns (IsOnlineOverride has named default constraint)
        migrationBuilder.Sql(@"
IF OBJECT_ID('DF_Riders_IsOnlineOverride', 'D') IS NOT NULL
    ALTER TABLE Riders DROP CONSTRAINT DF_Riders_IsOnlineOverride;
IF COL_LENGTH('Riders', 'IsOnlineOverride')    IS NOT NULL ALTER TABLE Riders DROP COLUMN IsOnlineOverride;
IF COL_LENGTH('Riders', 'LocationUpdatedAt')   IS NOT NULL ALTER TABLE Riders DROP COLUMN LocationUpdatedAt;
IF COL_LENGTH('Riders', 'LastSeenAt')          IS NOT NULL ALTER TABLE Riders DROP COLUMN LastSeenAt;
");

        // Drop Orders columns (CustomerConfirmed has named default constraint)
        migrationBuilder.Sql(@"
IF OBJECT_ID('DF_Orders_CustomerConfirmed', 'D') IS NOT NULL
    ALTER TABLE Orders DROP CONSTRAINT DF_Orders_CustomerConfirmed;
IF COL_LENGTH('Orders', 'FailReason')              IS NOT NULL ALTER TABLE Orders DROP COLUMN FailReason;
IF COL_LENGTH('Orders', 'FailedAt')                IS NOT NULL ALTER TABLE Orders DROP COLUMN FailedAt;
IF COL_LENGTH('Orders', 'EnRouteToDeliveryAt')     IS NOT NULL ALTER TABLE Orders DROP COLUMN EnRouteToDeliveryAt;
IF COL_LENGTH('Orders', 'PickedUpFromStoreAt')     IS NOT NULL ALTER TABLE Orders DROP COLUMN PickedUpFromStoreAt;
IF COL_LENGTH('Orders', 'DroppedAtStoreAt')        IS NOT NULL ALTER TABLE Orders DROP COLUMN DroppedAtStoreAt;
IF COL_LENGTH('Orders', 'AtCustomerAt')            IS NOT NULL ALTER TABLE Orders DROP COLUMN AtCustomerAt;
IF COL_LENGTH('Orders', 'EnRouteToCustomerAt')     IS NOT NULL ALTER TABLE Orders DROP COLUMN EnRouteToCustomerAt;
IF COL_LENGTH('Orders', 'RiderAcceptedAt')         IS NOT NULL ALTER TABLE Orders DROP COLUMN RiderAcceptedAt;
IF COL_LENGTH('Orders', 'RiderEarningsAmount')     IS NOT NULL ALTER TABLE Orders DROP COLUMN RiderEarningsAmount;
IF COL_LENGTH('Orders', 'CustomerConfirmedAt')     IS NOT NULL ALTER TABLE Orders DROP COLUMN CustomerConfirmedAt;
IF COL_LENGTH('Orders', 'CustomerConfirmed')       IS NOT NULL ALTER TABLE Orders DROP COLUMN CustomerConfirmed;
IF COL_LENGTH('Orders', 'BagCount')                IS NOT NULL ALTER TABLE Orders DROP COLUMN BagCount;
IF COL_LENGTH('Orders', 'DeliveryNotes')           IS NOT NULL ALTER TABLE Orders DROP COLUMN DeliveryNotes;
IF COL_LENGTH('Orders', 'PickupNotes')             IS NOT NULL ALTER TABLE Orders DROP COLUMN PickupNotes;
IF COL_LENGTH('Orders', 'DeliveryProofUploadedAt') IS NOT NULL ALTER TABLE Orders DROP COLUMN DeliveryProofUploadedAt;
IF COL_LENGTH('Orders', 'DeliveryProofUrl')        IS NOT NULL ALTER TABLE Orders DROP COLUMN DeliveryProofUrl;
IF COL_LENGTH('Orders', 'PickupProofUploadedAt')   IS NOT NULL ALTER TABLE Orders DROP COLUMN PickupProofUploadedAt;
IF COL_LENGTH('Orders', 'PickupProofUrl')          IS NOT NULL ALTER TABLE Orders DROP COLUMN PickupProofUrl;
");
    }
}

}
