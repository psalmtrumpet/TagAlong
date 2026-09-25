using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Identity.Infrastructure.Migrations
{
    [Migration("20260925000000_AddPasswordResetOtp")]
    public partial class AddPasswordResetOtp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetOtp",
                table: "users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetOtpExpiry",
                table: "users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.InsertData(
                table: "__EFMigrationsHistory",
                columns: new[] { "MigrationId", "ProductVersion" },
                values: new object[] { "20260925000000_AddPasswordResetOtp", "8.0.0" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PasswordResetOtp", table: "users");
            migrationBuilder.DropColumn(name: "PasswordResetOtpExpiry", table: "users");
        }
    }
}
