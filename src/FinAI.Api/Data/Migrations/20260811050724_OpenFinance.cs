using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinAI.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpenFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "accounts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "open_finance_syncs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    accounts_imported = table.Column<int>(type: "integer", nullable: false),
                    transactions_imported = table.Column<int>(type: "integer", nullable: false),
                    transactions_skipped = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_finance_syncs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_bank_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    institution_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sync_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_bank_connections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_user_id_external_id",
                table: "accounts",
                columns: new[] { "user_id", "external_id" });

            migrationBuilder.CreateIndex(
                name: "IX_open_finance_syncs_user_id_started_at",
                table: "open_finance_syncs",
                columns: new[] { "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_bank_connections_user_id_item_id",
                table: "user_bank_connections",
                columns: new[] { "user_id", "item_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "open_finance_syncs");

            migrationBuilder.DropTable(
                name: "user_bank_connections");

            migrationBuilder.DropIndex(
                name: "IX_accounts_user_id_external_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "accounts");
        }
    }
}
