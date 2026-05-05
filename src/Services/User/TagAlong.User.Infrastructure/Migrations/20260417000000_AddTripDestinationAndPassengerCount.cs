using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDestinationAndPassengerCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TripDestinationLatitude",
                table: "user_profiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TripDestinationLongitude",
                table: "user_profiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TripDestinationName",
                table: "user_profiles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivePassengerCount",
                table: "user_profiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TripDestinationLatitude",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "TripDestinationLongitude",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "TripDestinationName",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ActivePassengerCount",
                table: "user_profiles");
        }
    }
}
