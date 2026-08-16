using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SunBloom.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    parent_skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generation_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generator_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generator_prompt_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_skills_skills_parent_skill_id",
                        column: x => x.parent_skill_id,
                        principalSchema: "catalog",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skill_relationships",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    strength = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 1.0m),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_relationships", x => x.id);
                    table.CheckConstraint("ck_skill_rel_no_self", "from_skill_id <> to_skill_id");
                    table.ForeignKey(
                        name: "fk_skill_relationships_skills_from_skill_id",
                        column: x => x.from_skill_id,
                        principalSchema: "catalog",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_skill_relationships_skills_to_skill_id",
                        column: x => x.to_skill_id,
                        principalSchema: "catalog",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_skill_relationships_from_skill_id_type",
                schema: "catalog",
                table: "skill_relationships",
                columns: new[] { "from_skill_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_skill_relationships_to_skill_id_type",
                schema: "catalog",
                table: "skill_relationships",
                columns: new[] { "to_skill_id", "type" });

            migrationBuilder.CreateIndex(
                name: "uq_skill_rel",
                schema: "catalog",
                table: "skill_relationships",
                columns: new[] { "from_skill_id", "to_skill_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_parent_skill_id",
                schema: "catalog",
                table: "skills",
                column: "parent_skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_review_state",
                schema: "catalog",
                table: "skills",
                column: "review_state");

            migrationBuilder.CreateIndex(
                name: "ix_skills_slug",
                schema: "catalog",
                table: "skills",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skill_relationships",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "catalog");
        }
    }
}
