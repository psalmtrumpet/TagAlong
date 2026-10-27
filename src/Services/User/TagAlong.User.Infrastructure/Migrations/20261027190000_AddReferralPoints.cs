using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddReferralPoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "Points", table: "referral_accounts", type: "int", nullable: false, defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Points", table: "referral_accounts");
    }
}
