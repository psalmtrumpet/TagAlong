using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Package.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TravelerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreedPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PlatformFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TravelerPayout = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MeetupLocation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MeetupLatitude = table.Column<double>(type: "float", nullable: true),
                    MeetupLongitude = table.Column<double>(type: "float", nullable: true),
                    MeetupTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedUpAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryProofImageUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiverPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "package_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PickupLatitude = table.Column<double>(type: "float", nullable: false),
                    PickupLongitude = table.Column<double>(type: "float", nullable: false),
                    DeliveryLocation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DeliveryLatitude = table.Column<double>(type: "float", nullable: false),
                    DeliveryLongitude = table.Column<double>(type: "float", nullable: false),
                    PackageDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedWeight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OfferedPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SpecialInstructions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiredByDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PackageImageUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_PackageRequestId",
                table: "deliveries",
                column: "PackageRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_SenderId",
                table: "deliveries",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_Status",
                table: "deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_TravelerId",
                table: "deliveries",
                column: "TravelerId");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_TripId",
                table: "deliveries",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_package_requests_DeliveryLatitude_DeliveryLongitude",
                table: "package_requests",
                columns: new[] { "DeliveryLatitude", "DeliveryLongitude" });

            migrationBuilder.CreateIndex(
                name: "IX_package_requests_PickupLatitude_PickupLongitude",
                table: "package_requests",
                columns: new[] { "PickupLatitude", "PickupLongitude" });

            migrationBuilder.CreateIndex(
                name: "IX_package_requests_SenderId",
                table: "package_requests",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_package_requests_Status",
                table: "package_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "package_requests");
        }
    }
}
