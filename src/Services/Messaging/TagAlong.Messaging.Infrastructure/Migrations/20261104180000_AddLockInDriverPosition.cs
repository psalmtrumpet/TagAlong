using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddLockInDriverPosition : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "lock_in_driver_lat", table: "conversations", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "lock_in_driver_lng", table: "conversations", type: "float", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "lock_in_driver_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "lock_in_driver_lng", table: "conversations");
        }
    }
}
