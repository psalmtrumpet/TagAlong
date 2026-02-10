using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Payment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    sender_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    traveler_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    platform_fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    traveler_payout = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    transaction_reference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    payment_provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_delivery_id",
                table: "payments",
                column: "delivery_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_sender_id",
                table: "payments",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_status",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_payments_transaction_reference",
                table: "payments",
                column: "transaction_reference");

            migrationBuilder.CreateIndex(
                name: "IX_payments_traveler_id",
                table: "payments",
                column: "traveler_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
