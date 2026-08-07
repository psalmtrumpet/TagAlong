using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Identity.Infrastructure.Migrations
{
    public partial class AddAdminRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "User");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Role", table: "users");
        }
    }
}
