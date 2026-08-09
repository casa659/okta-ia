using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class PlataformaDeIntegracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conectores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Fabricante = table.Column<string>(type: "text", nullable: false),
                    TipoAuth = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UrlBase = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "text", nullable: true),
                    UltimoSyncEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UltimoHealthCheckEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LatenciaMs = table.Column<int>(type: "integer", nullable: true),
                    UltimoErro = table.Column<string>(type: "text", nullable: true),
                    UltimoErroEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conectores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conectores_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertasUnificados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ConectorId = table.Column<int>(type: "integer", nullable: false),
                    IdExterno = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Severidade = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: true),
                    AtivoNome = table.Column<string>(type: "text", nullable: true),
                    AtivoIp = table.Column<string>(type: "text", nullable: true),
                    UsuarioAfetado = table.Column<string>(type: "text", nullable: true),
                    OcorridoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngeridoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatusOrigem = table.Column<string>(type: "text", nullable: true),
                    Resolvido = table.Column<bool>(type: "boolean", nullable: false),
                    DadosBrutosJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasUnificados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertasUnificados_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertasUnificados_Conectores_ConectorId",
                        column: x => x.ConectorId,
                        principalTable: "Conectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CredenciaisConector",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConectorId = table.Column<int>(type: "integer", nullable: false),
                    SegredoCifrado = table.Column<string>(type: "text", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriadoPor = table.Column<string>(type: "text", nullable: true),
                    RotacionadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredenciaisConector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CredenciaisConector_Conectores_ConectorId",
                        column: x => x.ConectorId,
                        principalTable: "Conectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CursoresSync",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConectorId = table.Column<int>(type: "integer", nullable: false),
                    Escopo = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: true),
                    UltimoSyncEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ItensNoUltimoSync = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CursoresSync", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CursoresSync_Conectores_ConectorId",
                        column: x => x.ConectorId,
                        principalTable: "Conectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecucoesSync",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConectorId = table.Column<int>(type: "integer", nullable: false),
                    Escopo = table.Column<string>(type: "text", nullable: false),
                    IniciadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinalizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ItensLidos = table.Column<int>(type: "integer", nullable: false),
                    ItensNovos = table.Column<int>(type: "integer", nullable: false),
                    Sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    Erro = table.Column<string>(type: "text", nullable: true),
                    Automatico = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecucoesSync", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecucoesSync_Conectores_ConectorId",
                        column: x => x.ConectorId,
                        principalTable: "Conectores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasUnificados_CompanyId_OcorridoEm",
                table: "AlertasUnificados",
                columns: new[] { "CompanyId", "OcorridoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasUnificados_ConectorId_IdExterno",
                table: "AlertasUnificados",
                columns: new[] { "ConectorId", "IdExterno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conectores_CompanyId_Slug",
                table: "Conectores",
                columns: new[] { "CompanyId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CredenciaisConector_ConectorId",
                table: "CredenciaisConector",
                column: "ConectorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CursoresSync_ConectorId_Escopo",
                table: "CursoresSync",
                columns: new[] { "ConectorId", "Escopo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesSync_ConectorId_IniciadoEm",
                table: "ExecucoesSync",
                columns: new[] { "ConectorId", "IniciadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasUnificados");

            migrationBuilder.DropTable(
                name: "CredenciaisConector");

            migrationBuilder.DropTable(
                name: "CursoresSync");

            migrationBuilder.DropTable(
                name: "ExecucoesSync");

            migrationBuilder.DropTable(
                name: "Conectores");
        }
    }
}
