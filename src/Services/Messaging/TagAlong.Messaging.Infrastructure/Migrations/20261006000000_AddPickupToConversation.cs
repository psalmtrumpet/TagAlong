using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddPickupToConversation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "pickup_lat", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "pickup_lng", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<string>(name: "pickup_address", table: "conversations", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "pickup_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "pickup_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "pickup_address", table: "conversations");
        }
    }
}
