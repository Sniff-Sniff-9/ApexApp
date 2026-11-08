using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApexApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "department_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    parent_department_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                    table.ForeignKey(
                        name: "FK_departments_departments_parent_department_id",
                        column: x => x.parent_department_id,
                        principalSchema: "public",
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.id);
                });

            migrationBuilder.Sql("""
                INSERT INTO public.departments (name, is_active)
                SELECT DISTINCT BTRIM(department), TRUE
                FROM public.users
                WHERE NULLIF(BTRIM(department), '') IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO public.positions (name, is_active)
                SELECT DISTINCT BTRIM(position), TRUE
                FROM public.users
                WHERE NULLIF(BTRIM(position), '') IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE public.users AS u
                SET department_id = d.id
                FROM public.departments AS d
                WHERE NULLIF(BTRIM(u.department), '') = d.name;
                """);

            migrationBuilder.Sql("""
                UPDATE public.users AS u
                SET position_id = p.id
                FROM public.positions AS p
                WHERE NULLIF(BTRIM(u.position), '') = p.name;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_department_id",
                table: "users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_position_id",
                table: "users",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "IX_departments_parent_department_id",
                schema: "public",
                table: "departments",
                column: "parent_department_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_departments_department_id",
                table: "users",
                column: "department_id",
                principalSchema: "public",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_positions_position_id",
                table: "users",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "department",
                table: "users");

            migrationBuilder.DropColumn(
                name: "position",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "position",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE public.users AS u
                SET department = d.name
                FROM public.departments AS d
                WHERE u.department_id = d.id;
                """);

            migrationBuilder.Sql("""
                UPDATE public.users AS u
                SET position = p.name
                FROM public.positions AS p
                WHERE u.position_id = p.id;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_users_departments_department_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_positions_position_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_department_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_position_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "position_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "positions");
        }
    }
}
