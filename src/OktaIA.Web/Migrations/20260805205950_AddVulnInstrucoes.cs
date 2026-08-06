using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVulnInstrucoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstrucoesEn",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstrucoesPt",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstrucoesEn",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "InstrucoesPt",
                table: "Vulnerabilities");
        }
    }
}
