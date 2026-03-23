using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TripType",
                table: "trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Passenger");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TripType",
                table: "trips");
        }
    }
}
