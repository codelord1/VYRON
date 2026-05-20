using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vyron.API.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationApprovalStatus",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationApprovedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationRejectedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationRejectionReason",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ClosingTime",
                table: "Stores",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyClosed",
                table: "Stores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastClosedAt",
                table: "Stores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOpenedAt",
                table: "Stores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningDays",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OpeningTime",
                table: "Stores",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceTypeEntityId",
                table: "ServiceOfferings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Riders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Riders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Riders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Riders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Riders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryRiderId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupDelayNotifiedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachNotifiedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActivityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SentByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationLogs_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CommunicationLogs_Users_SentByAdminId",
                        column: x => x.SentByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreUserAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreUserAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreUserAssignments_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreUserAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StoreUserAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Riders",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                columns: new[] { "ApprovalStatus", "CreatedByUserId", "RejectedAt", "RejectedByUserId", "RejectionReason" },
                values: new object[] { 0, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000005"),
                column: "Description",
                value: "Super administrator — highest privilege");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("eeeeeeee-0000-0000-0000-000000000006"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Admin-level user created by SuperAdmin; cannot manage other admins", true, "AdminUser", "ADMINUSER" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000007"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Store manager scoped to assigned stores", true, "StoreManager", "STOREMANAGER" },
                    { new Guid("eeeeeeee-0000-0000-0000-000000000008"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Store staff scoped to assigned stores", true, "StoreStaff", "STORESTAFF" }
                });

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000001"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000002"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000003"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000004"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000005"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000006"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000007"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ServiceOfferings",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-0000-0000-0000-000000000008"),
                column: "ServiceTypeEntityId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Stores",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-0000-0000-0000-000000000001"),
                columns: new[] { "ClosingTime", "IsManuallyClosed", "LastClosedAt", "LastOpenedAt", "OpeningDays", "OpeningTime" },
                values: new object[] { null, false, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Stores",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
                columns: new[] { "ClosingTime", "IsManuallyClosed", "LastClosedAt", "LastOpenedAt", "OpeningDays", "OpeningTime" },
                values: new object[] { null, false, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Stores",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-0000-0000-0000-000000000003"),
                columns: new[] { "ClosingTime", "IsManuallyClosed", "LastClosedAt", "LastOpenedAt", "OpeningDays", "OpeningTime" },
                values: new object[] { null, false, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "ApprovedByUserId", "RegistrationApprovalStatus", "RegistrationApprovedAt", "RegistrationRejectedAt", "RegistrationRejectionReason", "RejectedByUserId" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "ApprovedByUserId", "RegistrationApprovalStatus", "RegistrationApprovedAt", "RegistrationRejectedAt", "RegistrationRejectionReason", "RejectedByUserId" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "ApprovedByUserId", "RegistrationApprovalStatus", "RegistrationApprovedAt", "RegistrationRejectedAt", "RegistrationRejectionReason", "RejectedByUserId" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "ApprovedByUserId", "RegistrationApprovalStatus", "RegistrationApprovedAt", "RegistrationRejectedAt", "RegistrationRejectionReason", "RejectedByUserId" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                columns: new[] { "ApprovedByUserId", "RegistrationApprovalStatus", "RegistrationApprovedAt", "RegistrationRejectedAt", "RegistrationRejectionReason", "RejectedByUserId" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOfferings_ServiceTypeEntityId",
                table: "ServiceOfferings",
                column: "ServiceTypeEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Riders_ApprovalStatus",
                table: "Riders",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryRiderId",
                table: "Orders",
                column: "DeliveryRiderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId_CreatedAt",
                table: "Orders",
                columns: new[] { "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CreatedAt",
                table: "ActivityLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EntityType_EntityId",
                table: "ActivityLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                table: "ActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_Channel",
                table: "CommunicationLogs",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_CreatedAt",
                table: "CommunicationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_RecipientUserId",
                table: "CommunicationLogs",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_SentByAdminId",
                table: "CommunicationLogs",
                column: "SentByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLogs_Status",
                table: "CommunicationLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StoreUserAssignments_AssignedByUserId",
                table: "StoreUserAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreUserAssignments_StoreId",
                table: "StoreUserAssignments",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreUserAssignments_UserId",
                table: "StoreUserAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreUserAssignments_UserId_StoreId_IsActive",
                table: "StoreUserAssignments",
                columns: new[] { "UserId", "StoreId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Riders_DeliveryRiderId",
                table: "Orders",
                column: "DeliveryRiderId",
                principalTable: "Riders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId",
                table: "ServiceOfferings",
                column: "ServiceTypeEntityId",
                principalTable: "ServiceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Riders_DeliveryRiderId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOfferings_ServiceTypes_ServiceTypeEntityId",
                table: "ServiceOfferings");

            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "CommunicationLogs");

            migrationBuilder.DropTable(
                name: "ServiceTypes");

            migrationBuilder.DropTable(
                name: "StoreUserAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOfferings_ServiceTypeEntityId",
                table: "ServiceOfferings");

            migrationBuilder.DropIndex(
                name: "IX_Riders_ApprovalStatus",
                table: "Riders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryRiderId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StoreId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000008"));

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationApprovalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationApprovedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationRejectedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationRejectionReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClosingTime",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "IsManuallyClosed",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "LastClosedAt",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "LastOpenedAt",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "OpeningDays",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ServiceTypeEntityId",
                table: "ServiceOfferings");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "DeliveryRiderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupDelayNotifiedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SlaBreachNotifiedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Notifications");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("eeeeeeee-0000-0000-0000-000000000005"),
                column: "Description",
                value: "Super administrator");
        }
    }
}
