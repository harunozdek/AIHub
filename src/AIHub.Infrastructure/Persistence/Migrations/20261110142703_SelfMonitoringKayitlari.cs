using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SelfMonitoringKayitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "self_monitoring_registrations",
                columns: table => new
                {
                    service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_self_monitoring_registrations", x => x.service_name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "self_monitoring_registrations");
        }
    }
}
