using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    public partial class AddTripIsPrivate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsPrivate", table: "trips", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsPrivate", table: "trips");
        }
    }
}
