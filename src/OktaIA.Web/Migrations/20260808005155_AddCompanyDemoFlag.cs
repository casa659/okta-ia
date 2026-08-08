using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OktaIA.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDemoFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Demo",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Demo",
                table: "Companies");
        }
    }
}
