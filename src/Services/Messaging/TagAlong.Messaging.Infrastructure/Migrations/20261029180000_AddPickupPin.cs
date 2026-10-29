using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddPickupPin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "pickup_pin", table: "conversations", type: "nvarchar(4)", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<int>(name: "pin_failures", table: "conversations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(name: "pin_locked_until", table: "conversations", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "pickup_pin", table: "conversations");
            migrationBuilder.DropColumn(name: "pin_failures", table: "conversations");
            migrationBuilder.DropColumn(name: "pin_locked_until", table: "conversations");
        }
    }
}
