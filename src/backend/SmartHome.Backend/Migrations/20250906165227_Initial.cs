using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appliance_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    OffThresholdWatts = table.Column<double>(type: "double precision", nullable: false),
                    OnThresholdWatts = table.Column<double>(type: "double precision", nullable: false),
                    SecondsMinDuration = table.Column<double>(type: "double precision", nullable: false),
                    HasStandby = table.Column<bool>(type: "boolean", nullable: false),
                    MinStandbyWatts = table.Column<double>(type: "double precision", nullable: false),
                    MaxStandbyWatts = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appliance_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "powermeters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_powermeters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appliances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OffThresholdWatts = table.Column<double>(type: "double precision", nullable: false),
                    OnThresholdWatts = table.Column<double>(type: "double precision", nullable: false),
                    SecondsMinDuration = table.Column<double>(type: "double precision", nullable: false),
                    HasStandby = table.Column<bool>(type: "boolean", nullable: false),
                    MinStandbyWatts = table.Column<double>(type: "double precision", nullable: false),
                    MaxStandbyWatts = table.Column<double>(type: "double precision", nullable: false),
                    EnableNilm = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryApplianceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appliances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appliances_appliance_categories_CategoryApplianceId",
                        column: x => x.CategoryApplianceId,
                        principalTable: "appliance_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "category_appliance_signatures",
                columns: table => new
                {
                    CategoryApplianceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OnDeltaMinW = table.Column<double>(type: "double precision", nullable: false),
                    OnDeltaMaxW = table.Column<double>(type: "double precision", nullable: false),
                    OffDeltaMinW = table.Column<double>(type: "double precision", nullable: false),
                    OffDeltaMaxW = table.Column<double>(type: "double precision", nullable: false),
                    MinOnSeconds = table.Column<double>(type: "double precision", nullable: false),
                    RefractorySeconds = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_appliance_signatures", x => x.CategoryApplianceId);
                    table.ForeignKey(
                        name: "FK_category_appliance_signatures_appliance_categories_Category~",
                        column: x => x.CategoryApplianceId,
                        principalTable: "appliance_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "powermeter_telemetries",
                columns: table => new
                {
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PowerMeterId = table.Column<int>(type: "integer", nullable: false),
                    VoltageV = table.Column<double>(type: "double precision", nullable: true),
                    CurrentA = table.Column<double>(type: "double precision", nullable: true),
                    PowerW = table.Column<double>(type: "double precision", nullable: true),
                    EnergyWh = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_powermeter_telemetries", x => new { x.Timestamp, x.PowerMeterId });
                    table.ForeignKey(
                        name: "FK_powermeter_telemetries_powermeters_PowerMeterId",
                        column: x => x.PowerMeterId,
                        principalTable: "powermeters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appliance_signatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplianceId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OnDeltaMinW = table.Column<double>(type: "double precision", nullable: false),
                    OnDeltaMaxW = table.Column<double>(type: "double precision", nullable: false),
                    OffDeltaMinW = table.Column<double>(type: "double precision", nullable: false),
                    OffDeltaMaxW = table.Column<double>(type: "double precision", nullable: false),
                    MinOnSeconds = table.Column<double>(type: "double precision", nullable: false),
                    RefractorySeconds = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appliance_signatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appliance_signatures_appliances_ApplianceId",
                        column: x => x.ApplianceId,
                        principalTable: "appliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appliance_telemetries",
                columns: table => new
                {
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplianceId = table.Column<int>(type: "integer", nullable: false),
                    PowerWatts = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appliance_telemetries", x => new { x.Timestamp, x.ApplianceId });
                    table.ForeignKey(
                        name: "FK_appliance_telemetries_appliances_ApplianceId",
                        column: x => x.ApplianceId,
                        principalTable: "appliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApplianceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plugs_appliances_ApplianceId",
                        column: x => x.ApplianceId,
                        principalTable: "appliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appliance_signatures_ApplianceId",
                table: "appliance_signatures",
                column: "ApplianceId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_appliance_signatures_ApplianceId_Version",
                table: "appliance_signatures",
                columns: new[] { "ApplianceId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_appliance_telemetries_ApplianceId",
                table: "appliance_telemetries",
                column: "ApplianceId");

            migrationBuilder.CreateIndex(
                name: "IX_appliances_CategoryApplianceId",
                table: "appliances",
                column: "CategoryApplianceId");

            migrationBuilder.CreateIndex(
                name: "IX_plugs_ApplianceId",
                table: "plugs",
                column: "ApplianceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_powermeter_telemetries_PowerMeterId",
                table: "powermeter_telemetries",
                column: "PowerMeterId");

            migrationBuilder.CreateIndex(
                name: "IX_powermeters_Code",
                table: "powermeters",
                column: "Code",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appliance_signatures");

            migrationBuilder.DropTable(
                name: "appliance_telemetries");

            migrationBuilder.DropTable(
                name: "category_appliance_signatures");

            migrationBuilder.DropTable(
                name: "plugs");

            migrationBuilder.DropTable(
                name: "powermeter_telemetries");

            migrationBuilder.DropTable(
                name: "appliances");

            migrationBuilder.DropTable(
                name: "powermeters");

            migrationBuilder.DropTable(
                name: "appliance_categories");
        }
    }
}
