using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddReferrals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "referral_accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                FreeTripCredits = table.Column<int>(type: "int", nullable: false),
                WalletBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_referral_accounts", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_referral_accounts_AuthUserId", table: "referral_accounts", column: "AuthUserId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_referral_accounts_Code", table: "referral_accounts", column: "Code", unique: true);

        migrationBuilder.CreateTable(
            name: "referrals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferrerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferredUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                QualifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Reward = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RewardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_referrals", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_referrals_ReferredUserId", table: "referrals", column: "ReferredUserId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_referrals_ReferrerUserId", table: "referrals", column: "ReferrerUserId");

        migrationBuilder.CreateTable(
            name: "wallet_transactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_wallet_transactions", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_wallet_transactions_AuthUserId", table: "wallet_transactions", column: "AuthUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "wallet_transactions");
        migrationBuilder.DropTable(name: "referrals");
        migrationBuilder.DropTable(name: "referral_accounts");
    }
}
