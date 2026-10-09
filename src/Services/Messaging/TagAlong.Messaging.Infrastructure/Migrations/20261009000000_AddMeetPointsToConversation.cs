using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddMeetPointsToConversation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "meet_lat", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "meet_lng", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<string>(name: "meet_name", table: "conversations", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<double>(name: "drop_lat", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "drop_lng", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<string>(name: "drop_name", table: "conversations", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "meet_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "meet_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "meet_name", table: "conversations");
            migrationBuilder.DropColumn(name: "drop_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "drop_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "drop_name", table: "conversations");
        }
    }
}
