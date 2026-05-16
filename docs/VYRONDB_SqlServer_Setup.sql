/* ═══════════════════════════════════════════════════════════════════
   VYRON Laundry Marketplace v3.0 — SQL Server Setup Script
   Normalized schema with GUID primary keys, indexes, FK integrity.

   USAGE:
   1. Run this script against SQL Server (or let EF migrate on first run).
   2. Update appsettings.json: "Database": { "Provider": "SqlServer" }
   3. Update "ConnectionStrings:DefaultConnection" with your server info.
   ═══════════════════════════════════════════════════════════════════ */

IF DB_ID('VYRONDB') IS NULL CREATE DATABASE VYRONDB;
GO
USE VYRONDB;
GO

-- ─── USERS ────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Phone           NVARCHAR(20)  NOT NULL,
    Email           NVARCHAR(200) NULL,
    FullName        NVARCHAR(120) NOT NULL,
    PasswordHash    NVARCHAR(500) NULL,
    Role            INT NOT NULL DEFAULT 0,            -- 0=Customer,1=Rider,2=StoreOwner,3=Admin,4=SuperAdmin
    IsActive        BIT NOT NULL DEFAULT 1,
    ProfilePhoto    NVARCHAR(500) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastLoginAt     DATETIME2 NULL
);
CREATE UNIQUE INDEX IX_Users_Phone ON dbo.Users(Phone);
CREATE INDEX IX_Users_Email ON dbo.Users(Email);
CREATE INDEX IX_Users_Role  ON dbo.Users(Role);
GO

-- ─── ADDRESSES ─────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Addresses') IS NULL
CREATE TABLE dbo.Addresses (
    Id        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId    UNIQUEIDENTIFIER NOT NULL,
    Label     NVARCHAR(50) NOT NULL,
    Street    NVARCHAR(300) NOT NULL,
    Area      NVARCHAR(100) NOT NULL,
    City      NVARCHAR(100) NOT NULL DEFAULT 'Lagos',
    [State]   NVARCHAR(100) NOT NULL DEFAULT 'Lagos',
    Landmark  NVARCHAR(200) NULL,
    Latitude  FLOAT NOT NULL,
    Longitude FLOAT NOT NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Addresses_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Addresses_UserId ON dbo.Addresses(UserId);
GO

-- ─── LAUNDRY STORES ────────────────────────────────────────────────
IF OBJECT_ID('dbo.Stores') IS NULL
CREATE TABLE dbo.Stores (
    Id                       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OwnerId                  UNIQUEIDENTIFIER NOT NULL,
    Name                     NVARCHAR(150) NOT NULL,
    [Description]            NVARCHAR(1000) NOT NULL DEFAULT '',
    Phone                    NVARCHAR(20)  NOT NULL,
    Email                    NVARCHAR(200) NOT NULL,
    [Address]                NVARCHAR(300) NOT NULL,
    Area                     NVARCHAR(100) NOT NULL,
    City                     NVARCHAR(100) NOT NULL DEFAULT 'Lagos',
    [State]                  NVARCHAR(100) NOT NULL DEFAULT 'Lagos',
    Latitude                 FLOAT NOT NULL,
    Longitude                FLOAT NOT NULL,
    LogoUrl                  NVARCHAR(500) NULL,
    BannerUrl                NVARCHAR(500) NULL,
    [Status]                 INT NOT NULL DEFAULT 0,    -- 0=Pending,1=Active,2=Suspended,3=Rejected
    IsVerified               BIT NOT NULL DEFAULT 0,
    IsTopRated               BIT NOT NULL DEFAULT 0,
    FastPickup               BIT NOT NULL DEFAULT 0,
    EstimatedPickupMinutes   INT NOT NULL DEFAULT 30,
    AverageRating            DECIMAL(3,2) NOT NULL DEFAULT 0,
    TotalReviews             INT NOT NULL DEFAULT 0,
    TotalOrders              INT NOT NULL DEFAULT 0,
    PickupFee                DECIMAL(18,2) NOT NULL DEFAULT 1000,
    DeliveryFee              DECIMAL(18,2) NOT NULL DEFAULT 1000,
    OpeningHours             NVARCHAR(100) NULL,
    CreatedAt                DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt                DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Stores_Users FOREIGN KEY (OwnerId) REFERENCES dbo.Users(Id)
);
CREATE INDEX IX_Stores_OwnerId ON dbo.Stores(OwnerId);
CREATE INDEX IX_Stores_Status  ON dbo.Stores([Status]);
CREATE INDEX IX_Stores_Area    ON dbo.Stores(Area);
GO

-- ─── SERVICE OFFERINGS ─────────────────────────────────────────────
IF OBJECT_ID('dbo.ServiceOfferings') IS NULL
CREATE TABLE dbo.ServiceOfferings (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    StoreId         UNIQUEIDENTIFIER NOT NULL,
    ServiceType     INT NOT NULL,                       -- 0=Wash,1=Wash+Iron,2=Iron,3=DryClean,4=W&F,5=Special
    Name            NVARCHAR(100) NOT NULL,
    [Description]   NVARCHAR(500) NULL,
    PricingMode     INT NOT NULL DEFAULT 0,             -- 0=PerKg,1=PerItem,2=Fixed
    BasePrice       DECIMAL(18,2) NOT NULL,
    MinimumCharge   DECIMAL(18,2) NOT NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    EstimatedHours  INT NOT NULL DEFAULT 24,
    CONSTRAINT FK_Services_Stores FOREIGN KEY (StoreId) REFERENCES dbo.Stores(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Services_StoreId      ON dbo.ServiceOfferings(StoreId);
CREATE INDEX IX_Services_StoreType    ON dbo.ServiceOfferings(StoreId, ServiceType);
GO

-- ─── RIDERS ────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Riders') IS NULL
CREATE TABLE dbo.Riders (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId           UNIQUEIDENTIFIER NOT NULL,
    VehicleType      NVARCHAR(50)  NOT NULL DEFAULT 'Motorcycle',
    VehiclePlate     NVARCHAR(20)  NULL,
    [Status]         INT NOT NULL DEFAULT 0,            -- 0=Offline,1=Online,2=OnDelivery
    CurrentLatitude  FLOAT NOT NULL DEFAULT 0,
    CurrentLongitude FLOAT NOT NULL DEFAULT 0,
    TotalDeliveries  INT NOT NULL DEFAULT 0,
    TotalEarnings    DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Riders_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
CREATE UNIQUE INDEX IX_Riders_UserId ON dbo.Riders(UserId);
CREATE INDEX IX_Riders_Status ON dbo.Riders([Status]);
GO

-- ─── ORDERS ────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Orders') IS NULL
CREATE TABLE dbo.Orders (
    Id                     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderNumber            NVARCHAR(20) NOT NULL,
    CustomerId             UNIQUEIDENTIFIER NOT NULL,
    StoreId                UNIQUEIDENTIFIER NOT NULL,
    ServiceOfferingId      UNIQUEIDENTIFIER NOT NULL,
    RiderId                UNIQUEIDENTIFIER NULL,
    [Status]               INT NOT NULL DEFAULT 0,
    PaymentState           INT NOT NULL DEFAULT 0,
    PaymentMethod          INT NOT NULL DEFAULT 0,
    EstimatedWeight        DECIMAL(18,2) NOT NULL DEFAULT 0,
    EstimatedPieces        INT NOT NULL DEFAULT 0,
    EstimatedLaundryCost   DECIMAL(18,2) NOT NULL DEFAULT 0,
    ActualLaundryCost      DECIMAL(18,2) NOT NULL DEFAULT 0,
    PickupFee              DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryFee            DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalEstimate          DECIMAL(18,2) NOT NULL DEFAULT 0,
    ActualTotal            DECIMAL(18,2) NOT NULL DEFAULT 0,
    PickupFeeAmount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    BalanceAmount          DECIMAL(18,2) NOT NULL DEFAULT 0,
    AdminPriceOverride     BIT NOT NULL DEFAULT 0,
    AdminOverrideReason    NVARCHAR(500) NULL,
    PickupAddress          NVARCHAR(500) NOT NULL,
    DeliveryAddress        NVARCHAR(500) NOT NULL,
    RequestedPickupDate    DATETIME2 NOT NULL,
    RequestedPickupSlot    NVARCHAR(50) NOT NULL,
    SpecialInstructions    NVARCHAR(1000) NULL,
    ConfirmedAt            DATETIME2 NULL,
    PickupFeePaidAt        DATETIME2 NULL,
    RiderAssignedAt        DATETIME2 NULL,
    PickedUpAt             DATETIME2 NULL,
    ProcessingStartedAt    DATETIME2 NULL,
    ReadyAt                DATETIME2 NULL,
    OutForDeliveryAt       DATETIME2 NULL,
    DeliveredAt            DATETIME2 NULL,
    BalancePaidAt          DATETIME2 NULL,
    CompletedAt            DATETIME2 NULL,
    CreatedAt              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Orders_Customer    FOREIGN KEY (CustomerId)        REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Orders_Store       FOREIGN KEY (StoreId)           REFERENCES dbo.Stores(Id),
    CONSTRAINT FK_Orders_Service     FOREIGN KEY (ServiceOfferingId) REFERENCES dbo.ServiceOfferings(Id),
    CONSTRAINT FK_Orders_Rider       FOREIGN KEY (RiderId)           REFERENCES dbo.Riders(Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IX_Orders_OrderNumber ON dbo.Orders(OrderNumber);
CREATE INDEX IX_Orders_CustomerId         ON dbo.Orders(CustomerId);
CREATE INDEX IX_Orders_StoreId            ON dbo.Orders(StoreId);
CREATE INDEX IX_Orders_RiderId            ON dbo.Orders(RiderId);
CREATE INDEX IX_Orders_Status             ON dbo.Orders([Status]);
CREATE INDEX IX_Orders_CreatedAt          ON dbo.Orders(CreatedAt);
GO

-- ─── ORDER STATUS HISTORY ──────────────────────────────────────────
IF OBJECT_ID('dbo.OrderStatusHistories') IS NULL
CREATE TABLE dbo.OrderStatusHistories (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderId          UNIQUEIDENTIFIER NOT NULL,
    [Status]         INT NOT NULL,
    Note             NVARCHAR(500) NULL,
    ChangedByUserId  UNIQUEIDENTIFIER NULL,
    ChangedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_OSH_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE
);
CREATE INDEX IX_OSH_OrderId ON dbo.OrderStatusHistories(OrderId);
GO

-- ─── PAYMENTS ──────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Payments') IS NULL
CREATE TABLE dbo.Payments (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderId       UNIQUEIDENTIFIER NOT NULL,
    PaymentRef    NVARCHAR(50)  NOT NULL,
    Amount        DECIMAL(18,2) NOT NULL,
    Method        INT NOT NULL,
    [Type]        NVARCHAR(30)  NOT NULL,
    IsSuccessful  BIT NOT NULL DEFAULT 0,
    GatewayRef    NVARCHAR(200) NULL,
    Notes         NVARCHAR(500) NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id)
);
CREATE UNIQUE INDEX IX_Payments_PaymentRef ON dbo.Payments(PaymentRef);
CREATE INDEX IX_Payments_OrderId ON dbo.Payments(OrderId);
GO

-- ─── REVIEWS ───────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Reviews') IS NULL
CREATE TABLE dbo.Reviews (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderId     UNIQUEIDENTIFIER NOT NULL,
    CustomerId  UNIQUEIDENTIFIER NOT NULL,
    StoreId     UNIQUEIDENTIFIER NOT NULL,
    Rating      INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Comment     NVARCHAR(1000) NULL,
    PhotoUrl    NVARCHAR(500) NULL,
    IsVisible   BIT NOT NULL DEFAULT 1,
    IsFlagged   BIT NOT NULL DEFAULT 0,
    AdminNote   NVARCHAR(500) NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Reviews_Orders   FOREIGN KEY (OrderId)    REFERENCES dbo.Orders(Id),
    CONSTRAINT FK_Reviews_Users    FOREIGN KEY (CustomerId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_Reviews_Stores   FOREIGN KEY (StoreId)    REFERENCES dbo.Stores(Id)
);
CREATE UNIQUE INDEX IX_Reviews_OrderId ON dbo.Reviews(OrderId);
CREATE INDEX IX_Reviews_StoreId        ON dbo.Reviews(StoreId);
CREATE INDEX IX_Reviews_CustomerId     ON dbo.Reviews(CustomerId);
GO

-- ─── DISPUTES ──────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Disputes') IS NULL
CREATE TABLE dbo.Disputes (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    OrderId           UNIQUEIDENTIFIER NOT NULL,
    RaisedByUserId    UNIQUEIDENTIFIER NOT NULL,
    [Type]            INT NOT NULL,
    [Description]     NVARCHAR(2000) NOT NULL,
    EvidenceUrl       NVARCHAR(500) NULL,
    [Status]          INT NOT NULL DEFAULT 0,
    Resolution        INT NULL,
    AdminNotes        NVARCHAR(3000) NULL,
    ResolutionNote    NVARCHAR(1000) NULL,
    RefundAmount      DECIMAL(18,2) NULL,
    AssignedAdminId   UNIQUEIDENTIFIER NULL,
    CreatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ResolvedAt        DATETIME2 NULL,
    CONSTRAINT FK_Disputes_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
    CONSTRAINT FK_Disputes_Users  FOREIGN KEY (RaisedByUserId) REFERENCES dbo.Users(Id)
);
CREATE UNIQUE INDEX IX_Disputes_OrderId ON dbo.Disputes(OrderId);
CREATE INDEX IX_Disputes_RaisedBy        ON dbo.Disputes(RaisedByUserId);
CREATE INDEX IX_Disputes_Status          ON dbo.Disputes([Status]);
GO

IF OBJECT_ID('dbo.DisputeMessages') IS NULL
CREATE TABLE dbo.DisputeMessages (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DisputeId        UNIQUEIDENTIFIER NOT NULL,
    SenderId         UNIQUEIDENTIFIER NOT NULL,
    [Message]        NVARCHAR(2000) NOT NULL,
    IsAdminMessage   BIT NOT NULL DEFAULT 0,
    SentAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_DM_Disputes FOREIGN KEY (DisputeId) REFERENCES dbo.Disputes(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DM_Users    FOREIGN KEY (SenderId)  REFERENCES dbo.Users(Id)
);
CREATE INDEX IX_DM_DisputeId ON dbo.DisputeMessages(DisputeId);
GO

-- ─── OTP ───────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.OtpCodes') IS NULL
CREATE TABLE dbo.OtpCodes (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Phone         NVARCHAR(20) NOT NULL,
    Code          NVARCHAR(10) NOT NULL,
    IsUsed        BIT NOT NULL DEFAULT 0,
    AttemptCount  INT NOT NULL DEFAULT 0,
    ExpiresAt     DATETIME2 NOT NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Otp_Phone     ON dbo.OtpCodes(Phone);
CREATE INDEX IX_Otp_PhoneUsed ON dbo.OtpCodes(Phone, IsUsed);
GO

-- ─── REFRESH TOKENS ────────────────────────────────────────────────
IF OBJECT_ID('dbo.RefreshTokens') IS NULL
CREATE TABLE dbo.RefreshTokens (
    Id         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId     UNIQUEIDENTIFIER NOT NULL,
    Token      NVARCHAR(500) NOT NULL,
    ExpiresAt  DATETIME2 NOT NULL,
    IsRevoked  BIT NOT NULL DEFAULT 0,
    CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_RT_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_RT_Token ON dbo.RefreshTokens(Token);
CREATE INDEX IX_RT_UserId        ON dbo.RefreshTokens(UserId);
GO

-- ─── SYSTEM CONFIG ─────────────────────────────────────────────────
IF OBJECT_ID('dbo.SystemConfigs') IS NULL
CREATE TABLE dbo.SystemConfigs (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Key]             NVARCHAR(100) NOT NULL,
    [Value]           NVARCHAR(2000) NOT NULL,
    [Description]     NVARCHAR(500) NULL,
    UpdatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedByUserId   UNIQUEIDENTIFIER NULL
);
CREATE UNIQUE INDEX IX_SC_Key ON dbo.SystemConfigs([Key]);
GO

-- ─── AUDIT LOG ─────────────────────────────────────────────────────
IF OBJECT_ID('dbo.AuditLogs') IS NULL
CREATE TABLE dbo.AuditLogs (
    Id         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId     UNIQUEIDENTIFIER NULL,
    Action     NVARCHAR(100) NOT NULL,
    Entity     NVARCHAR(100) NOT NULL,
    EntityId   UNIQUEIDENTIFIER NULL,
    OldValue   NVARCHAR(2000) NULL,
    NewValue   NVARCHAR(2000) NULL,
    IpAddress  NVARCHAR(50) NULL,
    CreatedAt  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AL_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
CREATE INDEX IX_AL_UserId       ON dbo.AuditLogs(UserId);
CREATE INDEX IX_AL_CreatedAt    ON dbo.AuditLogs(CreatedAt);
CREATE INDEX IX_AL_EntityEntity ON dbo.AuditLogs(Entity, EntityId);
GO

-- ─── NOTIFICATIONS ─────────────────────────────────────────────────
IF OBJECT_ID('dbo.Notifications') IS NULL
CREATE TABLE dbo.Notifications (
    Id        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UserId    UNIQUEIDENTIFIER NULL,
    Title     NVARCHAR(200) NOT NULL,
    [Message] NVARCHAR(2000) NOT NULL,
    [Type]    NVARCHAR(20) NOT NULL DEFAULT 'sms',
    IsSent    BIT NOT NULL DEFAULT 0,
    IsRead    BIT NOT NULL DEFAULT 0,
    SentAt    DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_N_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
CREATE INDEX IX_N_UserId ON dbo.Notifications(UserId);
CREATE INDEX IX_N_IsRead ON dbo.Notifications(IsRead);
GO

PRINT 'VYRONDB schema created successfully.';
PRINT 'EF Core migrations will seed initial data (admin, store owners, demo stores, services).';
GO
