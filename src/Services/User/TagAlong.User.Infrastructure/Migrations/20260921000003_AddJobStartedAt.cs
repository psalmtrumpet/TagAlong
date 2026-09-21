using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddJobStartedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "JobStartedAt",
            table: "kyc_verifications",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "JobStartedAt",
            table: "kyc_verifications");
    }
}
