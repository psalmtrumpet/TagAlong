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
            // Clear any values that exceed the column's nvarchar(20) max length before altering
            // nullability — SQL Server re-validates all data during ALTER COLUMN and will throw
            // error 2628 if existing rows violate the length constraint.
            migrationBuilder.Sql(
                "UPDATE trips SET VehiclePlateNumber = NULL WHERE LEN(VehiclePlateNumber) > 20");

            migrationBuilder.Sql(
                "ALTER TABLE trips ALTER COLUMN VehiclePlateNumber nvarchar(20) NULL");

            // Expand to 100 chars — users enter vehicle descriptions, not just plate numbers.
            migrationBuilder.Sql(
                "ALTER TABLE trips ALTER COLUMN VehiclePlateNumber nvarchar(100) NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE trips SET VehiclePlateNumber = '' WHERE VehiclePlateNumber IS NULL");
            migrationBuilder.Sql(
                "ALTER TABLE trips ALTER COLUMN VehiclePlateNumber nvarchar(100) NOT NULL");
            migrationBuilder.Sql(
                "ALTER TABLE trips ALTER COLUMN VehiclePlateNumber nvarchar(20) NOT NULL");
        }
    }
}
