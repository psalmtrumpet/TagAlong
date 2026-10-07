using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddDriverProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "driver_profiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                LicenseExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                LicenseImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                VehicleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                VehicleMake = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                VehicleModel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                VehicleColor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                VehiclePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                VehicleImagePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DocumentCheckJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DocumentCheckFailures = table.Column<int>(type: "int", nullable: true),
                DocumentCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_driver_profiles", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_driver_profiles_AuthUserId",
            table: "driver_profiles",
            column: "AuthUserId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "driver_profiles");
    }
}
