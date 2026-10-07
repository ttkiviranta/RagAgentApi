using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagAgentApi.Migrations
{
    public partial class AddGraphRAGConcepts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<System.Text.Json.JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_Name",
                table: "Concepts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateTable(
                name: "Relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<System.Text.Json.JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relations_Concepts_SourceConceptId",
                        column: x => x.SourceConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Relations_Concepts_TargetConceptId",
                        column: x => x.TargetConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Relations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Relations_Source_Target",
                table: "Relations",
                columns: new[] { "SourceConceptId", "TargetConceptId" });

            migrationBuilder.CreateIndex(
                name: "IX_Relations_DocumentId",
                table: "Relations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Relations_SourceConceptId",
                table: "Relations",
                column: "SourceConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Relations_TargetConceptId",
                table: "Relations",
                column: "TargetConceptId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Relations");

            migrationBuilder.DropTable(
                name: "Concepts");
        }
    }
}
