using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddDriverFeePayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "fee_settled_at", table: "conversations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "fee_payment_id", table: "conversations", type: "uniqueidentifier", nullable: true);
            // Rides from before fees were collected (testing) are not owed
            migrationBuilder.Sql("UPDATE conversations SET fee_settled_at = SYSUTCDATETIME() WHERE delivered_at IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "driver_fee_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    rides = table.Column<int>(type: "int", nullable: false),
                    ride_ids = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    authorization_url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_driver_fee_payments", x => x.id));
            migrationBuilder.CreateIndex(name: "IX_driver_fee_payments_reference", table: "driver_fee_payments", column: "reference", unique: true);
            migrationBuilder.CreateIndex(name: "IX_driver_fee_payments_driver_id", table: "driver_fee_payments", column: "driver_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "driver_fee_payments");
            migrationBuilder.DropColumn(name: "fee_settled_at", table: "conversations");
            migrationBuilder.DropColumn(name: "fee_payment_id", table: "conversations");
        }
    }
}
