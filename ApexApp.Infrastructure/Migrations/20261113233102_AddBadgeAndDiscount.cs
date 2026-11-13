using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeAndDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "product_variants_id_product_id_key",
                schema: "public",
                table: "product_variants",
                columns: new[] { "id", "product_id" });

            migrationBuilder.CreateTable(
                name: "product_badges",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, defaultValue: "#808080"),
                    assignment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "manual"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_badges_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_discounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    product_variant_id = table.Column<int>(type: "integer", nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_discounts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "product_discounts_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "product_discounts_product_variant_id_product_id_fkey",
                        columns: x => new { x.product_variant_id, x.product_id },
                        principalSchema: "public",
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_badge_assignments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    product_variant_id = table.Column<int>(type: "integer", nullable: true),
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_badge_assignments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "product_badge_assignments_badge_id_fkey",
                        column: x => x.badge_id,
                        principalTable: "product_badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "product_badge_assignments_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "product_badge_assignments_product_variant_id_product_id_fkey",
                        columns: x => new { x.product_variant_id, x.product_id },
                        principalSchema: "public",
                        principalTable: "product_variants",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_badge_settings",
                columns: table => new
                {
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    period_days = table.Column<int>(type: "integer", nullable: true),
                    threshold = table.Column<long>(type: "bigint", nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("product_badge_settings_pkey", x => x.badge_id);
                    table.ForeignKey(
                        name: "product_badge_settings_badge_id_fkey",
                        column: x => x.badge_id,
                        principalTable: "product_badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_badge_assignments_product_variant_id_product_id",
                table: "product_badge_assignments",
                columns: new[] { "product_variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "product_badge_assignments_badge_id_idx",
                table: "product_badge_assignments",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "product_badge_assignments_product_id_idx",
                table: "product_badge_assignments",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "product_badge_assignments_product_variant_id_idx",
                table: "product_badge_assignments",
                column: "product_variant_id");

            migrationBuilder.CreateIndex(
                name: "product_badges_code_key",
                table: "product_badges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_discounts_product_variant_id_product_id",
                table: "product_discounts",
                columns: new[] { "product_variant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "product_discounts_product_id_idx",
                table: "product_discounts",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "product_discounts_product_variant_id_idx",
                table: "product_discounts",
                column: "product_variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_badge_assignments");

            migrationBuilder.DropTable(
                name: "product_badge_settings");

            migrationBuilder.DropTable(
                name: "product_discounts");

            migrationBuilder.DropTable(
                name: "product_badges");

            migrationBuilder.DropUniqueConstraint(
                name: "product_variants_id_product_id_key",
                schema: "public",
                table: "product_variants");
        }
    }
}
