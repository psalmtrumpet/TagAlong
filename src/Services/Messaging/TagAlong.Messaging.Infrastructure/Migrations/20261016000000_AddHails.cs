using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddHails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "is_on_demand", table: "conversations", type: "bit", nullable: false, defaultValue: false);

            migrationBuilder.CreateTable(
                name: "hail_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    passenger_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    pickup_lat = table.Column<double>(type: "float", nullable: false),
                    pickup_lng = table.Column<double>(type: "float", nullable: false),
                    pickup_name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    dest_lat = table.Column<double>(type: "float", nullable: false),
                    dest_lng = table.Column<double>(type: "float", nullable: false),
                    dest_name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    offered_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    matched_driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    conversation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_hail_requests", x => x.id));
            migrationBuilder.CreateIndex(name: "IX_hail_requests_status_expires_at", table: "hail_requests", columns: new[] { "status", "expires_at" });
            migrationBuilder.CreateIndex(name: "IX_hail_requests_passenger_id", table: "hail_requests", column: "passenger_id");

            migrationBuilder.CreateTable(
                name: "hail_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    hail_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    driver_rating = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    driver_total_ratings = table.Column<int>(type: "int", nullable: false),
                    vehicle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    vehicle_plate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    eta_seconds = table.Column<int>(type: "int", nullable: false),
                    finishing_current_ride = table.Column<bool>(type: "bit", nullable: false),
                    counter_price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_hail_candidates", x => x.id));
            migrationBuilder.CreateIndex(name: "IX_hail_candidates_hail_id", table: "hail_candidates", column: "hail_id");
            migrationBuilder.CreateIndex(name: "IX_hail_candidates_driver_id", table: "hail_candidates", column: "driver_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "hail_candidates");
            migrationBuilder.DropTable(name: "hail_requests");
            migrationBuilder.DropColumn(name: "is_on_demand", table: "conversations");
        }
    }
}
