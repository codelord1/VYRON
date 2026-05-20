using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{

/// <summary>
/// Additive migration — adds Coupons + CouponUsages tables and seeds VYRON20 promo.
/// All DDL is idempotent (IF NOT EXISTS guards) so it is safe on the current DB
/// as well as fresh installations.
/// </summary>
[DbContext(typeof(VyronDbContext))]
[Migration("20260520150000_AddCoupons")]
public partial class AddCoupons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
-- ══════════════════════════════════════════════════════════════════
--  COUPONS table
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Coupons') AND type = 'U')
BEGIN
    CREATE TABLE Coupons (
        Id                    uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Code                  nvarchar(50)     NOT NULL,
        Description           nvarchar(500)    NULL,
        DiscountType          nvarchar(20)     NOT NULL DEFAULT 'Percentage',
        DiscountValue         decimal(18,2)    NOT NULL DEFAULT 0,
        MaxUsesPerCustomer    int              NOT NULL DEFAULT 1,
        GlobalMaxUses         int              NOT NULL DEFAULT 0,
        TotalUses             int              NOT NULL DEFAULT 0,
        StartDate             datetime2        NULL,
        EndDate               datetime2        NULL,
        IsActive              bit              NOT NULL DEFAULT 1,
        AppliesToFirstNOrders int              NOT NULL DEFAULT 0,
        CreatedAt             datetime2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt             datetime2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId       uniqueidentifier NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Coupons') AND name = 'IX_Coupons_Code')
    CREATE UNIQUE INDEX IX_Coupons_Code ON Coupons(Code);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Coupons') AND name = 'IX_Coupons_IsActive')
    CREATE INDEX IX_Coupons_IsActive ON Coupons(IsActive);

-- ══════════════════════════════════════════════════════════════════
--  COUPON USAGES table
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CouponUsages') AND type = 'U')
BEGIN
    CREATE TABLE CouponUsages (
        Id              uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,
        CouponId        uniqueidentifier NOT NULL,
        CustomerId      uniqueidentifier NOT NULL,
        OrderId         uniqueidentifier NULL,
        DiscountApplied decimal(18,2)    NOT NULL DEFAULT 0,
        UsedAt          datetime2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CouponUsages_Coupons_CouponId
            FOREIGN KEY (CouponId) REFERENCES Coupons(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CouponUsages_Users_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES Users(Id),
        CONSTRAINT FK_CouponUsages_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE SET NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CouponUsages') AND name = 'IX_CouponUsages_CouponId_CustomerId')
    CREATE INDEX IX_CouponUsages_CouponId_CustomerId ON CouponUsages(CouponId, CustomerId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CouponUsages') AND name = 'IX_CouponUsages_CustomerId')
    CREATE INDEX IX_CouponUsages_CustomerId ON CouponUsages(CustomerId);

-- ══════════════════════════════════════════════════════════════════
--  SEED: VYRON20 — 20% off first 3 orders
-- ══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM Coupons WHERE Code = 'VYRON20')
    INSERT INTO Coupons (Id, Code, Description, DiscountType, DiscountValue,
                         MaxUsesPerCustomer, GlobalMaxUses, AppliesToFirstNOrders,
                         IsActive, CreatedAt, UpdatedAt)
    VALUES (NEWID(), 'VYRON20', '20% off your first 3 orders', 'Percentage', 20,
            3, 0, 3, 1, GETUTCDATE(), GETUTCDATE());
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('CouponUsages') AND type = 'U')
    DROP TABLE CouponUsages;

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('Coupons') AND type = 'U')
    DROP TABLE Coupons;
");
    }
}

}
