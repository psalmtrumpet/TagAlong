using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddRideHistoryDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "ride_distance_m", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "distance_mark_lat", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "distance_mark_lng", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "hidden_by_sender", table: "conversations", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "hidden_by_traveler", table: "conversations", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ride_distance_m", table: "conversations");
            migrationBuilder.DropColumn(name: "distance_mark_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "distance_mark_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "hidden_by_sender", table: "conversations");
            migrationBuilder.DropColumn(name: "hidden_by_traveler", table: "conversations");
        }
    }
}
