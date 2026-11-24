using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Arrays constantes são gerados pelo EF e executados uma única vez
#pragma warning disable CA1707 // Nome da migration definido pelo escopo (Etapa4e_TelegramGroups)

namespace FinanceApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Etapa4e_TelegramGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "telegram_pending_expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "telegram_group_selections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telegram_group_selections", x => x.id);
                    table.ForeignKey(
                        name: "fk_telegram_group_selections_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_telegram_group_selections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_telegram_pending_expenses_group_id",
                table: "telegram_pending_expenses",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_group_selections_group_id",
                table: "telegram_group_selections",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_telegram_group_selections_user_id",
                table: "telegram_group_selections",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_telegram_pending_expenses_groups_group_id",
                table: "telegram_pending_expenses",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_telegram_pending_expenses_groups_group_id",
                table: "telegram_pending_expenses");

            migrationBuilder.DropTable(
                name: "telegram_group_selections");

            migrationBuilder.DropIndex(
                name: "ix_telegram_pending_expenses_group_id",
                table: "telegram_pending_expenses");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "telegram_pending_expenses");
        }
    }
}
