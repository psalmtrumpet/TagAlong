using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddSmileWebhookLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "smile_webhook_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ResultCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsJobStatusResult = table.Column<bool>(type: "bit", nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BodySnippet = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_smile_webhook_logs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_smile_webhook_logs_JobId",
            table: "smile_webhook_logs",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_smile_webhook_logs_ReceivedAt",
            table: "smile_webhook_logs",
            column: "ReceivedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "smile_webhook_logs");
    }
}
