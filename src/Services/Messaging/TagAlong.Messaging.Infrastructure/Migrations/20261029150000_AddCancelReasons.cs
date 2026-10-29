using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddCancelReasons : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "cancel_reason", table: "conversations", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "cancelled_by", table: "conversations", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<string>(name: "cancel_reason", table: "hail_requests", type: "nvarchar(300)", maxLength: 300, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "cancel_reason", table: "conversations");
            migrationBuilder.DropColumn(name: "cancelled_by", table: "conversations");
            migrationBuilder.DropColumn(name: "cancel_reason", table: "hail_requests");
        }
    }
}
