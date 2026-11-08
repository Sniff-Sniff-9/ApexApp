using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "product_variant_id",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "product_variant_id",
                table: "cart_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<long>(type: "bigint", nullable: true),
                    stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_variant_id",
                table: "order_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_product_variant_id",
                table: "cart_items",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_product_id",
                schema: "public",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_product_variants_product_variant_id",
                table: "cart_items",
                column: "product_variant_id",
                principalSchema: "public",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_product_variants_product_variant_id",
                table: "order_items",
                column: "product_variant_id",
                principalSchema: "public",
                principalTable: "product_variants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_product_variants_product_variant_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_product_variants_product_variant_id",
                table: "order_items");

            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_order_items_product_variant_id",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "IX_cart_items_product_variant_id",
                table: "cart_items");

            migrationBuilder.DropColumn(
                name: "product_variant_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "product_variant_id",
                table: "cart_items");
        }
    }
}
