using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddReferralEarnings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "referral_earnings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferrerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferredUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reward = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_referral_earnings", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_referral_earnings_ConversationId_ReferredUserId", table: "referral_earnings",
            columns: new[] { "ConversationId", "ReferredUserId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_referral_earnings_ReferrerUserId", table: "referral_earnings", column: "ReferrerUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "referral_earnings");
    }
}
