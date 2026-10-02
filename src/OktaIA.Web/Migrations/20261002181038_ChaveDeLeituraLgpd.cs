using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChaveDeLeituraLgpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChaveLgpdEm",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveLgpdHash",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveLgpdPor",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChaveLgpdPrefixo",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChaveLgpdUsoEm",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChaveLgpdEm",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ChaveLgpdHash",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ChaveLgpdPor",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ChaveLgpdPrefixo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ChaveLgpdUsoEm",
                table: "Companies");
        }
    }
}
