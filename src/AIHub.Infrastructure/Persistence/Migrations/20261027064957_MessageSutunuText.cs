using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MessageSutunuText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "message",
                table: "log_entries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32768)",
                oldMaxLength: 32768);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "message",
                table: "log_entries",
                type: "character varying(32768)",
                maxLength: 32768,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
