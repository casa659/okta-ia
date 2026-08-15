using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class TriagemDeAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O `dotnet ef` avisa "may result in the loss of data" por causa deste DropColumn.
            // Verificado antes de aplicar: `Resolvido` era declarado no modelo mas NENHUM código
            // lia ou escrevia nele (grep em todo o projeto) — ou seja, era sempre `false` e não
            // guarda informação de triagem nenhuma. Nada a preservar. O estado real passa a ser
            // a coluna `Status` (StatusTriagem), que tem quatro valores em vez de dois.
            migrationBuilder.DropColumn(
                name: "Resolvido",
                table: "AlertasUnificados");

            migrationBuilder.AddColumn<string>(
                name: "NotaTriagem",
                table: "AlertasUnificados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Responsavel",
                table: "AlertasUnificados",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AlertasUnificados",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TriadoEm",
                table: "AlertasUnificados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriadoPor",
                table: "AlertasUnificados",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotaTriagem",
                table: "AlertasUnificados");

            migrationBuilder.DropColumn(
                name: "Responsavel",
                table: "AlertasUnificados");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AlertasUnificados");

            migrationBuilder.DropColumn(
                name: "TriadoEm",
                table: "AlertasUnificados");

            migrationBuilder.DropColumn(
                name: "TriadoPor",
                table: "AlertasUnificados");

            migrationBuilder.AddColumn<bool>(
                name: "Resolvido",
                table: "AlertasUnificados",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
