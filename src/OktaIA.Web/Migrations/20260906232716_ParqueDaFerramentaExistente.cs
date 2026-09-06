using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class ParqueDaFerramentaExistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ DEFAULT 900, NÃO 0. A linha de ParametrosOrcamento em produção já existe (id=1) e
            // não passa pelo construtor do modelo — sem o valor aqui, ela ganharia onboarding
            // GRÁTIS para todo cliente que marcar "já tem ferramenta", até alguém notar e corrigir
            // na tela. Mesmo valor do padrão em CalculadoraDeOrcamento.ImplantacaoOnboardingFerramentaExistente.
            migrationBuilder.AddColumn<decimal>(
                name: "ImplantacaoOnboardingFerramentaExistente",
                table: "ParametrosOrcamento",
                type: "numeric",
                nullable: false,
                defaultValue: 900m);

            migrationBuilder.AddColumn<string>(
                name: "FerramentaExistente",
                table: "Orcamentos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "JaTemFerramenta",
                table: "Orcamentos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImplantacaoOnboardingFerramentaExistente",
                table: "ParametrosOrcamento");

            migrationBuilder.DropColumn(
                name: "FerramentaExistente",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "JaTemFerramenta",
                table: "Orcamentos");
        }
    }
}
