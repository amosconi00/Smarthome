using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class NilmDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nilm_detections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplianceId = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatedPowerW = table.Column<double>(type: "double precision", nullable: true),
                    IsOn = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nilm_detections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nilm_detections_appliances_ApplianceId",
                        column: x => x.ApplianceId,
                        principalTable: "appliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nilm_detections_ApplianceId",
                table: "nilm_detections",
                column: "ApplianceId");

            migrationBuilder.CreateIndex(
                name: "IX_nilm_detections_Timestamp_ApplianceId",
                table: "nilm_detections",
                columns: new[] { "Timestamp", "ApplianceId" });

            migrationBuilder.CreateIndex(
                name: "IX_nilm_detections_Timestamp_Source_ModelName",
                table: "nilm_detections",
                columns: new[] { "Timestamp", "Source", "ModelName" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nilm_detections");
        }
    }
}
