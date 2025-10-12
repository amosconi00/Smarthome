using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class HyperTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_nilm_detections",
                table: "nilm_detections");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "nilm_detections",
                newName: "Number");

            migrationBuilder.AddPrimaryKey(
                name: "PK_nilm_detections",
                table: "nilm_detections",
                columns: new[] { "Timestamp", "Number" });

            // Timescale extension
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");

            // Convert existing non-empty tables to hypertables (idempotent, migrates existing data)
            migrationBuilder.Sql("SELECT create_hypertable('powermeter_telemetries','Timestamp', chunk_time_interval => INTERVAL '1 month', if_not_exists => TRUE, migrate_data => TRUE);");
            migrationBuilder.Sql("SELECT create_hypertable('appliance_telemetries','Timestamp', chunk_time_interval => INTERVAL '1 month', if_not_exists => TRUE, migrate_data => TRUE);");
            migrationBuilder.Sql("SELECT create_hypertable('nilm_detections','Timestamp', chunk_time_interval => INTERVAL '1 month', if_not_exists => TRUE, migrate_data => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_nilm_detections",
                table: "nilm_detections");

            migrationBuilder.RenameColumn(
                name: "Number",
                table: "nilm_detections",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_nilm_detections",
                table: "nilm_detections",
                column: "Id");
        }
    }
}
