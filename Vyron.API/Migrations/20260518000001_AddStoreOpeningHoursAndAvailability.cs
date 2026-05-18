using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{
    /// <summary>
    /// Adds structured opening-hours availability fields to LaundryStore.
    /// Fully idempotent — safe to re-run if the previous attempt was interrupted.
    /// </summary>
    [DbContext(typeof(VyronDbContext))]
    [Migration("20260518000001_AddStoreOpeningHoursAndAvailability")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0058")]
    public partial class AddStoreOpeningHoursAndAvailability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- IsManuallyClosed: store owner can force-close the store regardless of hours
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'IsManuallyClosed')
    ALTER TABLE Stores ADD IsManuallyClosed bit NOT NULL DEFAULT 0;

-- Structured opening/closing time (maps to TimeOnly / SQL Server 'time')
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningTime')
    ALTER TABLE Stores ADD OpeningTime time NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'ClosingTime')
    ALTER TABLE Stores ADD ClosingTime time NULL;

-- Comma-separated day abbreviations e.g. 'Mon,Tue,Wed,Thu,Fri,Sat'
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningDays')
    ALTER TABLE Stores ADD OpeningDays nvarchar(100) NULL;

-- Audit timestamps for manual open/close events
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastOpenedAt')
    ALTER TABLE Stores ADD LastOpenedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastClosedAt')
    ALTER TABLE Stores ADD LastClosedAt datetime2 NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastClosedAt')
    ALTER TABLE Stores DROP COLUMN LastClosedAt;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'LastOpenedAt')
    ALTER TABLE Stores DROP COLUMN LastOpenedAt;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningDays')
    ALTER TABLE Stores DROP COLUMN OpeningDays;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'ClosingTime')
    ALTER TABLE Stores DROP COLUMN ClosingTime;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'OpeningTime')
    ALTER TABLE Stores DROP COLUMN OpeningTime;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Stores') AND name = 'IsManuallyClosed')
    ALTER TABLE Stores DROP COLUMN IsManuallyClosed;
");
        }
    }
}
