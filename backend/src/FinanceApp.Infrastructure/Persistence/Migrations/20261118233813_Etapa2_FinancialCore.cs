using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa2_FinancialCore)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etapa2_FinancialCore : Migration
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
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    institution = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_default_without_user", "(NOT is_default) OR user_id IS NULL");
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_categories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_balance_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_balance_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_balance_snapshots_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_at = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.CheckConstraint("ck_transactions_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_transactions_due_date_after_date", "due_date IS NULL OR due_date >= transaction_date");
                    table.CheckConstraint("ck_transactions_paid_amount_range", "paid_amount >= 0 AND paid_amount <= amount");
                    table.CheckConstraint("ck_transactions_status_settlement", "status IN ('Planned','Pending','Paid','Received','Canceled') AND (status = 'Canceled' OR ((status IN ('Paid','Received')) = (paid_amount = amount))) AND (status <> 'Paid' OR type = 'Expense') AND (status <> 'Received' OR type = 'Income') AND (paid_at IS NULL OR status IN ('Paid','Received')) AND (paid_by_user_id IS NULL OR status IN ('Paid','Received'))");
                    table.ForeignKey(
                        name: "fk_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    field = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    old_value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_audit_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_transaction_audit_entries_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transaction_audit_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_payments", x => x.id);
                    table.CheckConstraint("ck_transaction_payments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_transaction_payments_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_payments_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transaction_payments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "color", "icon", "is_active", "is_default", "name", "parent_category_id", "type", "user_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "#F97316", "pi-shopping-cart", true, true, "Alimentação", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "#EF4444", "pi-heart", true, true, "Saúde", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "#3B82F6", "pi-book", true, true, "Educação", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "#A855F7", "pi-ticket", true, true, "Lazer", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "#6366F1", "pi-sync", true, true, "Assinaturas", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "#14B8A6", "pi-home", true, true, "Moradia", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "#EAB308", "pi-car", true, true, "Transporte", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000008"), "#EC4899", "pi-shopping-bag", true, true, "Compras", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0001-000000000009"), "#64748B", "pi-ellipsis-h", true, true, "Outros", null, "Expense", null },
                    { new Guid("00000000-0000-0000-0002-000000000001"), "#22C55E", "pi-wallet", true, true, "Salário", null, "Income", null },
                    { new Guid("00000000-0000-0000-0002-000000000002"), "#0EA5E9", "pi-briefcase", true, true, "Freelance", null, "Income", null },
                    { new Guid("00000000-0000-0000-0002-000000000003"), "#10B981", "pi-chart-line", true, true, "Investimentos", null, "Income", null },
                    { new Guid("00000000-0000-0000-0002-000000000004"), "#84CC16", "pi-replay", true, true, "Reembolsos", null, "Income", null },
                    { new Guid("00000000-0000-0000-0002-000000000005"), "#64748B", "pi-ellipsis-h", true, true, "Outros", null, "Income", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_balance_snapshots_account_id_reference_date_created",
                table: "account_balance_snapshots",
                columns: new[] { "account_id", "reference_date", "created_at" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_id_is_active",
                table: "accounts",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_category_id",
                table: "categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_id",
                table: "categories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_audit_entries_transaction_id_created_at",
                table: "transaction_audit_entries",
                columns: new[] { "transaction_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_audit_entries_user_id",
                table: "transaction_audit_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_account_id",
                table: "transaction_payments",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_created_by_user_id",
                table: "transaction_payments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_transaction_id",
                table: "transaction_payments",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_id",
                table: "transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_created_by_user_id_due_date_open",
                table: "transactions",
                columns: new[] { "created_by_user_id", "due_date" },
                filter: "status IN ('Planned','Pending')");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_created_by_user_id_transaction_date",
                table: "transactions",
                columns: new[] { "created_by_user_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_paid_by_user_id",
                table: "transactions",
                column: "paid_by_user_id");

            // Índices únicos por expressão (EF Core não os modela; criados por SQL).
            // Contas: nome único por usuário, sem diferenciar maiúsculas.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_accounts_user_id_lower_name ON accounts (user_id, lower(name));");

            // Categorias: nome único entre irmãs (mesmo escopo de usuário, tipo e pai). NULLs (categorias padrão e
            // categorias raiz) são normalizados para um UUID zerado, já que NULL nunca colide em índices únicos.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_categories_scope_type_parent_lower_name ON categories (" +
                "COALESCE(user_id, '00000000-0000-0000-0000-000000000000'::uuid), " +
                "type, " +
                "COALESCE(parent_category_id, '00000000-0000-0000-0000-000000000000'::uuid), " +
                "lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_balance_snapshots");

            migrationBuilder.DropTable(
                name: "transaction_audit_entries");

            migrationBuilder.DropTable(
                name: "transaction_payments");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
