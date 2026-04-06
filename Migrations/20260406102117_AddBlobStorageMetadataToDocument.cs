using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagAgentApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBlobStorageMetadataToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobContainer",
                table: "Documents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlobName",
                table: "Documents",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlobUploadedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlobUri",
                table: "Documents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileHash",
                table: "Documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OriginalFileSizeBytes",
                table: "Documents",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlobContainer",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BlobName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BlobUploadedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BlobUri",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OriginalFileHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OriginalFileSizeBytes",
                table: "Documents");
        }
    }
}
