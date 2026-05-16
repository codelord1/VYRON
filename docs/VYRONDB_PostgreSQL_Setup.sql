-- ═══════════════════════════════════════════════════════════════════
-- VYRON Laundry Marketplace v3.0 — PostgreSQL Setup Script
-- Normalized schema with UUID primary keys, indexes, FK integrity.
--
-- USAGE:
-- 1. Run this script against PostgreSQL (or let EF migrate on first run).
-- 2. Update appsettings.json: "Database": { "Provider": "PostgreSQL" }
-- 3. Update "ConnectionStrings:DefaultConnection" with your connection.
-- ═══════════════════════════════════════════════════════════════════

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ─── USERS ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Users" (
    "Id"           UUID PRIMARY KEY,
    "Phone"        VARCHAR(20)  NOT NULL,
    "Email"        VARCHAR(200),
    "FullName"     VARCHAR(120) NOT NULL,
    "PasswordHash" VARCHAR(500),
    "Role"         INT NOT NULL DEFAULT 0,
    "IsActive"     BOOLEAN NOT NULL DEFAULT TRUE,
    "ProfilePhoto" VARCHAR(500),
    "CreatedAt"    TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "LastLoginAt"  TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Phone" ON "Users"("Phone");
CREATE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users"("Email");
CREATE INDEX IF NOT EXISTS "IX_Users_Role"  ON "Users"("Role");

-- ─── ADDRESSES ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Addresses" (
    "Id"        UUID PRIMARY KEY,
    "UserId"    UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Label"     VARCHAR(50) NOT NULL,
    "Street"    VARCHAR(300) NOT NULL,
    "Area"      VARCHAR(100) NOT NULL,
    "City"      VARCHAR(100) NOT NULL DEFAULT 'Lagos',
    "State"     VARCHAR(100) NOT NULL DEFAULT 'Lagos',
    "Landmark"  VARCHAR(200),
    "Latitude"  DOUBLE PRECISION NOT NULL,
    "Longitude" DOUBLE PRECISION NOT NULL,
    "IsDefault" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_Addresses_UserId" ON "Addresses"("UserId");

-- ─── STORES ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Stores" (
    "Id"                     UUID PRIMARY KEY,
    "OwnerId"                UUID NOT NULL REFERENCES "Users"("Id"),
    "Name"                   VARCHAR(150) NOT NULL,
    "Description"            VARCHAR(1000) NOT NULL DEFAULT '',
    "Phone"                  VARCHAR(20)  NOT NULL,
    "Email"                  VARCHAR(200) NOT NULL,
    "Address"                VARCHAR(300) NOT NULL,
    "Area"                   VARCHAR(100) NOT NULL,
    "City"                   VARCHAR(100) NOT NULL DEFAULT 'Lagos',
    "State"                  VARCHAR(100) NOT NULL DEFAULT 'Lagos',
    "Latitude"               DOUBLE PRECISION NOT NULL,
    "Longitude"              DOUBLE PRECISION NOT NULL,
    "LogoUrl"                VARCHAR(500),
    "BannerUrl"              VARCHAR(500),
    "Status"                 INT NOT NULL DEFAULT 0,
    "IsVerified"             BOOLEAN NOT NULL DEFAULT FALSE,
    "IsTopRated"             BOOLEAN NOT NULL DEFAULT FALSE,
    "FastPickup"             BOOLEAN NOT NULL DEFAULT FALSE,
    "EstimatedPickupMinutes" INT NOT NULL DEFAULT 30,
    "AverageRating"          NUMERIC(3,2) NOT NULL DEFAULT 0,
    "TotalReviews"           INT NOT NULL DEFAULT 0,
    "TotalOrders"            INT NOT NULL DEFAULT 0,
    "PickupFee"              NUMERIC(18,2) NOT NULL DEFAULT 1000,
    "DeliveryFee"            NUMERIC(18,2) NOT NULL DEFAULT 1000,
    "OpeningHours"           VARCHAR(100),
    "CreatedAt"              TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "UpdatedAt"              TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_Stores_OwnerId" ON "Stores"("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Stores_Status"  ON "Stores"("Status");
CREATE INDEX IF NOT EXISTS "IX_Stores_Area"    ON "Stores"("Area");

-- ─── SERVICE OFFERINGS ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "ServiceOfferings" (
    "Id"             UUID PRIMARY KEY,
    "StoreId"        UUID NOT NULL REFERENCES "Stores"("Id") ON DELETE CASCADE,
    "ServiceType"    INT NOT NULL,
    "Name"           VARCHAR(100) NOT NULL,
    "Description"    VARCHAR(500),
    "PricingMode"    INT NOT NULL DEFAULT 0,
    "BasePrice"      NUMERIC(18,2) NOT NULL,
    "MinimumCharge"  NUMERIC(18,2) NOT NULL,
    "IsActive"       BOOLEAN NOT NULL DEFAULT TRUE,
    "EstimatedHours" INT NOT NULL DEFAULT 24
);
CREATE INDEX IF NOT EXISTS "IX_Services_StoreId"   ON "ServiceOfferings"("StoreId");
CREATE INDEX IF NOT EXISTS "IX_Services_StoreType" ON "ServiceOfferings"("StoreId", "ServiceType");

-- ─── RIDERS ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Riders" (
    "Id"               UUID PRIMARY KEY,
    "UserId"           UUID NOT NULL REFERENCES "Users"("Id"),
    "VehicleType"      VARCHAR(50)  NOT NULL DEFAULT 'Motorcycle',
    "VehiclePlate"     VARCHAR(20),
    "Status"           INT NOT NULL DEFAULT 0,
    "CurrentLatitude"  DOUBLE PRECISION NOT NULL DEFAULT 0,
    "CurrentLongitude" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "TotalDeliveries"  INT NOT NULL DEFAULT 0,
    "TotalEarnings"    NUMERIC(18,2) NOT NULL DEFAULT 0,
    "CreatedAt"        TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Riders_UserId" ON "Riders"("UserId");
CREATE INDEX IF NOT EXISTS "IX_Riders_Status" ON "Riders"("Status");

-- ─── ORDERS ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Orders" (
    "Id"                    UUID PRIMARY KEY,
    "OrderNumber"           VARCHAR(20) NOT NULL,
    "CustomerId"            UUID NOT NULL REFERENCES "Users"("Id"),
    "StoreId"               UUID NOT NULL REFERENCES "Stores"("Id"),
    "ServiceOfferingId"     UUID NOT NULL REFERENCES "ServiceOfferings"("Id"),
    "RiderId"               UUID REFERENCES "Riders"("Id") ON DELETE SET NULL,
    "Status"                INT NOT NULL DEFAULT 0,
    "PaymentState"          INT NOT NULL DEFAULT 0,
    "PaymentMethod"         INT NOT NULL DEFAULT 0,
    "EstimatedWeight"       NUMERIC(18,2) NOT NULL DEFAULT 0,
    "EstimatedPieces"       INT NOT NULL DEFAULT 0,
    "EstimatedLaundryCost"  NUMERIC(18,2) NOT NULL DEFAULT 0,
    "ActualLaundryCost"     NUMERIC(18,2) NOT NULL DEFAULT 0,
    "PickupFee"             NUMERIC(18,2) NOT NULL DEFAULT 0,
    "DeliveryFee"           NUMERIC(18,2) NOT NULL DEFAULT 0,
    "TotalEstimate"         NUMERIC(18,2) NOT NULL DEFAULT 0,
    "ActualTotal"           NUMERIC(18,2) NOT NULL DEFAULT 0,
    "PickupFeeAmount"       NUMERIC(18,2) NOT NULL DEFAULT 0,
    "BalanceAmount"         NUMERIC(18,2) NOT NULL DEFAULT 0,
    "AdminPriceOverride"    BOOLEAN NOT NULL DEFAULT FALSE,
    "AdminOverrideReason"   VARCHAR(500),
    "PickupAddress"         VARCHAR(500) NOT NULL,
    "DeliveryAddress"       VARCHAR(500) NOT NULL,
    "RequestedPickupDate"   TIMESTAMP NOT NULL,
    "RequestedPickupSlot"   VARCHAR(50) NOT NULL,
    "SpecialInstructions"   VARCHAR(1000),
    "ConfirmedAt"           TIMESTAMP,
    "PickupFeePaidAt"       TIMESTAMP,
    "RiderAssignedAt"       TIMESTAMP,
    "PickedUpAt"            TIMESTAMP,
    "ProcessingStartedAt"   TIMESTAMP,
    "ReadyAt"               TIMESTAMP,
    "OutForDeliveryAt"      TIMESTAMP,
    "DeliveredAt"           TIMESTAMP,
    "BalancePaidAt"         TIMESTAMP,
    "CompletedAt"           TIMESTAMP,
    "CreatedAt"             TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "UpdatedAt"             TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_OrderNumber" ON "Orders"("OrderNumber");
CREATE INDEX IF NOT EXISTS "IX_Orders_CustomerId" ON "Orders"("CustomerId");
CREATE INDEX IF NOT EXISTS "IX_Orders_StoreId"    ON "Orders"("StoreId");
CREATE INDEX IF NOT EXISTS "IX_Orders_RiderId"    ON "Orders"("RiderId");
CREATE INDEX IF NOT EXISTS "IX_Orders_Status"     ON "Orders"("Status");
CREATE INDEX IF NOT EXISTS "IX_Orders_CreatedAt"  ON "Orders"("CreatedAt");

-- ─── ORDER STATUS HISTORY ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "OrderStatusHistories" (
    "Id"              UUID PRIMARY KEY,
    "OrderId"         UUID NOT NULL REFERENCES "Orders"("Id") ON DELETE CASCADE,
    "Status"          INT NOT NULL,
    "Note"            VARCHAR(500),
    "ChangedByUserId" UUID,
    "ChangedAt"       TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_OSH_OrderId" ON "OrderStatusHistories"("OrderId");

-- ─── PAYMENTS ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Payments" (
    "Id"           UUID PRIMARY KEY,
    "OrderId"      UUID NOT NULL REFERENCES "Orders"("Id"),
    "PaymentRef"   VARCHAR(50)  NOT NULL,
    "Amount"       NUMERIC(18,2) NOT NULL,
    "Method"       INT NOT NULL,
    "Type"         VARCHAR(30)  NOT NULL,
    "IsSuccessful" BOOLEAN NOT NULL DEFAULT FALSE,
    "GatewayRef"   VARCHAR(200),
    "Notes"        VARCHAR(500),
    "CreatedAt"    TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_PaymentRef" ON "Payments"("PaymentRef");
CREATE INDEX IF NOT EXISTS "IX_Payments_OrderId" ON "Payments"("OrderId");

-- ─── REVIEWS ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Reviews" (
    "Id"         UUID PRIMARY KEY,
    "OrderId"    UUID NOT NULL REFERENCES "Orders"("Id"),
    "CustomerId" UUID NOT NULL REFERENCES "Users"("Id"),
    "StoreId"    UUID NOT NULL REFERENCES "Stores"("Id"),
    "Rating"     INT NOT NULL CHECK ("Rating" BETWEEN 1 AND 5),
    "Comment"    VARCHAR(1000),
    "PhotoUrl"   VARCHAR(500),
    "IsVisible"  BOOLEAN NOT NULL DEFAULT TRUE,
    "IsFlagged"  BOOLEAN NOT NULL DEFAULT FALSE,
    "AdminNote"  VARCHAR(500),
    "CreatedAt"  TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Reviews_OrderId" ON "Reviews"("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Reviews_StoreId" ON "Reviews"("StoreId");
CREATE INDEX IF NOT EXISTS "IX_Reviews_CustomerId" ON "Reviews"("CustomerId");

-- ─── DISPUTES ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Disputes" (
    "Id"              UUID PRIMARY KEY,
    "OrderId"         UUID NOT NULL REFERENCES "Orders"("Id"),
    "RaisedByUserId"  UUID NOT NULL REFERENCES "Users"("Id"),
    "Type"            INT NOT NULL,
    "Description"     VARCHAR(2000) NOT NULL,
    "EvidenceUrl"     VARCHAR(500),
    "Status"          INT NOT NULL DEFAULT 0,
    "Resolution"      INT,
    "AdminNotes"      VARCHAR(3000),
    "ResolutionNote"  VARCHAR(1000),
    "RefundAmount"    NUMERIC(18,2),
    "AssignedAdminId" UUID,
    "CreatedAt"       TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "UpdatedAt"       TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "ResolvedAt"      TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Disputes_OrderId" ON "Disputes"("OrderId");
CREATE INDEX IF NOT EXISTS "IX_Disputes_RaisedBy" ON "Disputes"("RaisedByUserId");
CREATE INDEX IF NOT EXISTS "IX_Disputes_Status"   ON "Disputes"("Status");

CREATE TABLE IF NOT EXISTS "DisputeMessages" (
    "Id"             UUID PRIMARY KEY,
    "DisputeId"      UUID NOT NULL REFERENCES "Disputes"("Id") ON DELETE CASCADE,
    "SenderId"       UUID NOT NULL REFERENCES "Users"("Id"),
    "Message"        VARCHAR(2000) NOT NULL,
    "IsAdminMessage" BOOLEAN NOT NULL DEFAULT FALSE,
    "SentAt"         TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_DM_DisputeId" ON "DisputeMessages"("DisputeId");

-- ─── OTP ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "OtpCodes" (
    "Id"           UUID PRIMARY KEY,
    "Phone"        VARCHAR(20) NOT NULL,
    "Code"         VARCHAR(10) NOT NULL,
    "IsUsed"       BOOLEAN NOT NULL DEFAULT FALSE,
    "AttemptCount" INT NOT NULL DEFAULT 0,
    "ExpiresAt"    TIMESTAMP NOT NULL,
    "CreatedAt"    TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_Otp_Phone"     ON "OtpCodes"("Phone");
CREATE INDEX IF NOT EXISTS "IX_Otp_PhoneUsed" ON "OtpCodes"("Phone", "IsUsed");

-- ─── REFRESH TOKENS ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id"        UUID PRIMARY KEY,
    "UserId"    UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Token"     VARCHAR(500) NOT NULL,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "IsRevoked" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RT_Token" ON "RefreshTokens"("Token");
CREATE INDEX IF NOT EXISTS "IX_RT_UserId"        ON "RefreshTokens"("UserId");

-- ─── SYSTEM CONFIG ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "SystemConfigs" (
    "Id"              UUID PRIMARY KEY,
    "Key"             VARCHAR(100) NOT NULL,
    "Value"           VARCHAR(2000) NOT NULL,
    "Description"     VARCHAR(500),
    "UpdatedAt"       TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
    "UpdatedByUserId" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SC_Key" ON "SystemConfigs"("Key");

-- ─── AUDIT LOG ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id"        UUID PRIMARY KEY,
    "UserId"    UUID REFERENCES "Users"("Id") ON DELETE SET NULL,
    "Action"    VARCHAR(100) NOT NULL,
    "Entity"    VARCHAR(100) NOT NULL,
    "EntityId"  UUID,
    "OldValue"  VARCHAR(2000),
    "NewValue"  VARCHAR(2000),
    "IpAddress" VARCHAR(50),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_AL_UserId"       ON "AuditLogs"("UserId");
CREATE INDEX IF NOT EXISTS "IX_AL_CreatedAt"    ON "AuditLogs"("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_AL_EntityEntity" ON "AuditLogs"("Entity", "EntityId");

-- ─── NOTIFICATIONS ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id"        UUID PRIMARY KEY,
    "UserId"    UUID REFERENCES "Users"("Id") ON DELETE SET NULL,
    "Title"     VARCHAR(200) NOT NULL,
    "Message"   VARCHAR(2000) NOT NULL,
    "Type"      VARCHAR(20) NOT NULL DEFAULT 'sms',
    "IsSent"    BOOLEAN NOT NULL DEFAULT FALSE,
    "IsRead"    BOOLEAN NOT NULL DEFAULT FALSE,
    "SentAt"    TIMESTAMP,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
);
CREATE INDEX IF NOT EXISTS "IX_N_UserId" ON "Notifications"("UserId");
CREATE INDEX IF NOT EXISTS "IX_N_IsRead" ON "Notifications"("IsRead");

\echo 'VYRONDB schema created successfully on PostgreSQL.'
\echo 'EF Core migrations will seed initial data (admin, store owners, demo stores, services).'
