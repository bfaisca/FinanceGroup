using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa4b_Notifications)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etapa4b_Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    transaction_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_shape", "(type IN ('GroupTransactionCreated','GroupPaymentRegistered') AND transaction_id IS NOT NULL AND transaction_description IS NOT NULL AND transaction_type IS NOT NULL AND amount IS NOT NULL AND amount > 0 AND invitation_id IS NULL AND (actor_user_id IS NULL OR actor_user_id <> recipient_user_id)) OR (type = 'GroupInvitationReceived' AND invitation_id IS NOT NULL AND transaction_id IS NULL AND transaction_description IS NULL AND transaction_type IS NULL AND amount IS NULL)");
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
