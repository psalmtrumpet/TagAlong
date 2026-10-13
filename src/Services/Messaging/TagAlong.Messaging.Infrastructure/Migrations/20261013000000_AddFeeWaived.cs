using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddFeeWaived : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "fee_waived", table: "conversations", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "fee_waived", table: "conversations");
        }
    }
}
