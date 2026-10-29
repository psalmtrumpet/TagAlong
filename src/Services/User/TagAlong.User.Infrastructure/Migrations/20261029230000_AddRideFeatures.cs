using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagAlong.User.Infrastructure.Migrations;

public partial class AddRideFeatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ride_features",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                Icon = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ride_features", x => x.Id));

        migrationBuilder.AddColumn<string>(
            name: "RideFeatures", table: "user_profiles", type: "nvarchar(200)", maxLength: 200, nullable: true);

        migrationBuilder.Sql(
            "INSERT INTO ride_features (Name, Icon, SortOrder, IsActive, CreatedAt) VALUES " +
            "(N'Air conditioning', N'ac', 1, 1, SYSUTCDATETIME()), " +
            "(N'Tinted windows', N'tinted', 2, 1, SYSUTCDATETIME()), " +
            "(N'Music', N'music', 3, 1, SYSUTCDATETIME())");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RideFeatures", table: "user_profiles");
        migrationBuilder.DropTable(name: "ride_features");
    }
}
