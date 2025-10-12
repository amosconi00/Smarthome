using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFieldsApplianceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasStandby",
                table: "appliance_categories");

            migrationBuilder.DropColumn(
                name: "MaxStandbyWatts",
                table: "appliance_categories");

            migrationBuilder.DropColumn(
                name: "MinStandbyWatts",
                table: "appliance_categories");

            migrationBuilder.DropColumn(
                name: "OffThresholdWatts",
                table: "appliance_categories");

            migrationBuilder.DropColumn(
                name: "OnThresholdWatts",
                table: "appliance_categories");

            migrationBuilder.DropColumn(
                name: "SecondsMinDuration",
                table: "appliance_categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasStandby",
                table: "appliance_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MaxStandbyWatts",
                table: "appliance_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MinStandbyWatts",
                table: "appliance_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OffThresholdWatts",
                table: "appliance_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OnThresholdWatts",
                table: "appliance_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SecondsMinDuration",
                table: "appliance_categories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
