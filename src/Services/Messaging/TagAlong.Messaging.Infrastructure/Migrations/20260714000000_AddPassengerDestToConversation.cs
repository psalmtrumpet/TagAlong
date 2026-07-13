using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerDestToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS so this migration is safe to run
            // even if columns were previously added manually to the database.

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'agreed_price'
                )
                ALTER TABLE conversations ADD agreed_price DECIMAL(18,2) NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'lock_in_proposed_by'
                )
                ALTER TABLE conversations ADD lock_in_proposed_by UNIQUEIDENTIFIER NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'started_at'
                )
                ALTER TABLE conversations ADD started_at DATETIME2 NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'delivered_at'
                )
                ALTER TABLE conversations ADD delivered_at DATETIME2 NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'passenger_dest_lat'
                )
                ALTER TABLE conversations ADD passenger_dest_lat FLOAT NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'passenger_dest_lng'
                )
                ALTER TABLE conversations ADD passenger_dest_lng FLOAT NULL;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'conversations' AND COLUMN_NAME = 'passenger_dest_address'
                )
                ALTER TABLE conversations ADD passenger_dest_address NVARCHAR(500) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "agreed_price", table: "conversations");
            migrationBuilder.DropColumn(name: "lock_in_proposed_by", table: "conversations");
            migrationBuilder.DropColumn(name: "started_at", table: "conversations");
            migrationBuilder.DropColumn(name: "delivered_at", table: "conversations");
            migrationBuilder.DropColumn(name: "passenger_dest_lat", table: "conversations");
            migrationBuilder.DropColumn(name: "passenger_dest_lng", table: "conversations");
            migrationBuilder.DropColumn(name: "passenger_dest_address", table: "conversations");
        }
    }
}
