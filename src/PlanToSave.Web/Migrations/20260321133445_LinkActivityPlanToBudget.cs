using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class LinkActivityPlanToBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "PlannedFlowId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityPlans_PlannedFlows_PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "PlannedFlowId",
                principalSchema: "plantosave",
                principalTable: "PlannedFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityPlans_PlannedFlows_PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans");

            migrationBuilder.DropIndex(
                name: "IX_ActivityPlans_PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans");

            migrationBuilder.DropColumn(
                name: "PlannedFlowId",
                schema: "plantosave",
                table: "ActivityPlans");
        }
    }
}
