using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                schema: "plantosave",
                table: "Accounts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                schema: "plantosave",
                table: "Accounts");
        }
    }
}
