using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{
    /// <summary>
    /// Adds DeliveryRiderId to Orders — separate from the existing RiderId (pickup rider).
    /// Fully idempotent — safe to re-run.
    /// </summary>
    [DbContext(typeof(VyronDbContext))]
    [Migration("20260518000002_AddDeliveryRiderAssignment")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddDeliveryRiderAssignment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Add DeliveryRiderId column (nullable FK to Riders)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'DeliveryRiderId')
    ALTER TABLE Orders ADD DeliveryRiderId uniqueidentifier NULL;

-- Add FK constraint: NO ACTION to avoid multiple cascade-path error (Riders -> Orders already exists via RiderId)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Orders_Riders_DeliveryRiderId')
    ALTER TABLE Orders
        ADD CONSTRAINT FK_Orders_Riders_DeliveryRiderId
        FOREIGN KEY (DeliveryRiderId) REFERENCES Riders(Id)
        ON DELETE NO ACTION;

-- Index for fast lookups by delivery rider
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_DeliveryRiderId')
    CREATE INDEX IX_Orders_DeliveryRiderId ON Orders(DeliveryRiderId);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Orders') AND name = 'IX_Orders_DeliveryRiderId')
    DROP INDEX IX_Orders_DeliveryRiderId ON Orders;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Orders_Riders_DeliveryRiderId')
    ALTER TABLE Orders DROP CONSTRAINT FK_Orders_Riders_DeliveryRiderId;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'DeliveryRiderId')
    ALTER TABLE Orders DROP COLUMN DeliveryRiderId;
");
        }
    }
}
