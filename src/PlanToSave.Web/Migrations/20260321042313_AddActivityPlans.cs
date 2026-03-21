using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanToSave.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityPlans",
                schema: "plantosave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IdeaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityPlans_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "plantosave",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityPlans_Ideas_IdeaId",
                        column: x => x.IdeaId,
                        principalSchema: "plantosave",
                        principalTable: "Ideas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySteps",
                schema: "plantosave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySteps_ActivityPlans_ActivityPlanId",
                        column: x => x.ActivityPlanId,
                        principalSchema: "plantosave",
                        principalTable: "ActivityPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_IdeaId",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "IdeaId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_PlannedDate",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "PlannedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPlans_UserId",
                schema: "plantosave",
                table: "ActivityPlans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySteps_ActivityPlanId",
                schema: "plantosave",
                table: "ActivitySteps",
                column: "ActivityPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivitySteps",
                schema: "plantosave");

            migrationBuilder.DropTable(
                name: "ActivityPlans",
                schema: "plantosave");
        }
    }
}
