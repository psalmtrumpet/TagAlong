using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHelperLocationToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'helper_last_lat'
                )
                ALTER TABLE conversations ADD helper_last_lat FLOAT NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'helper_last_lng'
                )
                ALTER TABLE conversations ADD helper_last_lng FLOAT NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'helper_last_seen_at'
                )
                ALTER TABLE conversations ADD helper_last_seen_at DATETIME2 NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "helper_last_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "helper_last_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "helper_last_seen_at", table: "conversations");
        }
    }
}
