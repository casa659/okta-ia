using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class EventosEInfra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InfraHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CpuPct = table.Column<int>(type: "integer", nullable: false),
                    RamPct = table.Column<int>(type: "integer", nullable: false),
                    DiscoPct = table.Column<int>(type: "integer", nullable: false),
                    RedePct = table.Column<int>(type: "integer", nullable: false),
                    LatenciaMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfraHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoPt = table.Column<string>(type: "text", nullable: false),
                    TipoEn = table.Column<string>(type: "text", nullable: false),
                    Severidade = table.Column<string>(type: "text", nullable: false),
                    OrigemPaisCodigo = table.Column<string>(type: "text", nullable: false),
                    OrigemPaisNomePt = table.Column<string>(type: "text", nullable: false),
                    OrigemPaisNomeEn = table.Column<string>(type: "text", nullable: false),
                    OrigemLat = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    OrigemLng = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    OrigemIp = table.Column<string>(type: "text", nullable: false),
                    Alvo = table.Column<string>(type: "text", nullable: false),
                    Bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CriadoEm",
                table: "SecurityEvents",
                column: "CriadoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InfraHealthSnapshots");

            migrationBuilder.DropTable(
                name: "SecurityEvents");
        }
    }
}
