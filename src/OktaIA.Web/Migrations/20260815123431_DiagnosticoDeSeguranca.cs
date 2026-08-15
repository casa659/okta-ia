using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class DiagnosticoDeSeguranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Diagnosticos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "text", nullable: false),
                    ConcluidoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Respondente = table.Column<string>(type: "text", nullable: true),
                    RespondenteCargo = table.Column<string>(type: "text", nullable: true),
                    RealizadoEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    Cobertura = table.Column<int>(type: "integer", nullable: true),
                    Maturidade = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    UsoDoInvestimento = table.Column<int>(type: "integer", nullable: true),
                    Integracao = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diagnosticos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Diagnosticos_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoAnalises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiagnosticoId = table.Column<int>(type: "integer", nullable: false),
                    GeradaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeradaPor = table.Column<string>(type: "text", nullable: false),
                    Resultado = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: true),
                    MotivoRecusa = table.Column<string>(type: "text", nullable: true),
                    Erro = table.Column<string>(type: "text", nullable: true),
                    ResumoExecutivo = table.Column<string>(type: "text", nullable: true),
                    ResumoTecnico = table.Column<string>(type: "text", nullable: true),
                    Inconsistencias = table.Column<string>(type: "text", nullable: true),
                    LeituraDoInvestimento = table.Column<string>(type: "text", nullable: true),
                    PerguntasAdicionais = table.Column<string>(type: "text", nullable: true),
                    TokensEntrada = table.Column<int>(type: "integer", nullable: true),
                    TokensSaida = table.Column<int>(type: "integer", nullable: true),
                    TokensCacheLidos = table.Column<int>(type: "integer", nullable: true),
                    DuracaoMs = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoAnalises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoAnalises_Diagnosticos_DiagnosticoId",
                        column: x => x.DiagnosticoId,
                        principalTable: "Diagnosticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoFerramentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiagnosticoId = table.Column<int>(type: "integer", nullable: false),
                    DominioCodigo = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Fabricante = table.Column<string>(type: "text", nullable: false),
                    Produto = table.Column<string>(type: "text", nullable: true),
                    Versao = table.Column<string>(type: "text", nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    Responsavel = table.Column<string>(type: "text", nullable: true),
                    LicencaExpiraEm = table.Column<DateOnly>(type: "date", nullable: true),
                    Licenciado = table.Column<bool>(type: "boolean", nullable: false),
                    Atualizado = table.Column<bool>(type: "boolean", nullable: false),
                    Monitorado = table.Column<bool>(type: "boolean", nullable: false),
                    AlertasTratados = table.Column<bool>(type: "boolean", nullable: false),
                    IntegradaAoLokta = table.Column<bool>(type: "boolean", nullable: false),
                    ConectorSlug = table.Column<string>(type: "text", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoFerramentas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoFerramentas_Diagnosticos_DiagnosticoId",
                        column: x => x.DiagnosticoId,
                        principalTable: "Diagnosticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoRespostas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiagnosticoId = table.Column<int>(type: "integer", nullable: false),
                    PerguntaCodigo = table.Column<string>(type: "text", nullable: false),
                    Opcao = table.Column<string>(type: "text", nullable: true),
                    Texto = table.Column<string>(type: "text", nullable: true),
                    Numero = table.Column<int>(type: "integer", nullable: true),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    Origem = table.Column<string>(type: "text", nullable: false),
                    EvidenciaArquivo = table.Column<string>(type: "text", nullable: true),
                    RespondidoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoRespostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoRespostas_Diagnosticos_DiagnosticoId",
                        column: x => x.DiagnosticoId,
                        principalTable: "Diagnosticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoRiscos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiagnosticoId = table.Column<int>(type: "integer", nullable: false),
                    DominioCodigo = table.Column<string>(type: "text", nullable: false),
                    PerguntaCodigo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Gravidade = table.Column<string>(type: "text", nullable: false),
                    Origem = table.Column<string>(type: "text", nullable: false),
                    SeNaoTratar = table.Column<string>(type: "text", nullable: true),
                    Recomendacao = table.Column<string>(type: "text", nullable: true),
                    Prioridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoRiscos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoRiscos_Diagnosticos_DiagnosticoId",
                        column: x => x.DiagnosticoId,
                        principalTable: "Diagnosticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoAcoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiagnosticoId = table.Column<int>(type: "integer", nullable: false),
                    RiscoId = table.Column<int>(type: "integer", nullable: true),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Horizonte = table.Column<string>(type: "text", nullable: false),
                    Encaminhamento = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: true),
                    Concluida = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoAcoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoAcoes_DiagnosticoRiscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "DiagnosticoRiscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiagnosticoAcoes_Diagnosticos_DiagnosticoId",
                        column: x => x.DiagnosticoId,
                        principalTable: "Diagnosticos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoAcoes_DiagnosticoId",
                table: "DiagnosticoAcoes",
                column: "DiagnosticoId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoAcoes_RiscoId",
                table: "DiagnosticoAcoes",
                column: "RiscoId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoAnalises_DiagnosticoId_GeradaEm",
                table: "DiagnosticoAnalises",
                columns: new[] { "DiagnosticoId", "GeradaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoFerramentas_DiagnosticoId_DominioCodigo",
                table: "DiagnosticoFerramentas",
                columns: new[] { "DiagnosticoId", "DominioCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoRespostas_DiagnosticoId_PerguntaCodigo",
                table: "DiagnosticoRespostas",
                columns: new[] { "DiagnosticoId", "PerguntaCodigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoRiscos_DiagnosticoId_Prioridade",
                table: "DiagnosticoRiscos",
                columns: new[] { "DiagnosticoId", "Prioridade" });

            migrationBuilder.CreateIndex(
                name: "IX_Diagnosticos_CompanyId_CriadoEm",
                table: "Diagnosticos",
                columns: new[] { "CompanyId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagnosticoAcoes");

            migrationBuilder.DropTable(
                name: "DiagnosticoAnalises");

            migrationBuilder.DropTable(
                name: "DiagnosticoFerramentas");

            migrationBuilder.DropTable(
                name: "DiagnosticoRespostas");

            migrationBuilder.DropTable(
                name: "DiagnosticoRiscos");

            migrationBuilder.DropTable(
                name: "Diagnosticos");
        }
    }
}
