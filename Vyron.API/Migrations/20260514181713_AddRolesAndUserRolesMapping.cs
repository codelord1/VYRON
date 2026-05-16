using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vyron.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndUserRolesMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("eeeeeeee-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Customer user", true, "Customer", "CUSTOMER" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Delivery rider", true, "Rider", "RIDER" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Laundry store owner", true, "StoreOwner", "STOREOWNER" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Platform administrator", true, "Admin", "ADMIN" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Super administrator", true, "SuperAdmin", "SUPERADMIN" }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "CreatedAt", "CreatedByUserId", "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("ffffffff-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("eeeeeeee-0000-0000-0000-000000000004"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("ffffffff-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("eeeeeeee-0000-0000-0000-000000000002"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("ffffffff-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("eeeeeeee-0000-0000-0000-000000000003"), new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("ffffffff-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("eeeeeeee-0000-0000-0000-000000000003"), new Guid("44444444-4444-4444-4444-444444444444") },
                    { new Guid("ffffffff-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("eeeeeeee-0000-0000-0000-000000000003"), new Guid("55555555-5555-5555-5555-555555555555") }
                });

            // Map ALL existing users to UserRoles based on their Users.Role column value.
            // Skips any user already inserted above (seeded users) via NOT EXISTS guard.
            // Role column: 0=Customer, 1=Rider, 2=StoreOwner, 3=Admin, 4=SuperAdmin
            migrationBuilder.Sql(@"
INSERT INTO [UserRoles] ([Id], [UserId], [RoleId], [CreatedAt])
SELECT NEWID(), u.[Id], r.[Id], GETUTCDATE()
FROM   [Users]  u
JOIN   [Roles]  r ON r.[Name] = CASE u.[Role]
    WHEN 0 THEN 'Customer'
    WHEN 1 THEN 'Rider'
    WHEN 2 THEN 'StoreOwner'
    WHEN 3 THEN 'Admin'
    WHEN 4 THEN 'SuperAdmin'
    ELSE        'Customer'
END
WHERE NOT EXISTS (
    SELECT 1 FROM [UserRoles] ur
    WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
);");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NormalizedName",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
