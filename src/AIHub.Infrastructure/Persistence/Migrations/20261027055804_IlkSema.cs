using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IlkSema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    message = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    exception_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    stack_trace = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    key_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_api_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_received_at",
                table: "log_entries",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "ix_log_entries_service_name_received_at",
                table: "log_entries",
                columns: new[] { "service_name", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ux_service_api_keys_key_hash",
                table: "service_api_keys",
                column: "key_hash",
                unique: true);

            // Saklanan LogEntry değişmez (docs/domain.md): UPDATE veritabanı düzeyinde reddedilir.
            // DELETE serbesttir — retention (BR-4) için gereklidir.
            migrationBuilder.Sql("""
                CREATE FUNCTION log_entries_reject_update() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'log_entries kayitlari degistirilemez (LogEntry degismezligi)';
                END;
                $$;
                CREATE TRIGGER log_entries_no_update BEFORE UPDATE ON log_entries
                    FOR EACH ROW EXECUTE FUNCTION log_entries_reject_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS log_entries_no_update ON log_entries;
                DROP FUNCTION IF EXISTS log_entries_reject_update();
                """);
            migrationBuilder.DropTable(
                name: "log_entries");

            migrationBuilder.DropTable(
                name: "service_api_keys");
        }
    }
}
