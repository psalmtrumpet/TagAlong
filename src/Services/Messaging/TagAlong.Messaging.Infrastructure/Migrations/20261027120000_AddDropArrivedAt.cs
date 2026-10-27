using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddDropArrivedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "drop_arrived_at", table: "conversations", type: "datetime2", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "drop_arrived_at", table: "conversations");
        }
    }
}
