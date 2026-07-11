using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripRouteLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TripType",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Passenger");

            migrationBuilder.AlterColumn<int>(
                name: "PassengerCapacity",
                table: "trips",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 3);

            migrationBuilder.AddColumn<LineString>(
                name: "RouteLine",
                table: "trips",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteStatus",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.Sql(@"
                CREATE SPATIAL INDEX SIX_trips_RouteLine
                ON trips(RouteLine)
                USING GEOGRAPHY_AUTO_GRID
                WITH (CELLS_PER_OBJECT = 16);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS SIX_trips_RouteLine ON trips;");

            migrationBuilder.DropColumn(
                name: "RouteLine",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "RouteStatus",
                table: "trips");

            migrationBuilder.AlterColumn<string>(
                name: "TripType",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Passenger",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "PassengerCapacity",
                table: "trips",
                type: "int",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
