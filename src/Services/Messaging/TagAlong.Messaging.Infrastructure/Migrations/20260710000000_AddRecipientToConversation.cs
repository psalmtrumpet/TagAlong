using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recipient_user_id",
                table: "conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient_name",
                table: "conversations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recipient_user_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "recipient_name",
                table: "conversations");
        }
    }
}
