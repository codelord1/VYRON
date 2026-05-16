using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vyron.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreImagesProfileServicesRiderApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocalPhone",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageContentType",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageFileName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProfileImageSize",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileImageUpdatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ServiceOfferings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ServiceOfferings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ServiceOfferings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Riders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByAdminId",
                table: "Riders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Riders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreImages_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                columns: new[] { "ApprovedAt", "ApprovedByAdminId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000005"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000006"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000007"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000008"),
                columns: new[] { "Category", "CreatedAt", "UpdatedAt" },
                values: new object[] { null, null, null });

            migrationBuilder.InsertData(
                table: "SystemConfigs",
                columns: new[] { "Id", "Description", "Key", "UpdatedAt", "UpdatedByUserId", "Value" },
                values: new object[] { new Guid("dddddddd-0000-0000-0000-000000000009"), "Portal idle session timeout (5–120 min). Client-side enforced; server cookie uses its own expiry.", "PortalIdleTimeoutMinutes", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "15" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CountryCode", "LocalPhone", "PasswordChangedAt", "ProfileImageContentType", "ProfileImageFileName", "ProfileImageSize", "ProfileImageUpdatedAt" },
                values: new object[] { "+234", null, null, null, null, 0L, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "CountryCode", "LocalPhone", "PasswordChangedAt", "ProfileImageContentType", "ProfileImageFileName", "ProfileImageSize", "ProfileImageUpdatedAt" },
                values: new object[] { "+234", null, null, null, null, 0L, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "CountryCode", "LocalPhone", "PasswordChangedAt", "ProfileImageContentType", "ProfileImageFileName", "ProfileImageSize", "ProfileImageUpdatedAt" },
                values: new object[] { "+234", null, null, null, null, 0L, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "CountryCode", "LocalPhone", "PasswordChangedAt", "ProfileImageContentType", "ProfileImageFileName", "ProfileImageSize", "ProfileImageUpdatedAt" },
                values: new object[] { "+234", null, null, null, null, 0L, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "CountryCode", "LocalPhone", "PasswordChangedAt", "ProfileImageContentType", "ProfileImageFileName", "ProfileImageSize", "ProfileImageUpdatedAt" },
                values: new object[] { "+234", null, null, null, null, 0L, null });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreImages_StoreId",
                table: "StoreImages",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "StoreImages");

            migrationBuilder.DeleteData(
                table: "SystemConfigs",
                keyColumn: "Id",
                keyValue: new Guid("dddddddd-0000-0000-0000-000000000009"));

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocalPhone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageContentType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageFileName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageSize",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageUpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ServiceOfferings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ServiceOfferings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ServiceOfferings");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "ApprovedByAdminId",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Riders");
        }
    }
}
