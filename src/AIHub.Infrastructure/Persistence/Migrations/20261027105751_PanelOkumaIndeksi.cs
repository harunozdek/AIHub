using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PanelOkumaIndeksi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_entries_received_at",
                table: "log_entries");

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_received_at_id",
                table: "log_entries",
                columns: new[] { "received_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_log_entries_received_at_id",
                table: "log_entries");

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_received_at",
                table: "log_entries",
                column: "received_at");
        }
    }
}
