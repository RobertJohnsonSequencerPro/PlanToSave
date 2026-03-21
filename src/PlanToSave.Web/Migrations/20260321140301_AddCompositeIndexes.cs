using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActualFlows_Date",
                schema: "plantosave",
                table: "ActualFlows");

            migrationBuilder.DropIndex(
                name: "IX_ActualFlows_UserId",
                schema: "plantosave",
                table: "ActualFlows");

            migrationBuilder.DropIndex(
                name: "IX_ActivityPlans_UserId",
                schema: "plantosave",
                table: "ActivityPlans");

            migrationBuilder.CreateIndex(
                name: "IX_ActualFlows_UserId_Date",
                schema: "plantosave",
                table: "ActualFlows",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_UserId_Status",
                schema: "plantosave",
                table: "ActivityPlans",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActualFlows_UserId_Date",
                schema: "plantosave",
                table: "ActualFlows");

            migrationBuilder.DropIndex(
                name: "IX_ActivityPlans_UserId_Status",
                schema: "plantosave",
                table: "ActivityPlans");

            migrationBuilder.CreateIndex(
                name: "IX_ActualFlows_Date",
                schema: "plantosave",
                table: "ActualFlows",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ActualFlows_UserId",
                schema: "plantosave",
                table: "ActualFlows",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_UserId",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "UserId");
        }
    }
}
