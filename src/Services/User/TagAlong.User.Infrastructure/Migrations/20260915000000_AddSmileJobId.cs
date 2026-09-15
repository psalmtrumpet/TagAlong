using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddSmileJobId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SmileJobId",
            table: "kyc_verifications",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_kyc_verifications_SmileJobId",
            table: "kyc_verifications",
            column: "SmileJobId",
            unique: true,
            filter: "[SmileJobId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_kyc_verifications_SmileJobId",
            table: "kyc_verifications");

        migrationBuilder.DropColumn(
            name: "SmileJobId",
            table: "kyc_verifications");
    }
}
