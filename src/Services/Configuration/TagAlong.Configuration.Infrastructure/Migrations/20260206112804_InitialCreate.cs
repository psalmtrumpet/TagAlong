using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Configuration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fee_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    min_percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    max_percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    default_percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_configurations", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "fee_configurations",
                columns: new[] { "id", "created_at", "default_percentage", "deleted_at", "description", "is_active", "is_deleted", "max_percentage", "min_percentage", "name", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10.0m, null, "Platform fee percentage range for delivery services", true, false, 20.0m, 5.0m, "Platform Fee", null });

            migrationBuilder.CreateIndex(
                name: "IX_fee_configurations_is_active",
                table: "fee_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_fee_configurations_name",
                table: "fee_configurations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_configurations_is_active",
                table: "platform_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_platform_configurations_key",
                table: "platform_configurations",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_configurations_type",
                table: "platform_configurations",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_configurations");

            migrationBuilder.DropTable(
                name: "platform_configurations");
        }
    }
}
