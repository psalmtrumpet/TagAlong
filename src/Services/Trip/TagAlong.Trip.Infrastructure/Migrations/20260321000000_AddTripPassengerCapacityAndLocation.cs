using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.Trip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripPassengerCapacityAndLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassengerCapacity",
                table: "trips",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "CurrentPassengerCount",
                table: "trips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLatitude",
                table: "trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLongitude",
                table: "trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationUpdatedAt",
                table: "trips",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PassengerCapacity", table: "trips");
            migrationBuilder.DropColumn(name: "CurrentPassengerCount", table: "trips");
            migrationBuilder.DropColumn(name: "CurrentLatitude", table: "trips");
            migrationBuilder.DropColumn(name: "CurrentLongitude", table: "trips");
            migrationBuilder.DropColumn(name: "LocationUpdatedAt", table: "trips");
        }
    }
}
