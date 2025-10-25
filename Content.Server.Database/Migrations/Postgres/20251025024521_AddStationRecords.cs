using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddStationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "station_records",
                columns: table => new
                {
                    station_records_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    station_id = table.Column<int>(type: "integer", nullable: false),
                    record_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    record_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_station_records", x => x.station_records_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_station_records_created_at",
                table: "station_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_station_records_station_type",
                table: "station_records",
                columns: new[] { "station_id", "record_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "station_records");
        }
    }
}
