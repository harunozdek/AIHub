using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DashboardPencereIndeksleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_log_entries_errors_received_at_id",
                table: "log_entries",
                columns: new[] { "received_at", "id" },
                filter: "level IN ('Error', 'Critical')");

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_received_at_exception_type",
                table: "log_entries",
                column: "received_at",
                filter: "exception_type IS NOT NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "exception_type" });

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_warnings_received_at_id",
                table: "log_entries",
                columns: new[] { "received_at", "id" },
                filter: "level = 'Warning'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_entries_errors_received_at_id",
                table: "log_entries");

            migrationBuilder.DropIndex(
                name: "ix_log_entries_received_at_exception_type",
                table: "log_entries");

            migrationBuilder.DropIndex(
                name: "ix_log_entries_warnings_received_at_id",
                table: "log_entries");
        }
    }
}
