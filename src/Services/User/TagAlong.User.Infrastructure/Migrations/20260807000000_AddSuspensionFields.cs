using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations
{
    public partial class AddSuspensionFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "user_profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "user_profiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "user_profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsSuspended",      table: "user_profiles");
            migrationBuilder.DropColumn(name: "SuspendedAt",      table: "user_profiles");
            migrationBuilder.DropColumn(name: "SuspensionReason", table: "user_profiles");
        }
    }
}
