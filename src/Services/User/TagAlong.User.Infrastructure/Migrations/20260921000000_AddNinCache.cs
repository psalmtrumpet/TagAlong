using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddNinCache : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "nin_cache",
            columns: table => new
            {
                NIN = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DateOfBirth = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                CachedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_nin_cache", x => x.NIN);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "nin_cache");
    }
}
