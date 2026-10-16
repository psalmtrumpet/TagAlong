using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddHailDriverPosition : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(name: "driver_lat", table: "hail_candidates", type: "float", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<double>(name: "driver_lng", table: "hail_candidates", type: "float", nullable: false, defaultValue: 0.0);
            migrationBuilder.AddColumn<int>(name: "distance_meters", table: "hail_candidates", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "driver_lat", table: "hail_candidates");
            migrationBuilder.DropColumn(name: "driver_lng", table: "hail_candidates");
            migrationBuilder.DropColumn(name: "distance_meters", table: "hail_candidates");
        }
    }
}
