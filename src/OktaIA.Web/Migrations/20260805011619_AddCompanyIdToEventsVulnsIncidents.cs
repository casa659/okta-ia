using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToEventsVulnsIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Vulnerabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "SecurityEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Incidents",
                type: "integer",
                nullable: true);

            // Backfill: liga cada evento/CVE/incidente já existente à empresa dona do ativo citado
            // no texto (Alvo/AssetNome/Asset), casando pelo nome com Assets.Nome — mesma associação
            // que Assets.CompanyId já tem desde o seed original. Linhas cujo nome não bate com
            // nenhum Asset (ex.: "srv-app-04") ficam com CompanyId nulo.
            migrationBuilder.Sql(
                "UPDATE \"SecurityEvents\" e SET \"CompanyId\" = a.\"CompanyId\" " +
                "FROM \"Assets\" a WHERE a.\"Nome\" = e.\"Alvo\";");
            migrationBuilder.Sql(
                "UPDATE \"Vulnerabilities\" v SET \"CompanyId\" = a.\"CompanyId\" " +
                "FROM \"Assets\" a WHERE a.\"Nome\" = v.\"AssetNome\";");
            migrationBuilder.Sql(
                "UPDATE \"Incidents\" i SET \"CompanyId\" = a.\"CompanyId\" " +
                "FROM \"Assets\" a WHERE a.\"Nome\" = i.\"Asset\";");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_CompanyId",
                table: "Vulnerabilities",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CompanyId",
                table: "SecurityEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CompanyId",
                table: "Incidents",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Companies_CompanyId",
                table: "Incidents",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityEvents_Companies_CompanyId",
                table: "SecurityEvents",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vulnerabilities_Companies_CompanyId",
                table: "Vulnerabilities",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Companies_CompanyId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityEvents_Companies_CompanyId",
                table: "SecurityEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Vulnerabilities_Companies_CompanyId",
                table: "Vulnerabilities");

            migrationBuilder.DropIndex(
                name: "IX_Vulnerabilities_CompanyId",
                table: "Vulnerabilities");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_CompanyId",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_CompanyId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Incidents");
        }
    }
}
