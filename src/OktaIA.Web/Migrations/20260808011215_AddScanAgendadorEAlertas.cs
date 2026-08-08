using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddScanAgendadorEAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MonitoramentoContinuo",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ScanAlertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    AssetNome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    TituloPt = table.Column<string>(type: "text", nullable: false),
                    TituloEn = table.Column<string>(type: "text", nullable: false),
                    Severidade = table.Column<int>(type: "integer", nullable: false),
                    CategoriaScan = table.Column<string>(type: "text", nullable: true),
                    DetectadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Automatico = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanAlertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanAlertas_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanAlertas_CompanyId",
                table: "ScanAlertas",
                column: "CompanyId");

            // NENHUM backfill de propósito: monitoramento contínuo é item comercial (o cliente
            // paga pela revarredura recorrente), então todo ativo nasce e permanece desligado até
            // que o operador ligue explicitamente no chip da aba Scanner. Ligar em massa aqui
            // faria a plataforma escanear sozinha domínios de quem não contratou.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanAlertas");

            migrationBuilder.DropColumn(
                name: "MonitoramentoContinuo",
                table: "Assets");
        }
    }
}
