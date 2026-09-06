using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class ParametrosOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametrosOrcamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AtualizadoPor = table.Column<string>(type: "text", nullable: true),
                    MensalBase = table.Column<decimal>(type: "numeric", nullable: false),
                    MaquinasIncluidas = table.Column<int>(type: "integer", nullable: false),
                    MensalPorMaquinaExtra = table.Column<decimal>(type: "numeric", nullable: false),
                    MensalPorServidorExposto = table.Column<decimal>(type: "numeric", nullable: false),
                    CustoVpsMensal = table.Column<decimal>(type: "numeric", nullable: false),
                    MensalHospedagem = table.Column<decimal>(type: "numeric", nullable: false),
                    ImplantacaoBase = table.Column<decimal>(type: "numeric", nullable: false),
                    ImplantacaoPorMaquina = table.Column<decimal>(type: "numeric", nullable: false),
                    FatorEstendida = table.Column<decimal>(type: "numeric", nullable: false),
                    Fator24x7 = table.Column<decimal>(type: "numeric", nullable: false),
                    MensalPor90DiasExtras = table.Column<decimal>(type: "numeric", nullable: false),
                    ValidadeDias = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosOrcamento", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrosOrcamento");
        }
    }
}
