using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa4_Groups)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Etapa 4 consolidada (pré-lançamento, D-051): grupos, convites, membros, séries de despesas recorrentes, notificações e as
    /// colunas/regras de grupo em <c>transactions</c> e <c>transaction_payments</c> (juros/multa).
    /// </summary>
    public partial class Etapa4_Groups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_due_date_after_date",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transaction_payments_created_by_user_id",
                table: "transaction_payments");

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_series_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "series_number",
                table: "transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "interest_amount",
                table: "transaction_payments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "transaction_payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Pagamentos já gravados (Etapas 2/3) nunca foram editados: updated_at = created_at.
            migrationBuilder.Sql("UPDATE transaction_payments SET updated_at = created_at;");

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_groups_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_invitations_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_group_invitations_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_transaction_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    occurrences = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_transaction_series", x => x.id);
                    table.CheckConstraint("ck_group_transaction_series_frequency", "frequency IN ('Weekly','Biweekly','Monthly','Bimonthly','Quarterly','Semiannual','Annual')");
                    table.CheckConstraint("ck_group_transaction_series_occurrences", "occurrences BETWEEN 2 AND 60");
                    table.ForeignKey(
                        name: "fk_group_transaction_series_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_group_transaction_series_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    series_occurrences = table.Column<int>(type: "integer", nullable: true),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_series_occurrences", "series_occurrences IS NULL OR (type = 'GroupTransactionCreated' AND series_occurrences BETWEEN 2 AND 60)");
                    table.CheckConstraint("ck_notifications_shape", "(type IN ('GroupTransactionCreated','GroupPaymentRegistered') AND transaction_id IS NOT NULL AND transaction_description IS NOT NULL AND amount IS NOT NULL AND amount > 0 AND invitation_id IS NULL AND (actor_user_id IS NULL OR actor_user_id <> recipient_user_id)) OR (type = 'GroupInvitationReceived' AND invitation_id IS NOT NULL AND transaction_id IS NULL AND transaction_description IS NULL AND amount IS NULL AND series_occurrences IS NULL)");
                    table.ForeignKey(
                        name: "fk_notifications_group_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "group_invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_group_id_created_by_user_id",
                table: "transactions",
                columns: new[] { "group_id", "created_by_user_id" },
                filter: "group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_group_id_transaction_date",
                table: "transactions",
                columns: new[] { "group_id", "transaction_date" },
                filter: "group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_transactions_group_series_id_series_number",
                table: "transactions",
                columns: new[] { "group_series_id", "series_number" },
                unique: true,
                filter: "group_series_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_due_date_after_date",
                table: "transactions",
                sql: "due_date IS NULL OR group_id IS NOT NULL OR due_date >= transaction_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_group_rules",
                table: "transactions",
                sql: "group_id IS NULL OR (type = 'Expense' AND account_id IS NULL AND credit_card_id IS NULL AND installment_plan_id IS NULL AND recurrence_rule_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_group_series",
                table: "transactions",
                sql: "(group_series_id IS NULL) = (series_number IS NULL) AND (group_series_id IS NULL OR group_id IS NOT NULL) AND (series_number IS NULL OR series_number BETWEEN 1 AND 60)");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_created_by_user_id_transaction_id",
                table: "transaction_payments",
                columns: new[] { "created_by_user_id", "transaction_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_payments_interest_non_negative",
                table: "transaction_payments",
                sql: "interest_amount >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_group_invitations_group_id_created_at",
                table: "group_invitations",
                columns: new[] { "group_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_group_invitations_invited_by_user_id",
                table: "group_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_group_invitations_group_id_email_pending",
                table: "group_invitations",
                columns: new[] { "group_id", "email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_group_invitations_token_hash",
                table: "group_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_members_group_id_status",
                table: "group_members",
                columns: new[] { "group_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_group_members_user_id_status",
                table: "group_members",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_group_members_group_id_user_id",
                table: "group_members",
                columns: new[] { "group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_transaction_series_created_by_user_id",
                table: "group_transaction_series",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_transaction_series_group_id",
                table: "group_transaction_series",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_created_by_user_id",
                table: "groups",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_actor_user_id",
                table: "notifications",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_group_id",
                table: "notifications",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_invitation_id",
                table: "notifications",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_read_at",
                table: "notifications",
                column: "read_at",
                filter: "read_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id_unread",
                table: "notifications",
                column: "recipient_user_id",
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_transaction_id",
                table: "notifications",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_recipient_user_id_invitation_id",
                table: "notifications",
                columns: new[] { "recipient_user_id", "invitation_id" },
                unique: true,
                filter: "invitation_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_group_transaction_series_group_series_id",
                table: "transactions",
                column: "group_series_id",
                principalTable: "group_transaction_series",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_groups_group_id",
                table: "transactions",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_group_transaction_series_group_series_id",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_groups_group_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "group_transaction_series");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "group_invitations");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropIndex(
                name: "ix_transactions_group_id_created_by_user_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_group_id_transaction_date",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ux_transactions_group_series_id_series_number",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_due_date_after_date",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_group_rules",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_group_series",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transaction_payments_created_by_user_id_transaction_id",
                table: "transaction_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_payments_interest_non_negative",
                table: "transaction_payments");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "group_series_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "series_number",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "interest_amount",
                table: "transaction_payments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "transaction_payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_due_date_after_date",
                table: "transactions",
                sql: "due_date IS NULL OR due_date >= transaction_date");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_created_by_user_id",
                table: "transaction_payments",
                column: "created_by_user_id");
        }
    }
}
