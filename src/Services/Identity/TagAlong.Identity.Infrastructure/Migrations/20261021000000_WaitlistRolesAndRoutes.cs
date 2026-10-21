using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Identity.Infrastructure.Migrations
{
    // Applied by hand on production (see waitlist SQL) — no [Migration] attribute.
    public partial class WaitlistRolesAndRoutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_waitlist_entries_Email", table: "waitlist_entries");
            migrationBuilder.AlterColumn<string>(name: "Email", table: "waitlist_entries", type: "nvarchar(254)", maxLength: 254, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Role", table: "waitlist_entries", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "FromArea", table: "waitlist_entries", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ToArea", table: "waitlist_entries", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.CreateIndex(name: "IX_waitlist_entries_Email", table: "waitlist_entries", column: "Email", unique: true, filter: "[Email] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_waitlist_entries_Phone", table: "waitlist_entries", column: "Phone");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_waitlist_entries_Phone", table: "waitlist_entries");
            migrationBuilder.DropColumn(name: "Role", table: "waitlist_entries");
            migrationBuilder.DropColumn(name: "FromArea", table: "waitlist_entries");
            migrationBuilder.DropColumn(name: "ToArea", table: "waitlist_entries");
        }
    }
}
