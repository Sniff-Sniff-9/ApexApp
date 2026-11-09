using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                schema: "public",
                table: "product_variants");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                schema: "public",
                table: "product_variants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_variant_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_variant_categories_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_category_id",
                schema: "public",
                table: "product_variants",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_product_variants_product_variant_categories_category_id",
                schema: "public",
                table: "product_variants",
                column: "category_id",
                principalTable: "product_variant_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_variants_product_variant_categories_category_id",
                schema: "public",
                table: "product_variants");

            migrationBuilder.DropTable(
                name: "product_variant_categories");

            migrationBuilder.DropIndex(
                name: "IX_product_variants_category_id",
                schema: "public",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "category_id",
                schema: "public",
                table: "product_variants");

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "public",
                table: "product_variants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
