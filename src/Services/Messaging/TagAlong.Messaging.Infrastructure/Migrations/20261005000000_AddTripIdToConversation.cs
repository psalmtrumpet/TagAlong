using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    public partial class AddTripIdToConversation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "trip_id",
                table: "conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_trip_id",
                table: "conversations",
                column: "trip_id");

            migrationBuilder.AddColumn<bool>(
                name: "is_delivery",
                table: "conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_conversations_trip_id", table: "conversations");
            migrationBuilder.DropColumn(name: "is_delivery", table: "conversations");
            migrationBuilder.DropColumn(name: "trip_id", table: "conversations");
        }
    }
}
