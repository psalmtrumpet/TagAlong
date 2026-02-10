using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Report.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reported_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reported_delivery_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    report_type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    admin_notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reports_report_type",
                table: "reports",
                column: "report_type");

            migrationBuilder.CreateIndex(
                name: "IX_reports_reported_delivery_id",
                table: "reports",
                column: "reported_delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_reported_user_id",
                table: "reports",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_reporter_id",
                table: "reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_status",
                table: "reports",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reports");
        }
    }
}
