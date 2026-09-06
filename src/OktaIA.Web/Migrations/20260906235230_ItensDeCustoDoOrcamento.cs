using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class ItensDeCustoDoOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItensDeCustoJson",
                table: "Orcamentos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItensDeCustoJson",
                table: "Orcamentos");
        }
    }
}
