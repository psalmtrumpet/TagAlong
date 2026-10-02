using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Identity.Infrastructure.Migrations
{
    [Migration("20261002000000_AddUserGender")]
    public partial class AddUserGender : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Gender", table: "users");
        }
    }
}
