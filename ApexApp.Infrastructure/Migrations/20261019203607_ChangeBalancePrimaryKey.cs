using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeBalancePrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "balances_pkey",
                table: "balances");

            migrationBuilder.AddPrimaryKey(
                name: "balances_pkey",
                table: "balances",
                columns: new[] { "user_id", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "balances_pkey",
                table: "balances");

            migrationBuilder.AddPrimaryKey(
                name: "balances_pkey",
                table: "balances",
                column: "user_id");
        }
    }
}
