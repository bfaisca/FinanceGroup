using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa3_CardsInstallmentsRecurrences)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etapa3_CardsInstallmentsRecurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "credit_card_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_number",
                table: "transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "installment_plan_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_manually_edited",
                table: "transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "occurrence_date",
                table: "transactions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recurrence_rule_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "credit_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    institution = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    closing_day = table.Column<int>(type: "integer", nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_cards", x => x.id);
                    table.CheckConstraint("ck_credit_cards_closing_day_range", "closing_day BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_credit_cards_credit_limit_non_negative", "credit_limit >= 0");
                    table.CheckConstraint("ck_credit_cards_due_day_range", "due_day BETWEEN 1 AND 31");
                    table.ForeignKey(
                        name: "fk_credit_cards_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_credit_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installment_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: false),
                    first_date = table.Column<DateOnly>(type: "date", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installment_plans", x => x.id);
                    table.CheckConstraint("ck_installment_plans_card_without_account", "credit_card_id IS NULL OR account_id IS NULL");
                    table.CheckConstraint("ck_installment_plans_installment_count_range", "installment_count BETWEEN 2 AND 120");
                    table.CheckConstraint("ck_installment_plans_total_amount_positive", "total_amount > 0");
                    table.ForeignKey(
                        name: "fk_installment_plans_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installment_plans_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installment_plans_credit_cards_credit_card_id",
                        column: x => x.credit_card_id,
                        principalTable: "credit_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installment_plans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurrence_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    interval = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurrence_rules", x => x.id);
                    table.CheckConstraint("ck_recurrence_rules_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_recurrence_rules_card_expense_without_account", "credit_card_id IS NULL OR (type = 'Expense' AND account_id IS NULL)");
                    table.CheckConstraint("ck_recurrence_rules_end_date_after_start", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("ck_recurrence_rules_interval_range", "\"interval\" BETWEEN 1 AND 365");
                    table.ForeignKey(
                        name: "fk_recurrence_rules_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurrence_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurrence_rules_credit_cards_credit_card_id",
                        column: x => x.credit_card_id,
                        principalTable: "credit_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recurrence_rules_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_credit_card_id_due_date",
                table: "transactions",
                columns: new[] { "credit_card_id", "due_date" },
                filter: "credit_card_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_transactions_installment_plan_id_number",
                table: "transactions",
                columns: new[] { "installment_plan_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_transactions_recurrence_rule_id_occurrence_date",
                table: "transactions",
                columns: new[] { "recurrence_rule_id", "occurrence_date" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_credit_card_rules",
                table: "transactions",
                sql: "credit_card_id IS NULL OR (type = 'Expense' AND account_id IS NULL AND payment_method = 'CreditCard')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_installment_number_range",
                table: "transactions",
                sql: "installment_number IS NULL OR installment_number BETWEEN 1 AND 120");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_origin_consistency",
                table: "transactions",
                sql: "(installment_plan_id IS NULL) = (installment_number IS NULL) AND (recurrence_rule_id IS NULL) = (occurrence_date IS NULL) AND NOT (installment_plan_id IS NOT NULL AND recurrence_rule_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_credit_cards_account_id",
                table: "credit_cards",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_credit_cards_user_id_is_active",
                table: "credit_cards",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_installment_plans_account_id",
                table: "installment_plans",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_plans_category_id",
                table: "installment_plans",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_plans_credit_card_id",
                table: "installment_plans",
                column: "credit_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_plans_user_id_created_at",
                table: "installment_plans",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recurrence_rules_account_id",
                table: "recurrence_rules",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurrence_rules_category_id",
                table: "recurrence_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurrence_rules_credit_card_id",
                table: "recurrence_rules",
                column: "credit_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurrence_rules_is_active",
                table: "recurrence_rules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_recurrence_rules_user_id_is_active",
                table: "recurrence_rules",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_credit_cards_credit_card_id",
                table: "transactions",
                column: "credit_card_id",
                principalTable: "credit_cards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_installment_plans_installment_plan_id",
                table: "transactions",
                column: "installment_plan_id",
                principalTable: "installment_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_recurrence_rules_recurrence_rule_id",
                table: "transactions",
                column: "recurrence_rule_id",
                principalTable: "recurrence_rules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Índice único por expressão (EF Core não os modela; criado por SQL): nome do cartão único por usuário,
            // sem diferenciar maiúsculas.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_credit_cards_user_id_lower_name ON credit_cards (user_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_credit_cards_credit_card_id",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_installment_plans_installment_plan_id",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_recurrence_rules_recurrence_rule_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "installment_plans");

            migrationBuilder.DropTable(
                name: "recurrence_rules");

            migrationBuilder.DropTable(
                name: "credit_cards");

            migrationBuilder.DropIndex(
                name: "ix_transactions_credit_card_id_due_date",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ux_transactions_installment_plan_id_number",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ux_transactions_recurrence_rule_id_occurrence_date",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_credit_card_rules",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_installment_number_range",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_origin_consistency",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "credit_card_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "installment_number",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "installment_plan_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "is_manually_edited",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "occurrence_date",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "recurrence_rule_id",
                table: "transactions");
        }
    }
}
