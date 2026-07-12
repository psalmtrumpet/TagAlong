using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixNullableStringColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // VehiclePlateNumber drifted to NOT NULL in the DB despite being string? in the model.
            migrationBuilder.AlterColumn<string>(
                name: "VehiclePlateNumber",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE trips SET VehiclePlateNumber = '' WHERE VehiclePlateNumber IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "VehiclePlateNumber",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
