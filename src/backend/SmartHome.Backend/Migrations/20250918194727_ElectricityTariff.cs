using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartHome.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ElectricityTariff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "electricity_tariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsTimeOfUse = table.Column<bool>(type: "boolean", nullable: false),
                    IsMultiFascia = table.Column<bool>(type: "boolean", nullable: false),
                    CostF1 = table.Column<double>(type: "double precision", nullable: false),
                    CostF2 = table.Column<double>(type: "double precision", nullable: true),
                    CostF3 = table.Column<double>(type: "double precision", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electricity_tariffs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_electricity_tariffs_IsDefault",
                table: "electricity_tariffs",
                column: "IsDefault",
                filter: "\"IsDefault\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "electricity_tariffs");
        }
    }
}
