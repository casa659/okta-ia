using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVulnRecomendacaoECategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoriaScan",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecomendacaoEn",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecomendacaoPt",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoriaScan",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "RecomendacaoEn",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "RecomendacaoPt",
                table: "Vulnerabilities");
        }
    }
}
