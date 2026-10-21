using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "products");

            migrationBuilder.AlterColumn<TicketStatus>(
                name: "status",
                table: "support_tickets",
                type: "ticket_status",
                nullable: false,
                defaultValue: TicketStatus.Open,
                oldClrType: typeof(int),
                oldType: "ticket_status",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<QuestStepCompletionType>(
                name: "completion_type",
                table: "quest_steps",
                type: "quest_step_completion_type",
                nullable: false,
                defaultValue: QuestStepCompletionType.Manual,
                oldClrType: typeof(int),
                oldType: "quest_step_completion_type",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<OrderStatus>(
                name: "status",
                table: "orders",
                type: "order_status",
                nullable: false,
                defaultValue: OrderStatus.Pending,
                oldClrType: typeof(int),
                oldType: "order_status",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<BalanceType>(
                name: "type",
                table: "balances",
                type: "balance_type",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "balance_type",
                oldDefaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "support_tickets",
                type: "ticket_status",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(TicketStatus),
                oldType: "ticket_status",
                oldDefaultValue: TicketStatus.Open);

            migrationBuilder.AlterColumn<int>(
                name: "completion_type",
                table: "quest_steps",
                type: "quest_step_completion_type",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(QuestStepCompletionType),
                oldType: "quest_step_completion_type",
                oldDefaultValue: QuestStepCompletionType.Manual);

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "products",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                table: "products",
                type: "text[]",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "orders",
                type: "order_status",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(OrderStatus),
                oldType: "order_status",
                oldDefaultValue: OrderStatus.Pending);

            migrationBuilder.AlterColumn<int>(
                name: "type",
                table: "balances",
                type: "balance_type",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(BalanceType),
                oldType: "balance_type");
        }
    }
}
