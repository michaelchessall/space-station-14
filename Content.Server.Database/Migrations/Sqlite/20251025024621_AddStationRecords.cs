using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                    station_records_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    station_id = table.Column<int>(type: "INTEGER", nullable: false),
                    record_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    record_data = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
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
