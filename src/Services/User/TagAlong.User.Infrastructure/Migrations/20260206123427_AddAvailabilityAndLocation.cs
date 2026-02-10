using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilityAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLocationSharing",
                table: "user_profiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityExpiresAt",
                table: "user_profiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityStartedAt",
                table: "user_profiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLatitude",
                table: "user_profiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentLocationName",
                table: "user_profiles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLongitude",
                table: "user_profiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "user_profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationUpdatedAt",
                table: "user_profiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaxTravelRadiusKm",
                table: "user_profiles",
                type: "float",
                nullable: false,
                defaultValue: 10.0);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_IsAvailable_CurrentLatitude_CurrentLongitude",
                table: "user_profiles",
                columns: new[] { "IsAvailable", "CurrentLatitude", "CurrentLongitude" },
                filter: "[IsAvailable] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_profiles_IsAvailable_CurrentLatitude_CurrentLongitude",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "AllowLocationSharing",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "AvailabilityExpiresAt",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "AvailabilityStartedAt",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "CurrentLatitude",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "CurrentLocationName",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "CurrentLongitude",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "LocationUpdatedAt",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "MaxTravelRadiusKm",
                table: "user_profiles");
        }
    }
}
