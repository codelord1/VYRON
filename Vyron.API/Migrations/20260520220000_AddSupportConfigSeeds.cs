using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Vyron.API.Data;

#nullable disable

namespace Vyron.API.Migrations
{

/// <summary>
/// Seeds 4 customer-support contact keys into SystemConfigs.
/// All inserts are idempotent (IF NOT EXISTS guards) — safe on existing DBs.
/// Keys added:
///   CustomerSupportPhone    — phone number shown on Track Order / support screens
///   CustomerSupportEmail    — support email address
///   CustomerSupportWhatsApp — WhatsApp number (include country code)
///   CustomerSupportChatUrl  — optional live-chat URL; empty = no chat button
/// </summary>
[DbContext(typeof(VyronDbContext))]
[Migration("20260520220000_AddSupportConfigSeeds")]
public partial class AddSupportConfigSeeds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
-- ═══════════════════════════════════════════════════════════════════
--  CUSTOMER SUPPORT CONTACTS  (idempotent inserts)
-- ═══════════════════════════════════════════════════════════════════
IF NOT EXISTS (SELECT 1 FROM SystemConfigs WHERE [Key] = 'CustomerSupportPhone')
    INSERT INTO SystemConfigs (Id, [Key], Value, Description, UpdatedAt)
    VALUES ('dddddddd-0000-0000-0000-000000000010',
            'CustomerSupportPhone',
            '+2348000000000',
            'Support phone number shown to customers in track/order screens.',
            GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SystemConfigs WHERE [Key] = 'CustomerSupportEmail')
    INSERT INTO SystemConfigs (Id, [Key], Value, Description, UpdatedAt)
    VALUES ('dddddddd-0000-0000-0000-000000000011',
            'CustomerSupportEmail',
            'support@vyron.com',
            'Support email address shown to customers.',
            GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SystemConfigs WHERE [Key] = 'CustomerSupportWhatsApp')
    INSERT INTO SystemConfigs (Id, [Key], Value, Description, UpdatedAt)
    VALUES ('dddddddd-0000-0000-0000-000000000012',
            'CustomerSupportWhatsApp',
            '+2348000000000',
            'WhatsApp support number (include country code, no spaces).',
            GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SystemConfigs WHERE [Key] = 'CustomerSupportChatUrl')
    INSERT INTO SystemConfigs (Id, [Key], Value, Description, UpdatedAt)
    VALUES ('dddddddd-0000-0000-0000-000000000013',
            'CustomerSupportChatUrl',
            '',
            'Optional live-chat URL (e.g. Intercom widget). Leave empty to hide chat button.',
            GETUTCDATE());
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DELETE FROM SystemConfigs WHERE [Key] IN (
    'CustomerSupportPhone', 'CustomerSupportEmail',
    'CustomerSupportWhatsApp', 'CustomerSupportChatUrl'
);
");
    }
}

}
