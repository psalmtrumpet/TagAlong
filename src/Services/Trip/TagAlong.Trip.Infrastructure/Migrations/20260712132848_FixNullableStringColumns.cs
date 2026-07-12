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
            // Can't NULL long values (column is NOT NULL) and can't ALTER (too-long data).
            // Step 1: truncate oversized values to 20 chars so the column constraint is satisfied.
            migrationBuilder.Sql(
                "UPDATE trips SET VehiclePlateNumber = LEFT(VehiclePlateNumber, 20) WHERE LEN(VehiclePlateNumber) > 20");

            // Step 2: make nullable (column data now all <= 20 chars, constraint can be removed).
            migrationBuilder.Sql(
                "ALTER TABLE trips ALTER COLUMN VehiclePlateNumber nvarchar(20) NULL");

            // Step 3: expand to 100 chars — users enter vehicle descriptions, not just plate numbers.
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
