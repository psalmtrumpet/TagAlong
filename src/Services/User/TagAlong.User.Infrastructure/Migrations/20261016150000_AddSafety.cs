using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddSafety : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "emergency_contacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_emergency_contacts", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_emergency_contacts_AuthUserId", table: "emergency_contacts", column: "AuthUserId");

        migrationBuilder.CreateTable(
            name: "safety_recordings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FilePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                DurationSeconds = table.Column<int>(type: "int", nullable: false),
                Flagged = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_safety_recordings", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_safety_recordings_AuthUserId", table: "safety_recordings", column: "AuthUserId");
        migrationBuilder.CreateIndex(name: "IX_safety_recordings_ConversationId", table: "safety_recordings", column: "ConversationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "safety_recordings");
        migrationBuilder.DropTable(name: "emergency_contacts");
    }
}
