using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagAgentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemoType = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ResultData = table.Column<string>(type: "jsonb", nullable: false),
                    ExecutionTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemoTestData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemoType = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoTestData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoExecutions_CreatedAt",
                table: "DemoExecutions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DemoExecutions_DemoType",
                table: "DemoExecutions",
                column: "DemoType");

            migrationBuilder.CreateIndex(
                name: "IX_DemoExecutions_DemoType_CreatedAt",
                table: "DemoExecutions",
                columns: new[] { "DemoType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoTestData_ContentHash",
                table: "DemoTestData",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_DemoTestData_CreatedAt",
                table: "DemoTestData",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DemoTestData_DemoType",
                table: "DemoTestData",
                column: "DemoType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoExecutions");

            migrationBuilder.DropTable(
                name: "DemoTestData");
        }
    }
}
