using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class OrigemFrameworkNoDiagnostico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrigemFramework",
                table: "Diagnosticos",
                type: "text",
                nullable: true);

            // Backfill: diagnósticos importados de planilha ANTES deste campo existir. O título
            // sempre foi "Levantamento <Nome do Framework> · planilha" — ver
            // Diagnosticos.cshtml.cs, OnPostImportarAsync. Sem isto, o diagnóstico real da
            // Cleveris (LGPD, importado em 06/09/2026) ficaria com OrigemFramework nulo e cairia
            // na proposta da plataforma inteira, exatamente o que se está corrigindo agora.
            migrationBuilder.Sql(
                "UPDATE \"Diagnosticos\" SET \"OrigemFramework\" = 'LGPD' " +
                "WHERE \"Titulo\" LIKE 'Levantamento LGPD%' AND \"OrigemFramework\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigemFramework",
                table: "Diagnosticos");
        }
    }
}
