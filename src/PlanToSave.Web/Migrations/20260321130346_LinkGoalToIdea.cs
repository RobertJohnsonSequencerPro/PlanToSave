using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class LinkGoalToIdea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdeaId",
                schema: "plantosave",
                table: "Goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_IdeaId",
                schema: "plantosave",
                table: "Goals",
                column: "IdeaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Ideas_IdeaId",
                schema: "plantosave",
                table: "Goals",
                column: "IdeaId",
                principalSchema: "plantosave",
                principalTable: "Ideas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Ideas_IdeaId",
                schema: "plantosave",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_IdeaId",
                schema: "plantosave",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "IdeaId",
                schema: "plantosave",
                table: "Goals");
        }
    }
}
