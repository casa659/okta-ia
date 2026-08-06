using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddScanRealFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FonteScan",
                table: "Vulnerabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AutorizadoEm",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutorizadoParaScan",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Real",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UltimoScanEm",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FonteScan",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "AutorizadoEm",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AutorizadoParaScan",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Real",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "UltimoScanEm",
                table: "Assets");
        }
    }
}
