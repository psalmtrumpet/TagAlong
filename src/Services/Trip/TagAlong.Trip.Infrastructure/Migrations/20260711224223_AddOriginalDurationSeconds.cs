using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalDurationSeconds",
                table: "trips",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalDurationSeconds",
                table: "trips");
        }
    }
}
