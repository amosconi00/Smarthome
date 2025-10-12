using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ApplianceEnable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enable",
                table: "appliances",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Enable",
                table: "appliances");
        }
    }
}
