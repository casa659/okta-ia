using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class PrecoFechadoAMao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorImplantacaoCalculado",
                table: "Orcamentos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorImplantacaoManual",
                table: "Orcamentos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensalCalculado",
                table: "Orcamentos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensalManual",
                table: "Orcamentos",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorImplantacaoCalculado",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ValorImplantacaoManual",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ValorMensalCalculado",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "ValorMensalManual",
                table: "Orcamentos");
        }
    }
}
