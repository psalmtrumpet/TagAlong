using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQoreIdReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QoreIdReference",
                table: "kyc_verifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_QoreIdReference",
                table: "kyc_verifications",
                column: "QoreIdReference",
                unique: true,
                filter: "[QoreIdReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_QoreIdReference",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "QoreIdReference",
                table: "kyc_verifications");
        }
    }
}
