using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddFareChangeAsked : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "fare_change_asked", table: "conversations", type: "decimal(18,2)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "fare_change_asked", table: "conversations");
        }
    }
}
