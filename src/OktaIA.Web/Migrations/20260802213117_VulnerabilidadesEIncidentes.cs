using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class VulnerabilidadesEIncidentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Severidade = table.Column<string>(type: "text", nullable: false),
                    Asset = table.Column<string>(type: "text", nullable: false),
                    MitreCode = table.Column<string>(type: "text", nullable: false),
                    Analista = table.Column<string>(type: "text", nullable: true),
                    EventosCount = table.Column<int>(type: "integer", nullable: false),
                    TituloPt = table.Column<string>(type: "text", nullable: false),
                    TituloEn = table.Column<string>(type: "text", nullable: false),
                    AiResumoPt = table.Column<string>(type: "text", nullable: false),
                    AiResumoEn = table.Column<string>(type: "text", nullable: false),
                    AiConfianca = table.Column<string>(type: "text", nullable: false),
                    NarrativaPt = table.Column<string>(type: "text", nullable: false),
                    NarrativaEn = table.Column<string>(type: "text", nullable: false),
                    AbertoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cve = table.Column<string>(type: "text", nullable: false),
                    Cvss = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    Componente = table.Column<string>(type: "text", nullable: false),
                    TituloPt = table.Column<string>(type: "text", nullable: false),
                    TituloEn = table.Column<string>(type: "text", nullable: false),
                    Cwe = table.Column<string>(type: "text", nullable: false),
                    AssetNome = table.Column<string>(type: "text", nullable: false),
                    ExposicaoPt = table.Column<string>(type: "text", nullable: false),
                    ExposicaoEn = table.Column<string>(type: "text", nullable: false),
                    PrioridadeIa = table.Column<int>(type: "integer", nullable: false),
                    StatusPt = table.Column<string>(type: "text", nullable: false),
                    StatusEn = table.Column<string>(type: "text", nullable: false),
                    Severidade = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncidentSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    DescricaoPt = table.Column<string>(type: "text", nullable: false),
                    DescricaoEn = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentSteps_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Hora = table.Column<string>(type: "text", nullable: false),
                    Cor = table.Column<string>(type: "text", nullable: false),
                    DescricaoPt = table.Column<string>(type: "text", nullable: false),
                    DescricaoEn = table.Column<string>(type: "text", nullable: false),
                    Origem = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTimelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTimelineEvents_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentSteps_IncidentId",
                table: "IncidentSteps",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineEvents_IncidentId",
                table: "IncidentTimelineEvents",
                column: "IncidentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentSteps");

            migrationBuilder.DropTable(
                name: "IncidentTimelineEvents");

            migrationBuilder.DropTable(
                name: "Vulnerabilities");

            migrationBuilder.DropTable(
                name: "Incidents");
        }
    }
}
