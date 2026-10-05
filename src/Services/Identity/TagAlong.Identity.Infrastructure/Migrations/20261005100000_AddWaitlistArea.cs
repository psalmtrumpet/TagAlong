using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Identity.Infrastructure.Migrations
{
    [Migration("20261005100000_AddWaitlistArea")]
    public partial class AddWaitlistArea : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "waitlist_entries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Area", table: "waitlist_entries");
        }
    }
}
