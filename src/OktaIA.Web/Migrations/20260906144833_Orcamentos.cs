using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class Orcamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orcamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    NomeEmpresa = table.Column<string>(type: "text", nullable: false),
                    Cnpj = table.Column<string>(type: "text", nullable: true),
                    Contato = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Telefone = table.Column<string>(type: "text", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    EstacoesWindows = table.Column<int>(type: "integer", nullable: false),
                    EstacoesOutras = table.Column<int>(type: "integer", nullable: false),
                    Servidores = table.Column<int>(type: "integer", nullable: false),
                    ServidoresExpostos = table.Column<int>(type: "integer", nullable: false),
                    HospedagemDoCliente = table.Column<bool>(type: "boolean", nullable: false),
                    RetencaoDias = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<string>(type: "text", nullable: false),
                    Conformidade = table.Column<string>(type: "text", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    MaquinasTotal = table.Column<int>(type: "integer", nullable: false),
                    ValorImplantacao = table.Column<decimal>(type: "numeric", nullable: false),
                    ValorMensal = table.Column<decimal>(type: "numeric", nullable: false),
                    CustoDiretoMensal = table.Column<decimal>(type: "numeric", nullable: false),
                    MemoriaDeCalculo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriadaPor = table.Column<string>(type: "text", nullable: true),
                    EnviadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondidaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidaAte = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orcamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orcamentos_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_CompanyId",
                table: "Orcamentos",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orcamentos_Numero",
                table: "Orcamentos",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Orcamentos");
        }
    }
}
