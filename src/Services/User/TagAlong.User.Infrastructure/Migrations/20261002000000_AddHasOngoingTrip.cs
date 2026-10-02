using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TagAlong.User.Infrastructure.Persistence;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

[DbContext(typeof(UserDbContext))]
[Migration("20261002000000_AddHasOngoingTrip")]
public partial class AddHasOngoingTrip : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasOngoingTrip",
            table: "user_profiles",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HasOngoingTrip",
            table: "user_profiles");
    }
}
