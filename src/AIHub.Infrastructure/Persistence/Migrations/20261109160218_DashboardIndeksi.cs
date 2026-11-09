using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DashboardIndeksi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_log_entries_received_at_dashboard",
                table: "log_entries",
                column: "received_at")
                .Annotation("Npgsql:IndexInclude", new[] { "level", "service_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_entries_received_at_dashboard",
                table: "log_entries");
        }
    }
}
