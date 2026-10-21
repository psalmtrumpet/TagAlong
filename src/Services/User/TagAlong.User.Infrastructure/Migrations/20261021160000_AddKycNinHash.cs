using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddKycNinHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NinHash",
            table: "kyc_verifications",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
        migrationBuilder.CreateIndex(name: "IX_kyc_verifications_NinHash", table: "kyc_verifications", column: "NinHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_kyc_verifications_NinHash", table: "kyc_verifications");
        migrationBuilder.DropColumn(name: "NinHash", table: "kyc_verifications");
    }
}
