using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa4_Groups)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etapa4_Groups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transaction_payments_created_by_user_id",
                table: "transaction_payments");

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_group_rules",
                table: "transactions",
                sql: "group_id IS NULL OR (account_id IS NULL AND credit_card_id IS NULL AND installment_plan_id IS NULL AND recurrence_rule_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_created_by_user_id_transaction_id",
                table: "transaction_payments",
                columns: new[] { "created_by_user_id", "transaction_id" });

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
                name: "ix_groups_created_by_user_id",
                table: "groups",
                column: "created_by_user_id");

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
                name: "fk_transactions_groups_group_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "group_invitations");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropIndex(
                name: "ix_transactions_group_id_created_by_user_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_group_id_transaction_date",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_group_rules",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transaction_payments_created_by_user_id_transaction_id",
                table: "transaction_payments");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "transactions");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_payments_created_by_user_id",
                table: "transaction_payments",
                column: "created_by_user_id");
        }
    }
}
