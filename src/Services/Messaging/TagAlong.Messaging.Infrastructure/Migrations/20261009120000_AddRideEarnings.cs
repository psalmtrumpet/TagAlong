using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddRideEarnings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "platform_fee", table: "conversations", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "driver_earning", table: "conversations", type: "decimal(18,2)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "platform_fee", table: "conversations");
            migrationBuilder.DropColumn(name: "driver_earning", table: "conversations");
        }
    }
}
