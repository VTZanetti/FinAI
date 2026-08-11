using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinAI.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "budgets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false, comment: "Mês do orçamento (1..12)"),
                    year = table.Column<int>(type: "integer", nullable: false, comment: "Ano do orçamento"),
                    limit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budgets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subcategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_recurring = table.Column<bool>(type: "boolean", nullable: false),
                    external_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "is_system", "name", "subcategory", "user_id" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), true, "Food", "Restaurant", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111102"), true, "Food", "Groceries", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111103"), true, "Transportation", "Ride Sharing", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111104"), true, "Transportation", "Fuel", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111105"), true, "Transportation", "Public Transit", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111106"), true, "Housing", "Rent", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111107"), true, "Housing", "Mortgage", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111108"), true, "Utilities", "Electricity", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111109"), true, "Utilities", "Water", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110a"), true, "Utilities", "Internet", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110b"), true, "Health", "Pharmacy", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110c"), true, "Health", "Medical", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110d"), true, "Entertainment", "Streaming", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110e"), true, "Entertainment", "Games", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-11111111110f"), true, "Education", "Courses", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111110"), true, "Travel", "Flights", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111111"), true, "Shopping", "Online", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111112"), true, "Shopping", "Retail", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111113"), true, "Income", "Salary", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111114"), true, "Income", "Freelance", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11111111-1111-1111-1111-111111111115"), true, "Other", null, new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_user_id_name",
                table: "accounts",
                columns: new[] { "user_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_budgets_user_id_category_id_year_month",
                table: "budgets",
                columns: new[] { "user_id", "category_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_user_id_name_subcategory",
                table: "categories",
                columns: new[] { "user_id", "name", "subcategory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_account_id",
                table: "transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_account_id_date",
                table: "transactions",
                columns: new[] { "user_id", "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_category_id_date",
                table: "transactions",
                columns: new[] { "user_id", "category_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_date",
                table: "transactions",
                columns: new[] { "user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_external_id",
                table: "transactions",
                columns: new[] { "user_id", "external_id" },
                unique: true,
                filter: "\"external_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budgets");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
